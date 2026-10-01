using System.Runtime.InteropServices;
using Silk.NET.GLFW;

namespace Conquer.Client.Graphics;

/// <summary>
/// Checks for OpenGL 3.3 core before the real window is made: without it, creating the window
/// crashes natively instead of throwing. On Linux, old GPUs fall back to Mesa's software renderer.
/// </summary>
public static class GlSupport
{
    /// <summary>Samples per pixel the game's window can smooth edges with: 4 if the card allows it, otherwise 0 (see <see cref="Ensure"/>).</summary>
    public static int Samples { get; private set; }

    /// <summary>True if the game can open its window, switching to software rendering on Linux when needed.</summary>
    public static bool Ensure()
    {
        if (Probe()) return Smoothing();
        if (!OperatingSystem.IsLinux()) return false;

        // Mesa reads this from the native environment, which Environment.SetEnvironmentVariable does not change.
        setenv("LIBGL_ALWAYS_SOFTWARE", "1", 1);
        Console.Error.WriteLine("La tarjeta gráfica no admite OpenGL 3.3; se usa el renderizado por software (más lento).");
        return Probe() && Smoothing();
    }

    /// <summary>Checks whether the window can also have 4× multisampling, which smooths the edges of lines and shapes; always true.</summary>
    private static bool Smoothing()
    {
        Samples = Probe(samples: 4) ? 4 : 0;
        return true;
    }

    /// <summary>Opens a hidden OpenGL 3.3 core window like the game's, with that many samples per pixel; GLFW returns null if it cannot.</summary>
    private static unsafe bool Probe(int samples = 0)
    {
        var glfw = Glfw.GetApi();
        if (!glfw.Init()) return false;
        try
        {
            glfw.WindowHint(WindowHintBool.Visible, false);
            glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGL);
            glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
            glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
            glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
            glfw.WindowHint(WindowHintBool.OpenGLForwardCompat, true);
            glfw.WindowHint(WindowHintInt.Samples, samples);
            var window = glfw.CreateWindow(1, 1, "Conquer", null, null);
            if (window == null) return false;
            glfw.DestroyWindow(window);
            return true;
        }
        finally
        {
            glfw.Terminate();
        }
    }

    [DllImport("libc")]
    private static extern int setenv(string name, string value, int overwrite);
}
