using System.Numerics;
using System.Reflection;
using Conquer.Client.Audio;
using Conquer.Client.Graphics;
using Conquer.Client.Screens;
using Conquer.Client.UI;
using Conquer.Presentation;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Conquer.Client;

/// <summary>A screen with sounds of its own and music of its own; the others play the menu music.</summary>
public interface IAudibleScreen
{
    IReadOnlyList<string> Playlist { get; }
    IReadOnlyList<SoundCue> TakeSounds();
}

public interface IScreen : IDisposable
{
    /// <summary>Updates and draws one frame (immediate-mode UI does both at once).</summary>
    void Frame(double dt);
}

/// <summary>Owns the window, GL context, input and the current screen.</summary>
public sealed class ConquerApp
{
    private readonly IWindow _window;
    private readonly InputState _input = new();
    private IScreen? _screen;
    private IScreen? _nextScreen;
    private AudioPlayer? _audio;

    public GL Gl { get; private set; } = null!;
    public Batch2D Batch { get; private set; } = null!;
    public Font Font { get; private set; } = null!;
    public Ui Ui { get; private set; } = null!;
    /// <summary>
    /// The interface is laid out for at least this many logical pixels; smaller windows shrink
    /// everything by <see cref="UiScale"/> so no screen spills off the edge.
    /// </summary>
    private const float MinUiWidth = 1280, MinUiHeight = 820;

    public float UiScale => _window.Size.X <= 0 || _window.Size.Y <= 0
        ? 1
        : Math.Min(1, Math.Min(_window.Size.X / MinUiWidth, _window.Size.Y / MinUiHeight));

    /// <summary>Window size in logical pixels, the units every screen lays itself out in.</summary>
    public Vector2 ScreenSize => new Vector2(_window.Size.X, _window.Size.Y) / UiScale;

    /// <summary>Framebuffer pixels per logical pixel (HiDPI times <see cref="UiScale"/>).</summary>
    public float PixelScale => ScreenSize.X <= 0 ? 1 : _window.FramebufferSize.X / ScreenSize.X;
    public StartOptions Options { get; }

    public static string Version { get; } = ReadVersion();

    public ConquerApp(StartOptions options)
    {
        Options = options;
        var windowOptions = WindowOptions.Default with
        {
            Size = InitialSize(),
            Title = $"Conquer {Version}",
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
            VSync = true,
            Samples = GlSupport.Samples,
        };
        _window = Window.Create(windowOptions);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += size => Gl?.Viewport(size);
        _window.Closing += () =>
        {
            _screen?.Dispose();
            _audio?.Dispose();
        };
    }

    /// <summary>1600×900, or smaller so the window fits on the monitor with room for its frame and the taskbar.</summary>
    private static Vector2D<int> InitialSize()
    {
        var size = new Vector2D<int>(1600, 900);
        if (Silk.NET.Windowing.Monitor.GetMainMonitor(null).VideoMode.Resolution is { } screen)
        {
            float fit = Math.Min(1, Math.Min(screen.X * 0.9f / size.X, screen.Y * 0.85f / size.Y));
            size = new Vector2D<int>((int)(size.X * fit), (int)(size.Y * fit));
        }
        return size;
    }

    public void Run() => _window.Run();

    public void Quit() => _window.Close();

    /// <summary>Switches screen at the end of the current frame.</summary>
    public void Show(IScreen screen) => _nextScreen = screen;

    private void OnLoad()
    {
        Gl = GL.GetApi(_window);
        Batch = new Batch2D(Gl);
        Font = new Font(Gl);
        Ui = new Ui(Batch, Font, _input);
        _audio = new AudioPlayer();

        var input = _window.CreateInput();
        foreach (var mouse in input.Mice)
        {
            mouse.MouseDown += (m, button) =>
            {
                if (button == MouseButton.Left)
                {
                    _input.LeftDown = _input.LeftPressed = true;
                    _input.LeftPressPosition = m.Position / UiScale;
                }
                else if (button == MouseButton.Right) _input.RightPressed = true;
            };
            mouse.MouseUp += (_, button) =>
            {
                if (button != MouseButton.Left) return;
                _input.LeftDown = false;
                _input.LeftReleased = true;
            };
            mouse.MouseMove += (_, position) => _input.Mouse = position / UiScale;
            mouse.Scroll += (_, wheel) => _input.Scroll += wheel.Y;
        }
        foreach (var keyboard in input.Keyboards)
        {
            keyboard.KeyDown += (_, key, _) =>
            {
                // A key down while it is already held is the system repeating it.
                if (_input.KeysDown.Add(key)) _input.KeysPressed.Add(key);
                else
                {
                    _input.KeysRepeated.Add(key);
                    _input.SystemRepeats = true;
                }
            };
            var clipboard = keyboard;
            _input.ReadClipboard = () => clipboard.ClipboardText ?? "";
            _input.WriteClipboard = text => clipboard.ClipboardText = text;
            keyboard.KeyUp += (_, key, _) => _input.KeysDown.Remove(key);
            keyboard.KeyChar += (_, c) => _input.Chars.Add(c);
        }

        _screen = Options switch
        {
            { QuickStart: { } settings } => new LoadingScreen(this, settings.World, settings.Players, settings.Country),
            { Load: { } path } => new LoadingScreen(this, new SaveFile(Path.GetFullPath(path), Path.GetFileNameWithoutExtension(path), File.GetLastWriteTime(path))),
            { Menu: "new" } => new NewGameScreen(this),
            { Menu: "load" } => new LoadGameScreen(this),
            { Menu: "opciones" } => new MainMenuScreen(this, settingsOpen: true),
            _ => new MainMenuScreen(this),
        };
    }

    private void OnRender(double dt)
    {
        Gl.Viewport(_window.FramebufferSize);
        Gl.ClearColor(0.07f, 0.08f, 0.1f, 1f);
        Gl.Clear(ClearBufferMask.ColorBufferBit);

        Ui.BeginFrame();
        Batch.Begin(ScreenSize);
        _screen?.Frame(Math.Min(dt, 0.25));
        PlayAudio(dt);
        Ui.EndFrame(ScreenSize);
        Batch.Flush();
        _input.EndFrame();
        CaptureIfRequested();

        if (_nextScreen != null)
        {
            _screen?.Dispose();
            _screen = _nextScreen;
            _nextScreen = null;
        }
    }

    /// <summary>The click of any button pressed this frame, the sounds and music of the screen (the menu music elsewhere).</summary>
    private void PlayAudio(double dt)
    {
        if (_audio == null) return;
        var sounds = new List<SoundCue>();
        if (Ui.ButtonClicked) sounds.Add(SoundCue.Click);
        IReadOnlyList<string> playlist = Soundtrack.Menu;
        if (_screen is IAudibleScreen audible)
        {
            playlist = audible.Playlist;
            sounds.AddRange(audible.TakeSounds());
        }
        _audio.Update(dt, playlist, sounds, AudioSettings.Current);
    }

    private int _framesOnScreen;

    /// <summary>With --screenshot, saves a few frames into the game (or the menu) and closes.</summary>
    private void CaptureIfRequested()
    {
        if (Options.Screenshot is not { } path) return;
        // A menu waits for its backdrop, which is built in the background.
        bool ready = Options.QuickStart != null || Options.Load != null ? _screen is GameScreen : _screen is not LoadingScreen && MenuBackground.IsReady;
        _framesOnScreen = ready ? _framesOnScreen + 1 : 0;
        if (_framesOnScreen < 5) return;
        Screenshot.Save(Gl, _window.FramebufferSize.X, _window.FramebufferSize.Y, path);
        Quit();
    }

    private static string ReadVersion()
    {
        var info = typeof(ConquerApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        int plus = info.IndexOf('+');
        return plus >= 0 ? info[..plus] : info;
    }
}
