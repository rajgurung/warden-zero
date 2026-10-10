// Flat cyan silhouette of the Warden wherever scenery (walls, trees) hides him.
// Drawn just before his body with ZTest Greater and no depth write, so it only appears
// behind opaque geometry; the body then draws normally where he is visible. Flat colour,
// no vertex offset, so no seams (unlike the inverted-hull outline).
Shader "WardenZero/XRaySilhouette"
{
    Properties
    {
        [HDR] _BaseColor ("Colour", Color) = (0.3, 1.2, 1.8, 0.55)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        ZTest Greater
        ZWrite Off
        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            float4 vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half4 frag() : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
