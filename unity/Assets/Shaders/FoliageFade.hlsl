// The behind view's foliage fade (FoliageLit.shader); globals set by CameraFollow.
#ifndef WZ_FOLIAGE_FADE
#define WZ_FOLIAGE_FADE

float4 _WZFade; // x: 1 = on, y/z: the near fade from/to (metres from the lens)
float4 _WZFadeTarget; // xyz: the Warden's chest (world)
float4 _WZFadeSize; // xy: the hole's half width and half height where he stands (metres)

// 4x4 ordered dither threshold in [0, 1), float maths only (fine at shader target 2.0).
float WZBayer2(float2 a)
{
    a = floor(a);
    return frac(a.x / 2 + a.y * a.y * 0.75);
}

float WZBayer4(float2 a)
{
    return WZBayer2(0.5 * a) * 0.25 + WZBayer2(a);
}

// Drops the pixel by an ordered dither as it fades: near the lens, and inside a cone from
// the lens to the Warden's chest (an ellipse around him on screen) for plants in front of
// him. All in world space, so it lines up on every graphics API and render target.
void WZFadeClip(float3 positionWS, float4 positionCS)
{
    if (_WZFade.x <= 0) return;
    float3 eye = _WorldSpaceCameraPos;
    float3 toPixel = positionWS - eye;
    float fade = saturate((_WZFade.z - length(toPixel)) / max(1e-3, _WZFade.z - _WZFade.y));

    float3 axis = _WZFadeTarget.xyz - eye;
    float len = max(length(axis), 1e-3);
    axis /= len;
    float along = dot(toPixel, axis);
    float t = along / len; // 0 at the lens, 1 at his chest
    float3 side = toPixel - axis * along;
    // The screen-up direction around the line; any side axis will do when the line is vertical.
    float3 upRaw = float3(0, 1, 0) - axis * axis.y;
    float upLen = length(upRaw);
    float3 up = upLen > 1e-4 ? upRaw / upLen : float3(0, 0, 1);
    float v = dot(side, up);
    float h = length(side - up * v);
    float r = length(float2(h / _WZFadeSize.x, v / _WZFadeSize.y)) / max(t, 1e-3);
    // Fully clear inside 0.9 of the ellipse, soft out to 1.3; everything nearer than 0.1 m
    // past his chest along the line (plants at his legs project there too), none past 0.3 m.
    float hole = saturate((1.3 - r) / 0.4) * saturate((len - along + 0.3) / 0.4) * step(0, along);

    fade = max(fade, hole);
    clip(WZBayer4(positionCS.xy) + 1.0 / 32 - fade);
}

#endif
