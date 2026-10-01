using Conquer.Game.Economy;
using Conquer.Game.Science;
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
    Resources,
    Institutions,
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
        uniform sampler2D uDetail;
        uniform float uTime;
        // Per province: its owner's colour (alpha: owned) and, if occupied, the occupier's (alpha: occupied).
        uniform sampler2D uNation;
        uniform sampler2D uOccupier;
        // How strongly the nation's colour bands its border (0: a shadow instead), and how dark that colour is.
        uniform float uNationBand;
        uniform float uBandShade;
        // Each map pixel's distance to the nearest land border between owners (BorderStep per map pixel; 255 far away).
        uniform sampler2D uBorderDistance;
        const float BORDER_STEP = 40.0;
        // TerrainColors.MaxHeight: the metres the detail texture's height channel spans.
        const float MAX_HEIGHT = 9000.0;

        // ---- terrain detail: relief lit from the north-west and procedural texture for each kind of ground

        float hash(vec2 p) {
            vec3 q = fract(vec3(p.xyx) * 0.1031);
            q += dot(q, q.yzx + 33.33);
            return fract((q.x + q.y) * q.z);
        }

        // Smooth value noise; it repeats every `period` cells across, so it wraps with the map.
        float noise(vec2 p, float period) {
            vec2 i = floor(p), f = fract(p);
            vec2 u = f * f * (3.0 - 2.0 * f);
            float x0 = mod(i.x, period), x1 = mod(i.x + 1.0, period);
            float a = hash(vec2(x0, i.y)), b = hash(vec2(x1, i.y));
            float c = hash(vec2(x0, i.y + 1.0)), d = hash(vec2(x1, i.y + 1.0));
            return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
        }

        // One octave of noise, `freq` cells per map pixel, centred on 0. It fades out before its cells get smaller
        // than a few screen pixels, so detail appears as the map is zoomed in and never flickers.
        float detailFade(float freq) { return smoothstep(3.0, 9.0, uZoom / freq); }

        float octave(vec2 m, float freq) {
            float fade = detailFade(freq);
            return fade <= 0.0 ? 0.0 : (noise(m * freq, uMapSize.x * freq) - 0.5) * fade;
        }

        // Clumps of tree crowns: noise pushed towards round blobs, -0.5 (clearing) to 0.5 (canopy).
        float crowns(vec2 m) {
            float fade = detailFade(2.5);
            if (fade <= 0.0) return 0.0;
            float n = 0.7 * noise(m * 2.5, uMapSize.x * 2.5) + 0.3 * noise(m * 5.5 + 17.0, uMapSize.x * 5.5);
            return (smoothstep(0.2, 0.8, n) - 0.5) * fade;
        }

        // Land height in metres: the map's, smoothly interpolated, plus small bumps (bigger on rock) that show up close in.
        float relief(vec2 m, float rock) {
            float h = texture(uDetail, m / uMapSize).r;
            float bumps = (octave(m, 1.5) / 1.5 + octave(m, 4.0) / 4.0 + octave(m, 10.0) / 10.0) * (8.0 + 200.0 * rock);
            return h * h * MAX_HEIGHT + bumps;
        }

        // Lights the land and gives it texture: mottled fields, tree crowns in forests, ripples in sand.
        vec3 landDetail(vec2 m, vec3 col) {
            vec4 ground = texture(uDetail, m / uMapSize);
            float d = max(0.35, 0.75 / uZoom);
            float gx = (relief(m + vec2(d, 0.0), ground.a) - relief(m - vec2(d, 0.0), ground.a)) / (2.0 * d);
            float gy = (relief(m + vec2(0.0, d), ground.a) - relief(m - vec2(0.0, d), ground.a)) / (2.0 * d);
            col *= clamp(1.0 + (gx + gy) * 0.0022, 0.55, 1.4);

            float grain = octave(m, 0.8);
            float dunes = sin(dot(m, vec2(2.4, 0.9)) * 4.0 + octave(m, 1.0) * 10.0) * detailFade(3.0);
            return col * (1.0 + 0.06 * grain + 0.14 * ground.g * crowns(m) + 0.05 * ground.b * dunes);
        }

        // Slow ripples drifting across open water.
        vec3 waterDetail(vec2 m, vec3 col) {
            float t = uTime * 0.04;
            float ripple = octave(m + vec2(t, 0.6 * t), 2.5) + 0.6 * octave(m - vec2(0.7 * t, -0.4 * t), 7.0);
            return col * (1.0 + 0.07 * ripple);
        }

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

        bool isWater(int id) { return texelFetch(uProvOwner, slot(id), 0).g > 0.5; }

        // Zoomed in: blend the 3x3 surrounding map pixels with quadratic B-spline weights, so each region
        // (province, owner, land or sea) becomes a smooth field and its edges smooth curves rather than
        // the pixel staircase. The region shown is the strongest; its border runs where the top two tie.
        // The distance to that border is the margin between them over its gradient, from the weights'
        // exact derivatives (screen-space derivatives break down right on the border).
        void strongest(int keys[9], float w[9], vec2 dw[9], out int index, out float distancePx) {
            float share[9];
            vec2 grad[9];
            index = 0;
            for (int i = 0; i < 9; i++) {
                share[i] = 0.0;
                grad[i] = vec2(0.0);
                for (int j = 0; j < 9; j++) {
                    if (keys[j] == keys[i]) { share[i] += w[j]; grad[i] += dw[j]; }
                }
                if (share[i] > share[index]) index = i;
            }
            int runner = -1;
            for (int i = 0; i < 9; i++)
                if (keys[i] != keys[index] && (runner < 0 || share[i] > share[runner])) runner = i;
            if (runner < 0) { distancePx = 1e6; return; }
            float margin = share[index] - share[runner];
            float slope = max(length(grad[index] - grad[runner]), 1e-4);
            distancePx = margin / slope * uZoom;
        }

        // Also gives the terrain colour blended only from pixels on the same side of the coast as the
        // point, so land and sea meet along the smooth coastline, and the distance to that coast.
        void smoothRegions(vec2 m, out int id, out float provinceDistance, out float ownerDistance,
                           out vec3 terrain, out bool sea, out float coastDistance) {
            vec2 p = m - 0.5;
            vec2 c = floor(p + 0.5);
            vec2 d = p - c;
            vec3 wx = vec3(0.5 * (0.5 - d.x) * (0.5 - d.x), 0.75 - d.x * d.x, 0.5 * (0.5 + d.x) * (0.5 + d.x));
            vec3 wy = vec3(0.5 * (0.5 - d.y) * (0.5 - d.y), 0.75 - d.y * d.y, 0.5 * (0.5 + d.y) * (0.5 + d.y));
            vec3 gx = vec3(d.x - 0.5, -2.0 * d.x, 0.5 + d.x);
            vec3 gy = vec3(d.y - 0.5, -2.0 * d.y, 0.5 + d.y);

            int ids[9], owners[9], wet[9];
            float w[9];
            vec2 dw[9];
            vec3 colours[9];
            for (int j = 0; j < 3; j++) {
                for (int i = 0; i < 3; i++) {
                    int k = j * 3 + i;
                    vec2 t = c + vec2(float(i - 1), float(j - 1)) + 0.5;
                    ids[k] = idAt(t);
                    owners[k] = ownerOf(ids[k]);
                    wet[k] = isWater(ids[k]) ? 1 : 0;
                    w[k] = wx[i] * wy[j];
                    dw[k] = vec2(gx[i] * wy[j], wx[i] * gy[j]);
                    ivec2 texel = ivec2(int(mod(floor(t.x), uMapSize.x)), clamp(int(floor(t.y)), 0, int(uMapSize.y) - 1));
                    colours[k] = texelFetch(uTerrain, texel, 0).rgb;
                }
            }

            int top, topOwner, topWet;
            strongest(ids, w, dw, top, provinceDistance);
            strongest(owners, w, dw, topOwner, ownerDistance);
            strongest(wet, w, dw, topWet, coastDistance);
            id = ids[top];
            sea = wet[topWet] == 1;

            vec3 sum = vec3(0.0);
            float total = 0.0;
            for (int k = 0; k < 9; k++) {
                if (wet[k] != wet[topWet]) continue;
                sum += colours[k] * w[k];
                total += w[k];
            }
            terrain = sum / max(total, 1e-4);
        }

        // ---- national borders

        // Zoomed out, the distance in screen pixels to the nearest land of another owner, looking up to 6 pixels away
        // (1e6 beyond), so the borders and their bands are smooth there too.
        float ownerDistanceNear(vec2 m, int owner) {
            for (int k = 1; k <= 6; k++) {
                float off = float(k) / uZoom;
                if (ownerOf(idAt(m + vec2(off, 0.0))) != owner || ownerOf(idAt(m - vec2(off, 0.0))) != owner
                    || ownerOf(idAt(m + vec2(0.0, off))) != owner || ownerOf(idAt(m - vec2(0.0, off))) != owner)
                    return float(k) - 0.5;
            }
            return 1e6;
        }

        // The border of a nation: a dark line, and inside it a band of the nation's colour that fades inwards, so each
        // country reads as a shape (on maps without nation colours, a shadow instead). Occupied provinces are striped
        // with the occupier's colour.
        vec3 nationBorder(vec3 col, int id, bool sea, float ownerDistance, vec2 m) {
            vec4 nation = texelFetch(uNation, slot(id), 0);
            if (nation.a > 0.5) {
                // The band is about a map pixel and a half wide, kept between 6 and 16 screen pixels.
                float mapDistance = texture(uBorderDistance, m / uMapSize).r * 255.0 / BORDER_STEP + 0.5;
                float band = 1.0 - smoothstep(0.0, clamp(1.6 * uZoom, 6.0, 16.0), mapDistance * uZoom);
                col = uNationBand > 0.0 ? mix(col, nation.rgb * uBandShade, band * uNationBand) : col * (1.0 - 0.22 * band);
                vec4 occupier = texelFetch(uOccupier, slot(id), 0);
                if (occupier.a > 0.5 && !sea) {
                    // Stripes a constant width on screen, fixed to the map so they don't swim as it pans.
                    float stripe = smoothstep(0.4, 0.6, abs(fract((m.x + m.y) * uZoom / 14.0) * 2.0 - 1.0));
                    col = mix(col, occupier.rgb, stripe * 0.65);
                }
            }
            return mix(col, vec3(0.05, 0.03, 0.02), lineCoverage(ownerDistance, uZoom >= uSmoothZoom ? 1.4 : 0.6) * 0.9);
        }

        void main() {
            vec2 frag = vec2(gl_FragCoord.x, uScreen.y * uPixelScale - gl_FragCoord.y) / uPixelScale;
            vec2 m = uCenter + (frag - uScreen * 0.5) / uZoom;
            if (m.y < 0.0 || m.y >= uMapSize.y) { FragColor = vec4(0.05, 0.07, 0.1, 1.0); return; }

            vec3 col = texture(uTerrain, m / uMapSize).rgb;
            int id;
            float provinceLine, ownerDistance;
            bool sea;
            if (uZoom >= uSmoothZoom) {
                float provinceDistance, coastDistance;
                smoothRegions(m, id, provinceDistance, ownerDistance, col, sea, coastDistance);
                provinceLine = lineCoverage(provinceDistance, 0.6);
                // Shallows along the coast, about a map pixel wide, follow the smooth coastline.
                if (sea) col *= 1.0 + 0.22 * (1.0 - smoothstep(0.0, 1.2 * uZoom, coastDistance));
            } else {
                // Zoomed out a map pixel is smaller than a screen pixel: compare with the next screen pixel.
                id = idAt(m);
                float px = 1.0 / uZoom;
                int idR = idAt(m + vec2(px, 0.0));
                int idD = idAt(m + vec2(0.0, px));
                provinceLine = (id != idR || id != idD) ? 1.0 : 0.0;
                ownerDistance = ownerDistanceNear(m, ownerOf(id));
                sea = isWater(id);
                if (sea && (!isWater(idR) || !isWater(idD))) col *= 1.22;
            }
            col = sea ? waterDetail(m, col) : landDetail(m, col);

            vec4 pc = texelFetch(uProvColor, slot(id), 0);
            col = mix(col, pc.rgb, pc.a);
            col = mix(col, vec3(0.08, 0.08, 0.08), provinceLine * uProvinceBorders);
            col = nationBorder(col, id, sea, ownerDistance, m);

            if (id == uSelected) col = mix(col, vec3(1.0, 1.0, 0.85), 0.35);
            else if (id == uHover) col = mix(col, vec3(1.0), 0.12);
            // A soft vignette towards the screen edges.
            vec2 v = frag / uScreen - 0.5;
            col *= 1.0 - 0.35 * dot(v, v);
            FragColor = vec4(col, 1.0);
        }
        """;

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _vao, _vbo;
    private readonly WorldMap _map;
    private readonly Texture _ids, _terrain, _detail, _provColor, _provOwner, _nation, _occupier, _borderDistance;
    private readonly byte[] _colorData = new byte[SlotsX * SlotsY * 4];
    private readonly byte[] _ownerData = new byte[SlotsX * SlotsY * 4];
    private readonly byte[] _nationData = new byte[SlotsX * SlotsY * 4];
    private readonly byte[] _occupierData = new byte[SlotsX * SlotsY * 4];
    private readonly byte[] _borderData;
    /// <summary>Each province's owner when the border distances were last worked out.</summary>
    private int[] _bordersFor = [];

    public MapMode Mode { get; set; } = MapMode.Terrain;
    /// <summary>In resources mode, the only resource shown; null shows each province's main deposit.</summary>
    public ResourceType? ResourceFilter { get; set; }
    /// <summary>Resources the viewer knows; the rest are never drawn.</summary>
    public Func<ResourceType, bool> IsResourceKnown { get; set; } = _ => true;

    /// <summary>Pixel data prepared off the main thread (it takes a moment for 6.5 million pixels).</summary>
    public sealed record Prepared(byte[] Ids, byte[] Terrain, byte[] Detail);

    public static Prepared Prepare(WorldMap map) => new(EncodeIds(map), TerrainColors.Build(map), TerrainColors.BuildDetail(map));

    public unsafe MapRenderer(GL gl, WorldMap map, Prepared prepared)
    {
        if (map.Provinces.Count > SlotsX * SlotsY) throw new InvalidOperationException("Too many provinces for the lookup texture.");
        _gl = gl;
        _map = map;
        _shader = new Shader(gl, VertexSource, FragmentSource);
        _ids = new Texture(gl, map.Width, map.Height, prepared.Ids, smooth: false);
        _terrain = new Texture(gl, map.Width, map.Height, prepared.Terrain, smooth: true, mipmaps: true, repeatX: true);
        _detail = new Texture(gl, map.Width, map.Height, prepared.Detail, smooth: true, mipmaps: true, repeatX: true);
        _provColor = new Texture(gl, SlotsX, SlotsY, _colorData, smooth: false);
        _provOwner = new Texture(gl, SlotsX, SlotsY, _ownerData, smooth: false);
        _nation = new Texture(gl, SlotsX, SlotsY, _nationData, smooth: false);
        _occupier = new Texture(gl, SlotsX, SlotsY, _occupierData, smooth: false);
        _borderData = new byte[map.Width * map.Height];
        _borderDistance = new Texture(gl, map.Width, map.Height, _borderData, smooth: true, repeatX: true, singleChannel: true);

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
            _ownerData[o + 1] = p.IsWater ? (byte)255 : (byte)0; // for the coastline
            _ownerData[o + 3] = 255;
            // The owner's colour bands its borders; occupied land is striped with the occupier's (see the shader).
            SetColor(_nationData, o, p.IsOwned ? new Rgba(players[p.OwnerId].Color) : new Rgba(0));
            SetColor(_occupierData, o, p.IsOccupied ? new Rgba(players[p.ControllerId].Color) : new Rgba(0));

            Rgba color = new(0);
            switch (Mode)
            {
                case MapMode.Terrain:
                    // A light tint: the band along the border tells the nations apart, so the land shows through.
                    if (p.IsOwned) color = new Rgba(players[p.OwnerId].Color).WithAlpha(0.2f);
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
                case MapMode.Institutions:
                    if (p.IsClaimable) color = InstitutionColor(p);
                    break;
                case MapMode.Resources:
                    if (p.IsClaimable) color = DepositColor(p);
                    break;
            }
            SetColor(_colorData, o, color);
        }
        _provColor.Update(_colorData);
        _provOwner.Update(_ownerData);
        _nation.Update(_nationData);
        _occupier.Update(_occupierData);
        var owners = _map.Provinces.Select(p => p.IsWater ? Water : p.OwnerId).ToArray();
        if (!owners.SequenceEqual(_bordersFor))
        {
            _bordersFor = owners;
            BuildBorderDistances(owners);
            _borderDistance.Update(_borderData);
        }
    }

    /// <summary>Steps in the border distance texture per map pixel (as BORDER_STEP in the shader).</summary>
    private const int BorderStep = 40;

    /// <summary>Owner given to water when working out border distances: coasts are not borders.</summary>
    private const int Water = int.MinValue;

    /// <summary>
    /// Works out each map pixel's distance to the nearest border between land of different owners, in whole map
    /// pixels up to 5 (<see cref="BorderStep"/> per pixel, 255 beyond), for the bands of colour along national borders.
    /// Coasts are not borders. Grows outwards a pixel at a time from the pixels on a border.
    /// </summary>
    /// <param name="owners">Each province's owner (-1 for nobody), <see cref="Water"/> for the sea and lakes.</param>
    private void BuildBorderDistances(int[] owners)
    {
        int w = _map.Width, h = _map.Height;
        var ids = _map.ProvinceIds;
        var d = _borderData;
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x, own = owners[ids[i]];
                bool Differs(int j) => owners[ids[j]] is var other && other != Water && other != own;
                bool edge = own != Water && (Differs(y * w + (x + 1) % w) || Differs(y * w + (x + w - 1) % w)
                                         || (y > 0 && Differs(i - w)) || (y < h - 1 && Differs(i + w)));
                d[i] = edge ? (byte)0 : (byte)255;
            }
        });
        for (int layer = 1; layer <= 5; layer++)
        {
            byte previous = (byte)((layer - 1) * BorderStep), current = (byte)(layer * BorderStep);
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (d[i] != 255) continue;
                    bool next = d[y * w + (x + 1) % w] == previous || d[y * w + (x + w - 1) % w] == previous
                                || (y > 0 && d[i - w] == previous) || (y < h - 1 && d[i + w] == previous);
                    if (next) d[i] = current;
                }
            });
        }
    }

    private static void SetColor(byte[] data, int offset, Rgba color)
    {
        data[offset] = color.R;
        data[offset + 1] = color.G;
        data[offset + 2] = color.B;
        data[offset + 3] = color.A;
    }

    /// <summary>Yellow for sparse, deep red for dense (log scale, 0.01 to 100 citizens per km²).</summary>
    private static Rgba PopulationColor(double density)
    {
        float t = (float)Math.Clamp((Math.Log10(Math.Max(density, 0.01)) + 2) / 4, 0, 1);
        byte r = (byte)(255 - 90 * t), g = (byte)(230 * (1 - t)), b = (byte)(60 * (1 - t));
        return new Rgba(0xD0000000u | ((uint)r << 16) | ((uint)g << 8) | b);
    }

    /// <summary>
    /// Resources mode: the colour of the province's richest remaining deposit the viewer knows, or with a filter, that
    /// resource brighter the more is left (log scale). Grey where there is nothing.
    /// </summary>
    private Rgba DepositColor(Province p)
    {
        var none = new Rgba(0xFF606060).WithAlpha(0.6f);
        if (ResourceFilter is ResourceType only && IsResourceKnown(only))
        {
            if (!p.HasDeposit(only)) return none;
            float t = (float)Math.Clamp(Math.Log10(p.Reserves[(int)only]) / 5.5, 0.25, 1);
            return ResourceColor(only).WithAlpha(0.35f + 0.6f * t);
        }
        var main = Resources.Deposits.Where(r => p.HasDeposit(r) && IsResourceKnown(r)).OrderByDescending(r => p.Deposits[(int)r] / Richness(r)).Cast<ResourceType?>().FirstOrDefault();
        return main is ResourceType r ? ResourceColor(r).WithAlpha(0.9f) : none;
    }

    /// <summary>
    /// Institutions mode: the colour of the newest institution that has reached the province, strongest in
    /// cities; grey where none has, fainter where nobody lives.
    /// </summary>
    private static Rgba InstitutionColor(Province p)
    {
        var newest = Institutions.All.LastOrDefault(p.Institutions.Contains);
        if (!p.Institutions.Contains(newest)) return new Rgba(0xFF606060).WithAlpha(p.Population > 0 ? 0.6f : 0.35f);
        return new Rgba(newest.Info().Color).WithAlpha(p.CityId.HasValue ? 1f : 0.92f);
    }

    /// <summary>Gold and silver pockets are small, so they compare by a smaller yardstick.</summary>
    private static double Richness(ResourceType r) => r is ResourceType.Gold or ResourceType.Silver ? 1 : 4;

    /// <summary>Colour of each deposit resource on the map and in its legend.</summary>
    public static Rgba ResourceColor(ResourceType r) => new(r switch
    {
        ResourceType.Coal => 0xFF3A3A3Au,
        ResourceType.Iron => 0xFFB5523Bu,
        ResourceType.Copper => 0xFFE08A3Cu,
        ResourceType.Silicon => 0xFF9FC7E8u,
        ResourceType.Oil => 0xFF6A3E8Cu,
        ResourceType.Aluminium => 0xFFC8CCD4u,
        ResourceType.Rubber => 0xFF3FA34Du,
        ResourceType.Gold => 0xFFF2CC30u,
        ResourceType.Silver => 0xFFF4F4F4u,
        _ => 0xFF808080u,
    });

    /// <summary>Red at 0, yellow at 0.5, green at 1 (mood and fertility modes).</summary>
    private static Rgba ScaleColor(double value)
    {
        float t = (float)Math.Clamp(value, 0, 1);
        float r = t < 0.5f ? 215 : 215 - 300 * (t - 0.5f);
        float g = t < 0.5f ? 60 + 300 * t : 210 - 60 * (t - 0.5f);
        return new Rgba(0xD0000000u | ((uint)r << 16) | ((uint)g << 8) | 50u);
    }

    /// <param name="pixelScale">Framebuffer pixels per window pixel (high-DPI screens).</param>
    /// <param name="time">Seconds of real time, to move the ripples on the water.</param>
    public void Draw(Camera camera, int selectedProvince, int hoverProvince, float pixelScale, double time)
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
        // The terrain map keeps province lines faint, so the land itself stands out.
        _shader.Set("uProvinceBorders", Math.Clamp((camera.Zoom - 0.8f) / 2.5f, 0f, Mode == MapMode.Terrain ? 0.15f : 0.45f));
        _ids.Bind(0);
        _shader.Set("uIds", 0);
        _terrain.Bind(1);
        _shader.Set("uTerrain", 1);
        _provColor.Bind(2);
        _shader.Set("uProvColor", 2);
        _provOwner.Bind(3);
        _shader.Set("uProvOwner", 3);
        _detail.Bind(4);
        _shader.Set("uDetail", 4);
        _shader.Set("uTime", (float)(time % 10000));
        _nation.Bind(5);
        _shader.Set("uNation", 5);
        _occupier.Bind(6);
        _shader.Set("uOccupier", 6);
        _borderDistance.Bind(7);
        _shader.Set("uBorderDistance", 7);
        // Nations' colours band their borders on the terrain and political maps (darker there, over the filled
        // colour); the data maps keep a plain shadow so their own colours read true.
        var (band, shade) = Mode switch
        {
            MapMode.Terrain => (0.6f, 1f),
            MapMode.Political => (0.8f, 0.5f),
            _ => (0f, 1f),
        };
        _shader.Set("uNationBand", band);
        _shader.Set("uBandShade", shade);
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    public void Dispose()
    {
        _shader.Dispose();
        _ids.Dispose();
        _terrain.Dispose();
        _detail.Dispose();
        _provColor.Dispose();
        _provOwner.Dispose();
        _nation.Dispose();
        _occupier.Dispose();
        _borderDistance.Dispose();
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
    }
}
