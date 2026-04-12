Shader "GameAssembly/Environment/CloudLightOverlay"
{
   Properties
    {
        [Header(Tiled Shape)]
        _TileSize("Tile Size", Float) = 0.5 
        // НОВЫЙ ПАРАМЕТР: Смещение сетки по X и Z
        _TileOffset("Tile Offset (X, Z)", Vector) = (0, 0, 0, 0)
        _CloudSpeed("Cloud Speed", Vector) = (0.015, 0.008, 0, 0)
        _ZoneScale("Zone Scale", Float) = 5.0 
        _ZoneDensity("Zone Density (Cutoff)", Range(0, 1)) = 0.5
        _ZoneSoftness("Zone Edge Softness", Range(0, 1)) = 0.05

        [Header(Pixel Texture)]
        _PixelTextureDensity("Pixel Texture Density", Float) = 16.0
        _TextureStrength("Texture Strength", Range(0, 1)) = 0.3

        [Header(Lighting and Bloom)]
        [HDR] _LightColor("Light Color (Bloom)", Color) = (2.0, 1.8, 1.5, 1)
        _GlobalOpacity("Global Opacity", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            float _TileSize, _ZoneScale, _ZoneDensity, _ZoneSoftness, _PixelTextureDensity, _TextureStrength, _GlobalOpacity;
            float2 _CloudSpeed, _TileOffset; // Добавили _TileOffset
            float4 _LightColor;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i + float2(0,0)), hash(i + float2(1,0)), f.x),
                            lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 2; ++i) {
                    v += a * noise(p);
                    p = p * 2.1;
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Применяем смещение к мировым координатам ПЕРЕД квантованием
                float2 worldCoord = input.worldPos.xz + _TileOffset;
                float2 movement = _Time.y * _CloudSpeed;

                // Квантуем координаты (создаем сетку тайлов)
                float2 pTiled = floor(worldCoord / _TileSize) * _TileSize + (_TileSize * 0.5);
                
                // Шум зоны (теперь тоже зависит от смещения)
                float2 uvTiled = pTiled / _ZoneScale;
                float zoneNoise = fbm(uvTiled + movement);
                float zoneMask = smoothstep(_ZoneDensity - _ZoneSoftness, _ZoneDensity + _ZoneSoftness, zoneNoise);

                if (zoneMask < 0.001) return half4(0,0,0,0);

                // Внутренняя текстура пикселей
                float2 uvTexture = floor(worldCoord * _PixelTextureDensity) / _PixelTextureDensity;
                float textureNoise = noise(uvTexture * 5.0 + movement * 2.0);

                float finalMask = zoneMask * lerp(1.0, textureNoise, _TextureStrength);

                float3 finalColor = _LightColor.rgb;
                float finalAlpha = finalMask * _GlobalOpacity * _LightColor.a;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}