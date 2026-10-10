// Inverted-hull outline for the 3D Warden: the mesh drawn again, pushed out along its
// normals with front faces culled, in an HDR cyan that the bloom picks up. Used as a second
// material on the same mesh, so it follows the skinned pose.
Shader "WardenZero/Outline3D"
{
    Properties
    {
        [HDR] _BaseColor ("Colour", Color) = (0.3, 1.6, 2.4, 1)
        _Width ("Width (m)", Float) = 0.018
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+9" }
        Cull Front
        ZWrite On

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Width;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 vert(Attributes i) : SV_POSITION
            {
                float3 world = TransformObjectToWorld(i.positionOS.xyz);
                float3 n = normalize(TransformObjectToWorldNormal(i.normalOS));
                return TransformWorldToHClip(world + n * _Width);
            }

            half4 frag() : SV_Target
            {
                return half4(_BaseColor.rgb, 1);
            }
            ENDHLSL
        }
    }
}
