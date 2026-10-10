// Additive cyan fresnel rim, drawn over the Warden's body (same mesh, after it), so his
// silhouette edges glow and he reads against dark floors and crowds. No vertex offset, so
// none of the seams the inverted-hull outline had.
Shader "WardenZero/RimGlow"
{
    Properties
    {
        [HDR] _BaseColor ("Colour", Color) = (0.3, 1.2, 1.8, 1)
        _Power ("Power", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        ZTest LEqual
        ZWrite Off
        Blend One One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Power;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(world);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float rim = 1 - saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                return half4(_BaseColor.rgb * pow(rim, _Power), 0);
            }
            ENDHLSL
        }
    }
}
