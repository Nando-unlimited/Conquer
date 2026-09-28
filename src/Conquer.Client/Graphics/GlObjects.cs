using Silk.NET.OpenGL;

namespace Conquer.Client.Graphics;

public sealed class Shader : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, int> _locations = [];
    public uint Handle { get; }

    public Shader(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;
        uint vs = Compile(ShaderType.VertexShader, vertexSource);
        uint fs = Compile(ShaderType.FragmentShader, fragmentSource);
        Handle = gl.CreateProgram();
        gl.AttachShader(Handle, vs);
        gl.AttachShader(Handle, fs);
        gl.LinkProgram(Handle);
        gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0) throw new InvalidOperationException("Shader link failed: " + gl.GetProgramInfoLog(Handle));
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
    }

    private uint Compile(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException($"{type} compile failed: " + _gl.GetShaderInfoLog(shader));
        return shader;
    }

    public void Use() => _gl.UseProgram(Handle);

    private int Location(string name)
    {
        if (!_locations.TryGetValue(name, out int loc)) _locations[name] = loc = _gl.GetUniformLocation(Handle, name);
        return loc;
    }

    public void Set(string name, int value) => _gl.Uniform1(Location(name), value);
    public void Set(string name, float value) => _gl.Uniform1(Location(name), value);
    public void Set(string name, float x, float y) => _gl.Uniform2(Location(name), x, y);

    public void Dispose() => _gl.DeleteProgram(Handle);
}

public sealed class Texture : IDisposable
{
    private readonly GL _gl;
    public uint Handle { get; }
    public int Width { get; }
    public int Height { get; }

    /// <param name="rgba">Width*Height*4 bytes, row 0 at the top.</param>
    public Texture(GL gl, int width, int height, ReadOnlySpan<byte> rgba, bool smooth, bool mipmaps = false, bool repeatX = false)
    {
        _gl = gl;
        Width = width;
        Height = height;
        Handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, Handle);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
        var min = smooth ? (mipmaps ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear) : TextureMinFilter.Nearest;
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)min);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)(smooth ? TextureMagFilter.Linear : TextureMagFilter.Nearest));
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)(repeatX ? TextureWrapMode.Repeat : TextureWrapMode.ClampToEdge));
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        if (mipmaps) gl.GenerateMipmap(TextureTarget.Texture2D);
    }

    public void Update(ReadOnlySpan<byte> rgba)
    {
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, (uint)Width, (uint)Height, PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
    }

    public void Bind(int unit)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + unit);
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose() => _gl.DeleteTexture(Handle);
}
