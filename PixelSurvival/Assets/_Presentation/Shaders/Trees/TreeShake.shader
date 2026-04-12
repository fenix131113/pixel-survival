Shader "Custom/TreeShake"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _SwaySpeed ("Sway Speed", Float) = 1.0
        _SwayAmount ("Sway Amount", Float) = 0.04
        _BendStart ("Bend Start (UV Y)", Float) = 0.0
        _BendEnd ("Bend End (UV Y)", Float) = 1.0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
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
                float _SwaySpeed;
                float _SwayAmount;
                float _BendStart;
                float _BendEnd;
            CBUFFER_END

            // Функция хэша для определения направления (влево или вправо)
            float GetDirection(float3 worldPos) {
                float h = frac(sin(dot(worldPos.xz, float2(12.9898, 78.233))) * 43758.5453);
                return h > 0.5 ? 1.0 : -1.0;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                // 1. Получаем мировую позицию (как в траве)
                float3 worldPos = GetObjectToWorldMatrix()._m03_m13_m23;
                
                // 2. Логика маски (используем UV, так как дерево высокое и OS может плавать)
                float mask = smoothstep(_BendStart, _BendEnd, input.uv.y);
                float anchorMask = mask * mask;

                // 3. Расчет асинхронной фазы (как в траве)
                float noise = (worldPos.x * 1.37 + worldPos.z * 0.89);
                float phase = (_Time.y * _SwaySpeed) + noise;
                
                // Направление качания: каждое дерево выбирает свою сторону
                float dir = GetDirection(worldPos);
                
                float swayOffset = sin(phase) * _SwayAmount * dir;

                // 4. Применяем смещение
                float3 displacedPos = input.positionOS;
                displacedPos.x += swayOffset * anchorMask;

                output.positionCS = TransformObjectToHClip(displacedPos);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (tex.a < 0.1) discard;
                return tex * input.color;
            }
            ENDHLSL
        }
    }
}