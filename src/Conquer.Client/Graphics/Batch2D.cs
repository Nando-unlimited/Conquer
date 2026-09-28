using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace Conquer.Client.Graphics;

/// <summary>Colour in 0xAARRGGBB, the format the game library uses.</summary>
public readonly record struct Rgba(uint Argb)
{
    public static readonly Rgba White = new(0xFFFFFFFF);
    public static readonly Rgba Black = new(0xFF000000);

    public byte A => (byte)(Argb >> 24);
    public byte R => (byte)(Argb >> 16);
    public byte G => (byte)(Argb >> 8);
    public byte B => (byte)Argb;

    public Rgba WithAlpha(float alpha) => new((Argb & 0x00FFFFFF) | ((uint)Math.Clamp(alpha * 255, 0, 255) << 24));

    public Rgba Scale(float f) => new((Argb & 0xFF000000)
        | ((uint)Math.Clamp(R * f, 0, 255) << 16) | ((uint)Math.Clamp(G * f, 0, 255) << 8) | (uint)Math.Clamp(B * f, 0, 255));

    /// <summary>Packed as the vertex attribute expects (bytes R, G, B, A in memory).</summary>
    internal uint Packed => (uint)R | ((uint)G << 8) | ((uint)B << 16) | ((uint)A << 24);
}

/// <summary>Batches textured, coloured quads and lines in screen pixels (origin top-left).</summary>
public sealed unsafe class Batch2D : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
    {
        public Vector2 Position;
        public Vector2 Uv;
        public uint Color;
    }

    private const string VertexSource = """
        #version 330 core
        layout(location = 0) in vec2 aPos;
        layout(location = 1) in vec2 aUv;
        layout(location = 2) in vec4 aColor;
        uniform vec2 uScreen;
        out vec2 vUv;
        out vec4 vColor;
        void main() {
            vUv = aUv;
            vColor = aColor;
            gl_Position = vec4(aPos.x / uScreen.x * 2.0 - 1.0, 1.0 - aPos.y / uScreen.y * 2.0, 0.0, 1.0);
        }
        """;

    private const string FragmentSource = """
        #version 330 core
        in vec2 vUv;
        in vec4 vColor;
        uniform sampler2D uTexture;
        out vec4 FragColor;
        void main() { FragColor = vColor * texture(uTexture, vUv); }
        """;

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _vao, _vbo;
    private readonly Vertex[] _vertices = new Vertex[6 * 8192];
    private int _count;
    private Texture? _texture;
    private Vector2 _screen;

    public Texture WhiteTexture { get; }

    public Batch2D(GL gl)
    {
        _gl = gl;
        _shader = new Shader(gl, VertexSource, FragmentSource);
        WhiteTexture = new Texture(gl, 1, 1, [255, 255, 255, 255], smooth: false);
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_vertices.Length * sizeof(Vertex)), null, BufferUsageARB.StreamDraw);
        uint stride = (uint)sizeof(Vertex);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)8);
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, stride, (void*)16);
        gl.BindVertexArray(0);
    }

    public void Begin(Vector2 screen)
    {
        _screen = screen;
        _count = 0;
        _texture = null;
    }

    public void Quad(Texture texture, Vector2 p0, Vector2 p1, Vector2 uv0, Vector2 uv1, Rgba color)
    {
        if (_texture != texture || _count + 6 > _vertices.Length) Flush();
        _texture = texture;
        uint c = color.Packed;
        _vertices[_count++] = new Vertex { Position = p0, Uv = uv0, Color = c };
        _vertices[_count++] = new Vertex { Position = new(p1.X, p0.Y), Uv = new(uv1.X, uv0.Y), Color = c };
        _vertices[_count++] = new Vertex { Position = p1, Uv = uv1, Color = c };
        _vertices[_count++] = new Vertex { Position = p0, Uv = uv0, Color = c };
        _vertices[_count++] = new Vertex { Position = p1, Uv = uv1, Color = c };
        _vertices[_count++] = new Vertex { Position = new(p0.X, p1.Y), Uv = new(uv0.X, uv1.Y), Color = c };
    }

    public void Rect(float x, float y, float w, float h, Rgba color) =>
        Quad(WhiteTexture, new(x, y), new(x + w, y + h), Vector2.Zero, Vector2.One, color);

    public void Outline(float x, float y, float w, float h, Rgba color, float thickness = 1)
    {
        Rect(x, y, w, thickness, color);
        Rect(x, y + h - thickness, w, thickness, color);
        Rect(x, y, thickness, h, color);
        Rect(x + w - thickness, y, thickness, h, color);
    }

    public void Line(Vector2 a, Vector2 b, Rgba color, float thickness = 2)
    {
        var d = b - a;
        float len = d.Length();
        if (len < 0.01f) return;
        var n = new Vector2(-d.Y, d.X) / len * (thickness / 2);
        if (_texture != WhiteTexture || _count + 6 > _vertices.Length) Flush();
        _texture = WhiteTexture;
        uint c = color.Packed;
        var uv = new Vector2(0.5f);
        _vertices[_count++] = new Vertex { Position = a + n, Uv = uv, Color = c };
        _vertices[_count++] = new Vertex { Position = b + n, Uv = uv, Color = c };
        _vertices[_count++] = new Vertex { Position = b - n, Uv = uv, Color = c };
        _vertices[_count++] = new Vertex { Position = a + n, Uv = uv, Color = c };
        _vertices[_count++] = new Vertex { Position = b - n, Uv = uv, Color = c };
        _vertices[_count++] = new Vertex { Position = a - n, Uv = uv, Color = c };
    }

    public void Flush()
    {
        if (_count == 0 || _texture == null)
        {
            _count = 0;
            return;
        }
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _shader.Use();
        _shader.Set("uScreen", _screen.X, _screen.Y);
        _shader.Set("uTexture", 0);
        _texture.Bind(0);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, new ReadOnlySpan<Vertex>(_vertices, 0, _count));
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_count);
        _count = 0;
    }

    public void Dispose()
    {
        _shader.Dispose();
        WhiteTexture.Dispose();
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
    }
}
