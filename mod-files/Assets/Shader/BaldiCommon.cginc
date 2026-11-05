// Shared pieces for the hand-written replacements of Baldi's Shader Graph shaders.
// The originals did not survive extraction (only their property lists did), so
// these are rebuilt for the built-in render pipeline from those properties and
// from how the game's code drives them.
#ifndef BALDI_COMMON_INCLUDED
#define BALDI_COMMON_INCLUDED

#include "UnityCG.cginc"

// The game's light map: one texel per 10-unit tile, at the tile's grid position
// (CoreGameManager.UpdateLighting writes SetPixel(tile.x, tile.z, colour)).
sampler2D _LightMap;
float4 _LightMap_TexelSize;

fixed3 BaldiTile(float2 tile)
{
    return tex2Dlod(_LightMap, float4((tile + 0.5) * _LightMap_TexelSize.xy, 0, 0)).rgb;
}

// Light for a point in the world. Surfaces on a tile edge (walls) are nudged a
// little into the tile they face, so a wall takes its own tile's light. Floors,
// ceilings and the cutouts blend smoothly from tile to tile instead of in
// blocks, and every tile is brightest under its ceiling light, fading towards
// its edges, in a warm old-bulb colour (Jen's son: less cold, nicer lighting).
// Livelier (Jen's son again, same colours): brighter pools under the lamps,
// dark spots lifted a little so nothing goes muddy, walls a touch lighter or
// darker by which way they face so halls have depth, and the warm tint softer.
// The lighting setting (Options > Lighting, set by VR/LightingSetting.cs):
// 0 Normal - the game's own light, nothing added; 1 Dynamic - lamp pools,
// window-lit walls, depth; 2 Filmmaker - a moodier, cinematic grade on top.
// 3 to 16 are the extra looks (Jen's son): Snow day, Halloween, Christmas,
// Space, Alarm, Candle, Hospital, Sunrise, Infrared, Comic, Ghost, Lava,
// Arcade CRT and Eclipse, each the Dynamic light turned into its own look.
float _BaldiLightMode;

float BaldiHash(float2 at)
{
    return frac(sin(dot(at, float2(12.9898, 78.233))) * 43758.5453);
}

fixed BaldiLuma(fixed3 c)
{
    return dot(c, fixed3(0.299, 0.587, 0.114));
}

// The extra looks, made from the Dynamic light (lit), the game's own light
// for the room (light) and how near the room's lamp the point is (near).
fixed3 BaldiLook(fixed3 lit, fixed3 light, float near, float hasSurface, float3 worldPos, float3 worldNormal, float2 p, float2 fromLamp, float height, float shine)
{
    int mode = (int)round(_BaldiLightMode);
    float t = _Time.y;
    float2 tile = floor(p / 10.0);
    float sprite = 1.0 - hasSurface;
    fixed3 o = lit;
    if (mode == 3)
    {
        // Snow day: bright, cold white light, the halls a pale blue.
        o = lit * fixed3(0.93, 0.99, 1.13) * 1.15 + light * fixed3(0.05, 0.06, 0.08) * saturate(abs(worldNormal.x) + abs(worldNormal.z));
    }
    else if (mode == 4)
    {
        // Halloween: orange and purple, the lamps flickering like pumpkins.
        fixed3 orange = fixed3(1.3, 0.62, 0.18);
        fixed3 purple = fixed3(0.72, 0.42, 1.15);
        float mixAt = 0.5 + 0.5 * sin(worldPos.x * 0.07 + worldPos.z * 0.05);
        float flick = 0.8 + 0.2 * sin(t * 9.0 + BaldiHash(tile) * 40.0);
        o = lit * 0.62 * lerp(orange, purple, mixAt) + light * orange * near * near * 0.35 * flick;
    }
    else if (mode == 5)
    {
        // Christmas: warm light, with red and green twinkling up by the lamps.
        float h = BaldiHash(tile);
        fixed3 twinkle = frac(h * 7.0 + floor(t * 1.5 + h * 3.0) * 0.5) < 0.5 ? fixed3(1.0, 0.12, 0.1) : fixed3(0.12, 1.0, 0.25);
        float blink = 0.6 + 0.4 * sin(t * 3.0 + h * 20.0);
        o = lit * fixed3(1.08, 1.0, 0.86) + light * twinkle * (0.06 + 0.22 * height * height) * blink;
    }
    else if (mode == 6)
    {
        // Space: nearly dark and purple, with tiny twinkling stars on everything.
        float h = BaldiHash(floor(worldPos.xz * 1.6) + floor(worldPos.y * 1.6) * 17.0);
        float star = step(0.985, h) * (0.6 + 0.4 * sin(t * 3.0 + h * 60.0)) * hasSurface;
        o = lit * fixed3(0.32, 0.26, 0.5) + fixed3(0.9, 0.9, 1.0) * star * 2.0;
    }
    else if (mode == 7)
    {
        // Alarm: the normal light, with a red beam spinning round each lamp.
        float beam = pow(saturate(cos(atan2(fromLamp.y, fromLamp.x) - t * 4.0)), 8.0);
        o = lit * 0.8 + light * fixed3(1.0, 0.06, 0.03) * beam * 0.9;
    }
    else if (mode == 8)
    {
        // Candle: small, warm, wobbly pools of light, deep dark between.
        float h = BaldiHash(tile);
        float flick = 0.85 + 0.15 * sin(t * 11.0 + h * 30.0) * sin(t * 7.3 + h * 11.0);
        o = light * fixed3(1.25, 0.75, 0.35) * (0.06 + 1.15 * pow(near, 2.5) * flick) * (1.0 + shine);
    }
    else if (mode == 9)
    {
        // Hospital: harsh, cold, greenish strip lights that now and then flicker.
        float flick = 0.97 + 0.06 * step(0.96, BaldiHash(float2(floor(t * 12.0), 1.0)));
        o = lerp(BaldiLuma(lit).xxx, lit, 0.4) * fixed3(0.88, 1.06, 0.95) * 1.14 * flick;
    }
    else if (mode == 10)
    {
        // Sunrise: soft pink and peach, glowing in from the windows.
        o = lit * lerp(fixed3(1.12, 0.93, 0.84), fixed3(1.08, 0.85, 1.0), height) * 1.04 + light * fixed3(0.22, 0.11, 0.09) * saturate(worldNormal.x);
    }
    else if (mode == 11)
    {
        // Infrared: characters and things hot, the school cold (the heat
        // colours are put on in BaldiSaturate).
        o = sprite > 0.5 ? lit * 2.2 : lit * (0.35 + 0.4 * near);
    }
    else if (mode == 12)
    {
        // Comic: light in three flat steps, with hard dark shadows.
        fixed l = BaldiLuma(lit);
        fixed q = max(floor(l * 3.0 + 0.5) / 3.0, 0.12);
        o = lit / max(l, 0.01) * q;
    }
    else if (mode == 13)
    {
        // Ghost: pale, washed-out light, and the characters glowing.
        o = lerp(BaldiLuma(lit).xxx, lit, 0.35) * fixed3(0.85, 1.0, 1.1) * 0.85 + sprite * fixed3(0.25, 0.36, 0.4) * (0.8 + 0.2 * sin(t * 2.0));
    }
    else if (mode == 14)
    {
        // Lava: the light comes up orange from the floor, not down from the ceiling.
        float up = 1.0 - height;
        float pulse = 0.9 + 0.1 * sin(t * 2.0 + worldPos.x * 0.3 + worldPos.z * 0.2);
        o = light * fixed3(1.5, 0.55, 0.15) * (0.18 + 1.1 * up * up) * pulse;
        o += fixed3(0.5, 0.15, 0.02) * step(0.5, worldNormal.y) * pulse;
    }
    else if (mode == 15)
    {
        // Arcade CRT: lines across the picture and darker, rounded edges, like an old TV.
        float4 clip = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
        float2 sp = clip.xy / clip.w;
        float row = (sp.y * 0.5 + 0.5) * _ScreenParams.y;
        o = lit * 1.08 * (0.8 + 0.2 * sin(row * 1.5708)) * (1.0 - 0.3 * dot(sp, sp));
    }
    else if (mode == 16)
    {
        // Eclipse: almost dark, with a thin ring of light round the edge of what you see.
        float4 clip = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
        float2 sp = clip.xy / clip.w;
        float r = length(sp);
        o = lit * 0.12 + light * fixed3(1.2, 0.9, 0.6) * exp(-pow((r - 0.85) * 7.0, 2.0)) * 0.8;
    }
    return o * (1.0 + 0.08 * (1.0 - o)) / (1.0 + 0.05 * o);
}

fixed3 BaldiLight(float3 worldPos, float3 worldNormal)
{
    float2 p = worldPos.xz + worldNormal.xz * 1.0;
    fixed3 light;
    if (abs(worldNormal.x) + abs(worldNormal.z) > 0.5)
    {
        // A wall: its own room's light only, so none leaks through it.
        light = BaldiTile(floor(p / 10.0));
    }
    else
    {
        float2 g = p / 10.0 - 0.5;
        float2 corner = floor(g);
        float2 f = smoothstep(0.0, 1.0, frac(g));
        fixed3 near = lerp(BaldiTile(corner), BaldiTile(corner + float2(1, 0)), f.x);
        fixed3 far = lerp(BaldiTile(corner + float2(0, 1)), BaldiTile(corner + float2(1, 1)), f.x);
        light = lerp(near, far, f.y);
    }
    float2 fromLamp = frac(p / 10.0) - 0.5;
    // Much brighter right under each lamp, with a soft glow round it.
    float near = saturate(1.0 - dot(fromLamp, fromLamp) * 3.0);
    float pool = 0.84 + 0.30 * near + 0.18 * near * near;
    // Walls lit a little differently by which way they face, warmer one way
    // and cooler the other, like light coming in from windows.
    float facing = 1.0 + 0.09 * worldNormal.x - 0.06 * worldNormal.z;
    fixed3 tint = lerp(fixed3(0.98, 1.0, 1.04), fixed3(1.08, 1.0, 0.88), saturate(0.5 + 0.5 * worldNormal.x + 0.5 * abs(worldNormal.y)));
    // Walls brighter up near the lamps and a little darker down by the
    // floor, so rooms have depth.
    float wall = 1.0 - abs(worldNormal.y);
    float height = saturate(worldPos.y / 10.0);
    float upWall = lerp(1.0, 0.86 + 0.24 * height, wall);
    if (_BaldiLightMode < 0.5) return light;
    // A slightly shiny, metallic finish (Jen's son): a soft highlight from
    // the room's ceiling lamp and a sheen at glancing angles, in the
    // surface's own colour (it brightens, never changes the colour).
    // Flat pictures (characters, items) have no surface to shine.
    float hasSurface = step(0.5, length(worldNormal));
    float3 n = worldNormal / max(length(worldNormal), 0.001);
    float2 lampAt = (floor(p / 10.0) + 0.5) * 10.0;
    float3 toLamp = normalize(float3(lampAt.x, 9.5, lampAt.y) - worldPos);
    float3 toEye = normalize(_WorldSpaceCameraPos - worldPos);
    float glint = pow(saturate(dot(n, normalize(toLamp + toEye))), 20.0) * (0.5 + 0.5 * near);
    float sheen = pow(1.0 - saturate(dot(n, toEye)), 4.0);
    float shine = hasSurface * (0.32 * glint + 0.10 * sheen);
    if (_BaldiLightMode > 1.5 && _BaldiLightMode < 2.5)
    {
        // Filmmaker: deeper darks between the lamps and brighter pools right
        // under them, like a lit film set; darker overall, with warm,
        // yellowy lamps.
        float film = (0.70 + 0.42 * near + 0.34 * near * near) * 0.86;
        fixed3 set = (light * 0.88 + 0.03) * film * facing * tint * fixed3(1.12, 0.97, 0.68) * lerp(1.0, 0.8 + 0.36 * height, wall) * 1.08;
        // A warm orange glow round each lamp, as if the light hangs in the air.
        set += light * fixed3(0.16, 0.08, 0.02) * near * near;
        return set * (1.0 + 0.1 * (1.0 - set)) / (1.0 + 0.04 * set) * (1.0 + 0.9 * shine);
    }
    fixed3 lit = (light * 0.9 + 0.08) * pool * facing * tint * upWall * 1.06;
    if (_BaldiLightMode > 2.5) return BaldiLook(lit, light, near, hasSurface, worldPos, worldNormal, p, fromLamp, height, shine);
    // A gentle film-like curve: richer darks and soft, unblown highlights.
    return lit * (1.0 + 0.12 * (1.0 - lit)) / (1.0 + 0.06 * lit) * (1.0 + shine);
}

// Richer colour (Jen's son: a bit more saturated). The amount is global
// (_VRSaturation, set by VRRig); a material's _VRSat scales how much of it
// applies (0 for the desks and tables, which stay as they are).
float _VRSaturation;
fixed3 BaldiSaturate(fixed3 c, float amount)
{
    float boost = lerp(1.0, max(_VRSaturation, 1.0), amount);
    fixed luma = dot(c, fixed3(0.299, 0.587, 0.114));
    c = saturate(lerp(luma.xxx, c, boost));
    int mode = (int)round(_BaldiLightMode);
    if (mode == 11)
    {
        // Infrared: brightness as heat, blue for cold up to yellow for hot.
        fixed h = saturate(dot(c, fixed3(0.299, 0.587, 0.114)) * 1.3);
        c = h < 0.33 ? lerp(fixed3(0.02, 0.02, 0.25), fixed3(0.45, 0.05, 0.6), h * 3.0)
          : h < 0.66 ? lerp(fixed3(0.45, 0.05, 0.6), fixed3(1.0, 0.2, 0.05), h * 3.0 - 1.0)
          : lerp(fixed3(1.0, 0.2, 0.05), fixed3(1.0, 0.95, 0.3), h * 3.0 - 2.0);
    }
    else if (mode == 12)
    {
        // Comic: bold colour.
        luma = dot(c, fixed3(0.299, 0.587, 0.114));
        c = lerp(luma.xxx, c, 1.5);
    }
    else if (mode == 9 || mode == 13)
    {
        // Hospital and Ghost: washed out.
        luma = dot(c, fixed3(0.299, 0.587, 0.114));
        c = lerp(luma.xxx, c, 0.55);
    }
    if (mode == 2)
    {
        // Filmmaker's grade: a little less colour, cool shadows, warm
        // highlights and a soft S-curve of contrast.
        luma = dot(c, fixed3(0.299, 0.587, 0.114));
        c = lerp(luma.xxx, c, 0.86);
        c *= lerp(fixed3(0.90, 0.97, 1.08), fixed3(1.07, 1.0, 0.88), saturate(luma * 1.4));
        c = lerp(c, c * c * (3.0 - 2.0 * c), 0.4);
    }
    return saturate(c);
}

#endif
