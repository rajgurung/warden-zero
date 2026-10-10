// Unlit sprite with a thin glowing outline, used for the Warden so he stays readable
// inside a crowd. The outline is found by sampling alpha around each texel; its HDR
// colour feeds the bloom. Based on URP's Sprite-Unlit-Default (same flip/colour handling).
Shader "WardenZero/SpriteOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _OutlineColor ("Outline Colour", Color) = (0.3, 1.6, 2.4, 1)
        _OutlineWidth ("Outline Width (UV)", Float) = 0.012
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        // Premultiplied output so the outline and the sprite blend in one pass.
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half Alpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                float w = _OutlineWidth;
                float d = w * 0.7071;
                half n = max(max(max(Alpha(i.uv + float2(w, 0)), Alpha(i.uv - float2(w, 0))),
                                 max(Alpha(i.uv + float2(0, w)), Alpha(i.uv - float2(0, w)))),
                             max(max(Alpha(i.uv + float2(d, d)), Alpha(i.uv - float2(d, d))),
                                 max(Alpha(i.uv + float2(d, -d)), Alpha(i.uv + float2(-d, d)))));
                // Outline only where the sprite itself is (mostly) transparent; fades with the
                // sprite's own alpha so hurt-blink and death still read.
                half o = saturate(n - c.a) * i.color.a;
                return half4(c.rgb * c.a + _OutlineColor.rgb * o, saturate(c.a + o));
            }
            ENDHLSL
        }
    }
}
