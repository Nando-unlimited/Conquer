using System.Runtime.InteropServices;
using Silk.NET.GLFW;

namespace Conquer.Client.Graphics;

/// <summary>
/// Checks for OpenGL 3.3 core before the real window is made: without it, creating the window
/// crashes natively instead of throwing. On Linux, old GPUs fall back to Mesa's software renderer.
/// </summary>
public static class GlSupport
{
    /// <summary>True if the game can open its window, switching to software rendering on Linux when needed.</summary>
    public static bool Ensure()
    {
        if (Probe()) return true;
        if (!OperatingSystem.IsLinux()) return false;

        // Mesa reads this from the native environment, which Environment.SetEnvironmentVariable does not change.
        setenv("LIBGL_ALWAYS_SOFTWARE", "1", 1);
        Console.Error.WriteLine("La tarjeta gráfica no admite OpenGL 3.3; se usa el renderizado por software (más lento).");
        return Probe();
    }

    /// <summary>Opens a hidden OpenGL 3.3 core window like the game's; GLFW returns null if it cannot.</summary>
    private static unsafe bool Probe()
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
