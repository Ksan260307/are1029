#ifndef NUMBERROAD_COMMON_INCLUDED
#define NUMBERROAD_COMMON_INCLUDED

#include "UnityCG.cginc"

// Global lighting / fog values, set once by WorldView.ApplyEnvironment().
// The shaders do their own lighting so they behave the same on every
// platform and render pipeline, and need no light or fog keywords.
float4 _NR_LightDir;       // xyz: normalized direction towards the light
float4 _NR_LightColor;
float4 _NR_SkyAmbient;
float4 _NR_GroundAmbient;
float4 _NR_FogColor;
float4 _NR_FogParams;      // x: fog start distance, y: 1 / (end - start)

inline fixed4 NR_VertexColor(fixed4 c)
{
#ifdef UNITY_COLORSPACE_GAMMA
    return c;
#else
    return fixed4(GammaToLinearSpace(c.rgb), c.a);
#endif
}

inline float NR_FogFactor(float3 worldPos)
{
    float d = distance(_WorldSpaceCameraPos.xyz, worldPos);
    float f = saturate((d - _NR_FogParams.x) * _NR_FogParams.y);
    return f * f * (3.0 - 2.0 * f);
}

inline float3 NR_ApplyFog(float3 color, float3 worldPos)
{
    return lerp(color, _NR_FogColor.rgb, NR_FogFactor(worldPos));
}

// Two-tone "toon" lambert + hemisphere ambient + a soft rim.
inline float3 NR_Lighting(float3 albedo, float3 normal, float3 worldPos)
{
    float ndl = dot(normal, _NR_LightDir.xyz);
    float ramp = smoothstep(-0.05, 0.3, ndl);
    float3 ambient = lerp(_NR_GroundAmbient.rgb, _NR_SkyAmbient.rgb, normal.y * 0.5 + 0.5);
    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);
    float rim = pow(1.0 - saturate(dot(normal, viewDir)), 3.0) * 0.2;
    return albedo * (ambient + _NR_LightColor.rgb * ramp) + rim * _NR_SkyAmbient.rgb;
}

#endif
