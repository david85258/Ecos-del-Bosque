// Shader para hierba y hojas del suelo (URP, Forward y Forward+).
//
// - Ilumina con la normal hacia arriba: la hierba recibe la misma luz que el suelo y no se ven
//   los planos oscuros segun hacia donde miren.
// - Viento en el vertice (solo mueve la parte alta: usa la Y local de la malla, 0 = base, 1 = punta).
// - Variacion de color por manchas en el mundo, para que no se repita.
// - Luces adicionales (linterna, antorchas...), sombras de la luz principal y niebla.
// - Se encoge suavemente cerca de la distancia maxima (_EcosVegetacionDistancia, la pone GeneradorVegetacion).
Shader "Ecos del Bosque/Vegetacion"
{
    Properties
    {
        [MainTexture] _BaseMap("Textura", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Recorte alfa", Range(0,1)) = 0.5

        [Header(Color)]
        _VariacionColor("Color de variacion", Color) = (0.95,0.85,0.55,1)
        _CantidadVariacion("Cantidad de variacion", Range(0,1)) = 0.35
        _EscalaVariacion("Tamano de las manchas (m)", Float) = 9
        _OscurecerBase("Oscurecer la base", Range(0,1)) = 0.45
        _Translucidez("Luz a traves de las hojas", Range(0,1)) = 0.35

        [Header(Viento)]
        _FuerzaViento("Fuerza", Range(0,1)) = 0.12
        _VelocidadViento("Velocidad", Range(0,5)) = 1.2
        _EscalaRafagas("Tamano de las rafagas (m)", Float) = 7
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half4 _VariacionColor;
            half _CantidadVariacion;
            float _EscalaVariacion;
            half _OscurecerBase;
            half _Translucidez;
            half _FuerzaViento;
            float _VelocidadViento;
            float _EscalaRafagas;
        CBUFFER_END

        float _EcosVegetacionDistancia; // global; 0 = sin desvanecer

        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float RuidoValor(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = Hash21(i), b = Hash21(i + float2(1, 0));
            float c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // Posicion de mundo con viento y desvanecido. altura = Y local (0 base, 1 punta).
        float3 PosicionVegetacion(float3 positionOS, out float3 origen)
        {
            origen = TransformObjectToWorld(float3(0, 0, 0));
            float3 posWS = TransformObjectToWorld(positionOS);
            float altura = saturate(positionOS.y);

            if (_EcosVegetacionDistancia > 0.0)
            {
                float d = distance(origen, _WorldSpaceCameraPos);
                float f = saturate((_EcosVegetacionDistancia - d) / (_EcosVegetacionDistancia * 0.25));
                posWS = origen + (posWS - origen) * f;
            }

            float t = _Time.y * _VelocidadViento;
            float2 dir = normalize(float2(1.0, 0.45));
            float rafaga = RuidoValor(origen.xz / max(0.1, _EscalaRafagas) - dir * t * 0.6);
            float onda = sin(t * 2.0 + dot(origen.xz, dir) * 0.7) * 0.5 + 0.5;
            float temblor = sin(t * 7.0 + Hash21(origen.xz) * 6.28) * 0.15;
            float desplaz = (rafaga * 0.75 + onda * 0.25 + temblor) * _FuerzaViento * altura * altura;
            posWS.xz += dir * desplaz;
            posWS.y -= abs(desplaz) * 0.25;
            return posWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 tinte : TEXCOORD2; // rgb = color, a = altura
                half fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 origen;
                float3 posWS = PosicionVegetacion(v.positionOS.xyz, origen);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

                float n = RuidoValor(origen.xz / max(0.1, _EscalaVariacion));
                half3 tinte = lerp(half3(1, 1, 1), _VariacionColor.rgb, smoothstep(0.35, 0.75, n) * _CantidadVariacion);
                tinte *= lerp(0.85, 1.1, Hash21(origen.xz));
                o.tinte = half4(tinte, saturate(v.positionOS.y));
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                clip(tex.a - _Cutoff);

                half3 albedo = tex.rgb * i.tinte.rgb;
                albedo *= lerp(1.0 - _OscurecerBase, 1.0, saturate(i.tinte.a * 2.5));

                const half3 normalWS = half3(0, 1, 0);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light luz = GetMainLight(shadowCoord);
                half difusa = saturate(dot(normalWS, luz.direction)) * 0.6 + 0.4 + _Translucidez * 0.4;
                half3 color = albedo * luz.color * (luz.distanceAttenuation * luz.shadowAttenuation * difusa);
                color += albedo * SampleSH(normalWS);

            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint numLuces = GetAdditionalLightsCount();

                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    half d = saturate(dot(normalWS, l.direction)) * 0.5 + 0.5 + _Translucidez * 0.4;
                    color += albedo * l.color * (l.distanceAttenuation * l.shadowAttenuation * d);
                }
                #endif

                LIGHT_LOOP_BEGIN(numLuces)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    // Luz casi horizontal (linterna): la envoltura evita que la hierba quede negra.
                    half d = saturate(dot(normalWS, l.direction)) * 0.5 + 0.5 + _Translucidez * 0.4;
                    color += albedo * l.color * (l.distanceAttenuation * l.shadowAttenuation * d);
                LIGHT_LOOP_END
            #endif

                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertSombra
            #pragma fragment fragSombra
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesSombra
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsSombra
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsSombra vertSombra(AttributesSombra v)
            {
                VaryingsSombra o = (VaryingsSombra)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 origen;
                float3 posWS = PosicionVegetacion(v.positionOS.xyz, origen); // con viento: la sombra se mueve igual
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 dirLuz = normalize(_LightPosition - posWS);
            #else
                float3 dirLuz = _LightDirection;
            #endif
                // Normal hacia la luz: el "normal bias" no deforma los planos finos de la hierba.
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(posWS, dirLuz, dirLuz)));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 fragSombra(VaryingsSombra i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertProf
            #pragma fragment fragProf
            #pragma multi_compile_instancing

            struct AttributesProf
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsProf
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsProf vertProf(AttributesProf v)
            {
                VaryingsProf o = (VaryingsProf)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 origen;
                o.positionCS = TransformWorldToHClip(PosicionVegetacion(v.positionOS.xyz, origen));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half fragProf(VaryingsProf i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertNorm
            #pragma fragment fragNorm
            #pragma multi_compile_instancing

            struct AttributesNorm
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsNorm
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsNorm vertNorm(AttributesNorm v)
            {
                VaryingsNorm o = (VaryingsNorm)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 origen;
                o.positionCS = TransformWorldToHClip(PosicionVegetacion(v.positionOS.xyz, origen));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 fragNorm(VaryingsNorm i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                return half4(0, 1, 0, 0); // normal hacia arriba (codificacion por defecto de URP)
            }
            ENDHLSL
        }
    }

    FallBack Off
}
