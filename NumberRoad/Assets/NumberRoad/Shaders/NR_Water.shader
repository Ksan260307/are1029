// Stylised animated water for the moat on both sides of the road.
// Waves are computed from world position, so the plane can follow the camera without sliding.
Shader "NumberRoad/Water"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.13, 0.47, 0.82, 1)
        _Shallow ("Shallow", Color) = (0.36, 0.74, 0.97, 1)
        _Highlight ("Highlight", Color) = (0.85, 0.95, 1, 1)
        _WaveHeight ("Wave Height", Float) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-10" "IgnoreProjector" = "True" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "NumberRoadCommon.cginc"

            fixed4 _Deep;
            fixed4 _Shallow;
            fixed4 _Highlight;
            float _WaveHeight;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float wave : TEXCOORD1;
            };

            float Waves(float2 p, float t)
            {
                return sin(p.x * 0.7 + t * 1.3) * 0.5
                     + sin(p.y * 0.45 - t * 1.1) * 0.6
                     + sin((p.x + p.y) * 1.3 + t * 2.1) * 0.25;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float w = Waves(wp.xz, _Time.y);
                wp.y += w * _WaveHeight;
                o.worldPos = wp;
                o.wave = w;
                o.pos = UnityWorldToClipPos(wp);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.worldPos.xz;
                float t = _Time.y;
                float pattern = sin(p.x * 2.1 + sin(p.y * 0.9 + t) * 1.5 + t * 0.8) * sin(p.y * 1.7 - t * 0.6);
                float3 c = lerp(_Deep.rgb, _Shallow.rgb, saturate(i.wave * 0.35 + 0.5));
                c = lerp(c, _Highlight.rgb, smoothstep(0.82, 0.95, pattern) * 0.55);
                return fixed4(NR_ApplyFog(c, i.worldPos), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
