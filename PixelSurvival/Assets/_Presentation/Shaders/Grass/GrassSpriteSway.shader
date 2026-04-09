Shader "GameAssembly/Sprites/Grass Sprite Sway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        _SwaySpeed ("Sway Speed", Float) = 1.5
        _SwayAmount ("Sway Amount", Float) = 0.05
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

        Blend SrcAlpha OneMinusSrcAlpha
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

                // Расчет маски: возводим в квадрат, чтобы корень (внизу) был максимально неподвижен
                float mask = smoothstep(_BendStart, _BendEnd, input.positionOS.y);
                float anchorMask = mask * mask; 

                // Получаем мировые координаты объекта для создания уникальной фазы
                float3 worldPos = GetObjectToWorldMatrix()._m03_m13_m23;
                
                // Усиливаем разброс фазы. Числа 1.37 и 0.89 создают эффект псевдо-рандома.
                float noise = (worldPos.x * 1.37 + worldPos.y * 0.89);
                float phase = (_Time.y * _SwaySpeed) + noise;
                
                float swayOffset = sin(phase) * _SwayAmount;
                float impactOffset = UNITY_ACCESS_INSTANCED_PROP(GrassProps, _ImpactForce);

                // Применяем смещение только к X
                input.positionOS.x += (swayOffset + impactOffset) * anchorMask;

                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color * _RendererColor;
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