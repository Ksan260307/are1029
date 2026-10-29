// Opaque, vertex-coloured, toon-lit. Used by every Blender model, debris and the soldiers.
// _Color / _Flash / _Emission are per-instance so tinted or flashing units still batch.
Shader "NumberRoad/Toon"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Flash ("Hit Flash", Range(0, 1)) = 0
        _Emission ("Unlit Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "NumberRoadCommon.cginc"

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Flash)
                UNITY_DEFINE_INSTANCED_PROP(float, _Emission)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 normal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(o.worldPos);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = NR_VertexColor(v.color) * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 albedo = i.color.rgb;
                float3 color = NR_Lighting(albedo, normalize(i.normal), i.worldPos);
                color = lerp(color, albedo, UNITY_ACCESS_INSTANCED_PROP(Props, _Emission));
                color = lerp(color, float3(1, 1, 1), UNITY_ACCESS_INSTANCED_PROP(Props, _Flash));
                return fixed4(NR_ApplyFog(color, i.worldPos), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
