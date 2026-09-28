using System.Numerics;
using System.Reflection;
using Conquer.Client.Graphics;
using Conquer.Client.Screens;
using Conquer.Client.UI;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Conquer.Client;

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

    public GL Gl { get; private set; } = null!;
    public Batch2D Batch { get; private set; } = null!;
    public Font Font { get; private set; } = null!;
    public Ui Ui { get; private set; } = null!;
    public Vector2 ScreenSize => new(_window.Size.X, _window.Size.Y);
    public float PixelScale => _window.Size.X == 0 ? 1 : _window.FramebufferSize.X / (float)_window.Size.X;
    public StartOptions Options { get; }

    public static string Version { get; } = ReadVersion();

    public ConquerApp(StartOptions options)
    {
        Options = options;
        var windowOptions = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1600, 900),
            Title = $"Conquer {Version}",
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
            VSync = true,
            Samples = 0,
        };
        _window = Window.Create(windowOptions);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += size => Gl?.Viewport(size);
        _window.Closing += () => _screen?.Dispose();
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

        var input = _window.CreateInput();
        foreach (var mouse in input.Mice)
        {
            mouse.MouseDown += (m, button) =>
            {
                if (button == MouseButton.Left)
                {
                    _input.LeftDown = _input.LeftPressed = true;
                    _input.LeftPressPosition = m.Position;
                }
                else if (button == MouseButton.Right) _input.RightPressed = true;
            };
            mouse.MouseUp += (_, button) =>
            {
                if (button != MouseButton.Left) return;
                _input.LeftDown = false;
                _input.LeftReleased = true;
            };
            mouse.MouseMove += (_, position) => _input.Mouse = position;
            mouse.Scroll += (_, wheel) => _input.Scroll += wheel.Y;
        }
        foreach (var keyboard in input.Keyboards)
        {
            keyboard.KeyDown += (_, key, _) =>
            {
                _input.KeysPressed.Add(key);
                _input.KeysDown.Add(key);
            };
            keyboard.KeyUp += (_, key, _) => _input.KeysDown.Remove(key);
        }

        _screen = Options.QuickStart is { } settings
            ? new LoadingScreen(this, settings.World, settings.Players)
            : new MainMenuScreen(this);
    }

    private void OnRender(double dt)
    {
        Gl.Viewport(_window.FramebufferSize);
        Gl.ClearColor(0.07f, 0.08f, 0.1f, 1f);
        Gl.Clear(ClearBufferMask.ColorBufferBit);

        Ui.BeginFrame();
        Batch.Begin(ScreenSize);
        _screen?.Frame(Math.Min(dt, 0.25));
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

    private int _framesOnScreen;

    /// <summary>With --screenshot, saves a few frames into the game (or the menu) and closes.</summary>
    private void CaptureIfRequested()
    {
        if (Options.Screenshot is not { } path) return;
        bool ready = Options.QuickStart == null ? _screen is MainMenuScreen : _screen is GameScreen;
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
