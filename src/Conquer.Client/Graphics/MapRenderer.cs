using Conquer.Game.Simulation;
using Conquer.Game.World;
using Silk.NET.OpenGL;

namespace Conquer.Client.Graphics;

public enum MapMode
{
    Terrain,
    Political,
    Population,
    Mood,
    Fertility,
}

/// <summary>
/// Draws the whole map with one full-screen quad. The fragment shader turns each screen pixel into
/// a map position, looks up the province id there and colours it from per-province lookup textures,
/// so changing an owner only re-uploads a 256x128 texture. Borders are found by comparing ids one
/// screen pixel apart, which keeps them thin at every zoom.
/// </summary>
public sealed class MapRenderer : IDisposable
{
    private const int SlotsX = 256, SlotsY = 128;

    private const string VertexSource = """
        #version 330 core
        layout(location = 0) in vec2 aPos;
        void main() { gl_Position = vec4(aPos, 0.0, 1.0); }
        """;

    private const string FragmentSource = """
        #version 330 core
        out vec4 FragColor;
        uniform vec2 uScreen;
        uniform vec2 uCenter;
        uniform float uPixelScale;
        uniform float uZoom;
        uniform vec2 uMapSize;
        uniform sampler2D uIds;
        uniform sampler2D uTerrain;
        uniform sampler2D uProvColor;
        uniform sampler2D uProvOwner;
        uniform int uSelected;
        uniform int uHover;
        uniform float uProvinceBorders;
        uniform float uSmoothZoom;

        int idAt(vec2 m) {
            ivec2 p = ivec2(int(mod(floor(m.x), uMapSize.x)), clamp(int(floor(m.y)), 0, int(uMapSize.y) - 1));
            vec4 c = texelFetch(uIds, p, 0);
            return int(c.r * 255.0 + 0.5) + int(c.g * 255.0 + 0.5) * 256 + int(c.b * 255.0 + 0.5) * 65536;
        }
        ivec2 slot(int id) { return ivec2(id % 256, id / 256); }
        int ownerOf(int id) {
            vec4 o = texelFetch(uProvOwner, slot(id), 0);
            return int(o.r * 255.0 + 0.5);
        }

        // Coverage (0..1) of an anti-aliased line of the given half-width, from the distance to it in screen pixels.
        float lineCoverage(float distancePx, float halfWidth) {
            return 1.0 - clamp(distancePx - halfWidth + 0.5, 0.0, 1.0);
        }

        // Zoomed in: blend the 2x2 surrounding map pixels so each region (province or owner) becomes
        // a smooth field. The region shown is the strongest; its border runs where the top two tie.
        // The distance to that border is the margin between them over its gradient, which is exact
        // for the bilinear weights (screen-space derivatives break down right on the border).
        void strongest(int keys[4], float w[4], vec2 dw[4], out int index, out float distancePx) {
            float share[4];
            vec2 grad[4];
            index = 0;
            for (int i = 0; i < 4; i++) {
                share[i] = 0.0;
                grad[i] = vec2(0.0);
                for (int j = 0; j < 4; j++) {
                    if (keys[j] == keys[i]) { share[i] += w[j]; grad[i] += dw[j]; }
                }
                if (share[i] > share[index]) index = i;
            }
            int runner = -1;
            for (int i = 0; i < 4; i++)
                if (keys[i] != keys[index] && (runner < 0 || share[i] > share[runner])) runner = i;
            if (runner < 0) { distancePx = 1e6; return; }
            float margin = share[index] - share[runner];
            float slope = max(length(grad[index] - grad[runner]), 1e-4);
            distancePx = margin / slope * uZoom;
        }

        void smoothRegions(vec2 m, out int id, out float provinceDistance, out float ownerDistance) {
            vec2 p = m - 0.5;
            vec2 f = fract(p);
            vec2 b = floor(p) + 0.5;
            int ids[4] = int[4](idAt(b), idAt(b + vec2(1.0, 0.0)), idAt(b + vec2(0.0, 1.0)), idAt(b + vec2(1.0, 1.0)));
            int owners[4] = int[4](ownerOf(ids[0]), ownerOf(ids[1]), ownerOf(ids[2]), ownerOf(ids[3]));
            float w[4] = float[4]((1.0 - f.x) * (1.0 - f.y), f.x * (1.0 - f.y), (1.0 - f.x) * f.y, f.x * f.y);
            vec2 dw[4] = vec2[4](vec2(f.y - 1.0, f.x - 1.0), vec2(1.0 - f.y, -f.x), vec2(-f.y, 1.0 - f.x), vec2(f.y, f.x));

            int top, topOwner;
            strongest(ids, w, dw, top, provinceDistance);
            strongest(owners, w, dw, topOwner, ownerDistance);
            id = ids[top];
        }

        void main() {
            vec2 frag = vec2(gl_FragCoord.x, uScreen.y * uPixelScale - gl_FragCoord.y) / uPixelScale;
            vec2 m = uCenter + (frag - uScreen * 0.5) / uZoom;
            if (m.y < 0.0 || m.y >= uMapSize.y) { FragColor = vec4(0.05, 0.07, 0.1, 1.0); return; }

            vec3 col = texture(uTerrain, m / uMapSize).rgb;
            int id;
            float provinceLine, countryLine;
            if (uZoom >= uSmoothZoom) {
                float provinceDistance, ownerDistance;
                smoothRegions(m, id, provinceDistance, ownerDistance);
                provinceLine = lineCoverage(provinceDistance, 0.6);
                countryLine = lineCoverage(ownerDistance, 1.4);
            } else {
                // Zoomed out a map pixel is smaller than a screen pixel: compare with the next screen pixel.
                id = idAt(m);
                float px = 1.0 / uZoom;
                int idR = idAt(m + vec2(px, 0.0));
                int idD = idAt(m + vec2(0.0, px));
                int own = ownerOf(id);
                provinceLine = (id != idR || id != idD) ? 1.0 : 0.0;
                countryLine = (own != ownerOf(idR) || own != ownerOf(idD)) ? 1.0 : 0.0;
            }

            vec4 pc = texelFetch(uProvColor, slot(id), 0);
            col = mix(col, pc.rgb, pc.a);
            col = mix(col, vec3(0.08, 0.08, 0.08), provinceLine * uProvinceBorders);
            col = mix(col, vec3(0.05, 0.03, 0.02), countryLine * 0.85);

            if (id == uSelected) col = mix(col, vec3(1.0, 1.0, 0.85), 0.35);
            else if (id == uHover) col = mix(col, vec3(1.0), 0.12);
            FragColor = vec4(col, 1.0);
        }
        """;

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _vao, _vbo;
    private readonly WorldMap _map;
    private readonly Texture _ids, _terrain, _provColor, _provOwner;
    private readonly byte[] _colorData = new byte[SlotsX * SlotsY * 4];
    private readonly byte[] _ownerData = new byte[SlotsX * SlotsY * 4];

    public MapMode Mode { get; set; } = MapMode.Terrain;

    /// <summary>Pixel data prepared off the main thread (it takes a moment for 6.5 million pixels).</summary>
    public sealed record Prepared(byte[] Ids, byte[] Terrain);

    public static Prepared Prepare(WorldMap map) => new(EncodeIds(map), TerrainColors.Build(map));

    public unsafe MapRenderer(GL gl, WorldMap map, Prepared prepared)
    {
        if (map.Provinces.Count > SlotsX * SlotsY) throw new InvalidOperationException("Too many provinces for the lookup texture.");
        _gl = gl;
        _map = map;
        _shader = new Shader(gl, VertexSource, FragmentSource);
        _ids = new Texture(gl, map.Width, map.Height, prepared.Ids, smooth: false);
        _terrain = new Texture(gl, map.Width, map.Height, prepared.Terrain, smooth: true, mipmaps: true, repeatX: true);
        _provColor = new Texture(gl, SlotsX, SlotsY, _colorData, smooth: false);
        _provOwner = new Texture(gl, SlotsX, SlotsY, _ownerData, smooth: false);

        float[] quad = [-1, -1, 1, -1, 1, 1, -1, -1, 1, 1, -1, 1];
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, new ReadOnlySpan<float>(quad), BufferUsageARB.StaticDraw);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 8, (void*)0);
        gl.BindVertexArray(0);
    }

    /// <summary>
    /// The province drawn at a map position, matching the shader: zoomed in, the strongest province
    /// of the 2x2 blended map pixels; zoomed out, the map pixel itself. -1 off the map.
    /// </summary>
    public static int ProvinceAt(WorldMap map, System.Numerics.Vector2 m, float zoom)
    {
        if (m.Y < 0 || m.Y >= map.Height) return -1;
        if (zoom < SmoothZoom) return map.ProvinceAt((int)MathF.Floor(m.X), (int)MathF.Floor(m.Y))?.Id ?? -1;

        float px = m.X - 0.5f, py = m.Y - 0.5f;
        int bx = (int)MathF.Floor(px), by = (int)MathF.Floor(py);
        float fx = px - bx, fy = py - by;
        Span<int> ids = stackalloc int[4];
        Span<float> w = [(1 - fx) * (1 - fy), fx * (1 - fy), (1 - fx) * fy, fx * fy];
        for (int i = 0; i < 4; i++)
        {
            int y = Math.Clamp(by + i / 2, 0, map.Height - 1);
            ids[i] = map.ProvinceIds[y * map.Width + map.WrapX(bx + i % 2)];
        }
        int best = ids[0];
        float bestShare = -1;
        for (int i = 0; i < 4; i++)
        {
            float share = 0;
            for (int j = 0; j < 4; j++) if (ids[j] == ids[i]) share += w[j];
            if (share > bestShare) (best, bestShare) = (ids[i], share);
        }
        return best;
    }

    /// <summary>Zoom (screen pixels per map pixel) from which borders are smoothed.</summary>
    public const float SmoothZoom = 2;

    private static byte[] EncodeIds(WorldMap map)
    {
        var data = new byte[map.Width * map.Height * 4];
        for (int i = 0; i < map.ProvinceIds.Length; i++)
        {
            int id = map.ProvinceIds[i];
            data[i * 4] = (byte)id;
            data[i * 4 + 1] = (byte)(id >> 8);
            data[i * 4 + 2] = (byte)(id >> 16);
            data[i * 4 + 3] = 255;
        }
        return data;
    }

    /// <summary>Recomputes every province's overlay colour and owner for the current mode.</summary>
    public void Refresh(GameSession session)
    {
        var players = session.Players;
        foreach (var p in _map.Provinces)
        {
            int o = p.Id * 4;
            _ownerData[o] = (byte)(p.OwnerId + 1);
            _ownerData[o + 3] = 255;

            Rgba color = new(0);
            switch (Mode)
            {
                case MapMode.Terrain:
                    if (p.IsOwned) color = new Rgba(players[p.OwnerId].Color).WithAlpha(0.35f);
                    break;
                case MapMode.Political:
                    if (p.IsOwned) color = new Rgba(players[p.OwnerId].Color).WithAlpha(0.85f);
                    else if (p.IsClaimable) color = new Rgba(0xFFD9D0B4).WithAlpha(0.6f);
                    break;
                case MapMode.Population:
                    if (p.IsOwned && p.Population > 0) color = PopulationColor(p.Population / p.AreaKm2);
                    else if (p.IsClaimable) color = new Rgba(0xFF808080).WithAlpha(0.55f);
                    break;
                case MapMode.Mood:
                    if (p.IsOwned && p.Population > 0) color = ScaleColor(p.Mood / 100);
                    else if (p.IsClaimable) color = new Rgba(0xFF808080).WithAlpha(0.55f);
                    break;
                case MapMode.Fertility:
                    if (p.IsOwned && p.Population > 0) color = ScaleColor(p.Fertility - 0.5);
                    else if (p.IsClaimable) color = new Rgba(0xFF808080).WithAlpha(0.55f);
                    break;
            }
            _colorData[o] = color.R;
            _colorData[o + 1] = color.G;
            _colorData[o + 2] = color.B;
            _colorData[o + 3] = color.A;
        }
        _provColor.Update(_colorData);
        _provOwner.Update(_ownerData);
    }

    /// <summary>Yellow for sparse, deep red for dense (log scale, 0.01 to 100 citizens per km²).</summary>
    private static Rgba PopulationColor(double density)
    {
        float t = (float)Math.Clamp((Math.Log10(Math.Max(density, 0.01)) + 2) / 4, 0, 1);
        byte r = (byte)(255 - 90 * t), g = (byte)(230 * (1 - t)), b = (byte)(60 * (1 - t));
        return new Rgba(0xD0000000u | ((uint)r << 16) | ((uint)g << 8) | b);
    }

    /// <summary>Red at 0, yellow at 0.5, green at 1 (mood and fertility modes).</summary>
    private static Rgba ScaleColor(double value)
    {
        float t = (float)Math.Clamp(value, 0, 1);
        float r = t < 0.5f ? 215 : 215 - 300 * (t - 0.5f);
        float g = t < 0.5f ? 60 + 300 * t : 210 - 60 * (t - 0.5f);
        return new Rgba(0xD0000000u | ((uint)r << 16) | ((uint)g << 8) | 50u);
    }

    /// <param name="pixelScale">Framebuffer pixels per window pixel (high-DPI screens).</param>
    public void Draw(Camera camera, int selectedProvince, int hoverProvince, float pixelScale)
    {
        _gl.Disable(EnableCap.Blend);
        _shader.Use();
        _shader.Set("uScreen", camera.Screen.X, camera.Screen.Y);
        _shader.Set("uPixelScale", pixelScale);
        _shader.Set("uSmoothZoom", SmoothZoom);
        _shader.Set("uCenter", camera.Center.X, camera.Center.Y);
        _shader.Set("uZoom", camera.Zoom);
        _shader.Set("uMapSize", _map.Width, _map.Height);
        _shader.Set("uSelected", selectedProvince);
        _shader.Set("uHover", hoverProvince);
        _shader.Set("uProvinceBorders", Math.Clamp((camera.Zoom - 0.8f) / 2.5f, 0f, 0.45f));
        _ids.Bind(0);
        _shader.Set("uIds", 0);
        _terrain.Bind(1);
        _shader.Set("uTerrain", 1);
        _provColor.Bind(2);
        _shader.Set("uProvColor", 2);
        _provOwner.Bind(3);
        _shader.Set("uProvOwner", 3);
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    public void Dispose()
    {
        _shader.Dispose();
        _ids.Dispose();
        _terrain.Dispose();
        _provColor.Dispose();
        _provOwner.Dispose();
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
    }
}
