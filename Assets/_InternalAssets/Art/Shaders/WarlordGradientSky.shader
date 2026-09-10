// Градиентное небо: зенит — горизонт — низ, без физического рассеяния.
//
// Процедурное небо Unity считает рэлеевское рассеяние, и при низком солнце горизонт
// уходит в жёлто-зелёный. Для стилизованной низкополигональной арены это и некрасиво,
// и вредно: небо начинает спорить по цвету с командными цветами игроков.
// Здесь три опорных цвета и явная резкость перехода — что задали, то и видно.
Shader "Warlord/Gradient Sky"
{
    Properties
    {
        _TopColor("Зенит", Color) = (0.24, 0.42, 0.72, 1)
        _HorizonColor("Горизонт", Color) = (0.72, 0.80, 0.88, 1)
        _BottomColor("Низ", Color) = (0.40, 0.38, 0.34, 1)

        _HorizonSharpness("Резкость горизонта", Range(0.5, 8)) = 2.2
        _HorizonHeight("Высота горизонта", Range(-0.5, 0.5)) = 0.0
        _Exposure("Экспозиция", Range(0, 4)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 _TopColor;
            half4 _HorizonColor;
            half4 _BottomColor;
            half _HorizonSharpness;
            half _HorizonHeight;
            half _Exposure;

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Купол неба рисуется в пространстве объекта вокруг камеры,
                // поэтому направление взгляда — это сама позиция вершины.
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.directionWS = input.positionOS.xyz;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float height = normalize(input.directionWS).y - _HorizonHeight;

                // Резкость возводит долю в степень, сохраняя знак: одинаковый профиль
                // перехода и вверх, и вниз от линии горизонта.
                float shaped = pow(abs(height), _HorizonSharpness);

                half3 color = height > 0.0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, saturate(shaped))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(shaped));

                return half4(color * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
