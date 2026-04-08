Shader "GameAssembly/Sprites/Grass Sprite Sway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        _SwaySpeed ("Sway Speed", Float) = 1.5
        _SwayAmount ("Sway Amount", Float) = 0.03
        _BendStart ("Bend Start", Float) = 0.0
        _BendEnd ("Bend End", Float) = 0.5
        [PerRendererData] _ImpactForce ("Impact Force", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "SpriteUnlit"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _RendererColor;
                float _SwaySpeed;
                float _SwayAmount;
                float _BendStart;
                float _BendEnd;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(GrassProps)
                UNITY_DEFINE_INSTANCED_PROP(float, _ImpactForce)
            UNITY_INSTANCING_BUFFER_END(GrassProps)

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                float anchorMask = smoothstep(_BendStart, _BendEnd, input.positionOS.y);
                float phase = (_Time.y * _SwaySpeed) + dot(unity_ObjectToWorld._m03_m13, float2(0.31, 0.17));
                float swayOffset = sin(phase) * _SwayAmount;
                float impactOffset = UNITY_ACCESS_INSTANCED_PROP(GrassProps, _ImpactForce);

                input.positionOS.x += (swayOffset + impactOffset) * anchorMask;

                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color * _RendererColor * unity_SpriteColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            }
            ENDHLSL
        }
    }
}
