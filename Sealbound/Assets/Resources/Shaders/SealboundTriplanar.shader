// Real stone (or any surface texture) projected onto a model from three sides, so models whose own
// UVs only point into a flat colour palette (the KayKit ruins, the golem's blocks) can wear scanned,
// photo-real textures. A second texture (moss, grass or dirt) settles on the upward-facing parts.
// Textures are measured in metres in the object's own space, so moving pieces don't "swim".
// Lit by URP's standard physically based lighting, with shadows, SSAO, fog and Forward+ lights.
Shader "Sealbound/Triplanar"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _BaseMap ("Albedo", 2D) = "white" {}
        [Normal] _NormalMap ("Normal (OpenGL)", 2D) = "bump" {}
        _ARMMap ("AO / Roughness / Metal", 2D) = "white" {}
        _Scale ("Metres per tile", Float) = 2
        _NormalStrength ("Normal strength", Range(0, 2)) = 1
        _Smoothness ("Smoothness scale", Range(0, 1)) = 1
        _Sharpness ("Blend sharpness", Range(1, 16)) = 6

        _TopColor ("Top tint", Color) = (1, 1, 1, 1)
        _TopMap ("Top albedo (moss)", 2D) = "white" {}
        [Normal] _TopNormalMap ("Top normal", 2D) = "bump" {}
        _TopARMMap ("Top AO / Roughness / Metal", 2D) = "white" {}
        _TopScale ("Top metres per tile", Float) = 2
        _TopAmount ("Top coverage (0 = none)", Range(0, 1)) = 0
        _TopSoftness ("Top edge softness", Range(0.01, 0.5)) = 0.15

        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        [Toggle(_USE_UV)] _UseUV ("Use the model's own UVs (scans)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap);    SAMPLER(sampler_NormalMap);
        TEXTURE2D(_ARMMap);       SAMPLER(sampler_ARMMap);
        TEXTURE2D(_TopMap);       SAMPLER(sampler_TopMap);
        TEXTURE2D(_TopNormalMap); SAMPLER(sampler_TopNormalMap);
        TEXTURE2D(_TopARMMap);    SAMPLER(sampler_TopARMMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _Scale;
            half _NormalStrength;
            half _Smoothness;
            half _Sharpness;
            half4 _TopColor;
            float4 _TopMap_ST;
            float _TopScale;
            half _TopAmount;
            half _TopSoftness;
            half4 _EmissionColor;
        CBUFFER_END

        // Object-space position in metres (undoing the object's scale), so textures stay the same size
        // on stretched pieces and stick to moving ones.
        float3 MetricObjectPosition(float3 positionOS)
        {
            float3 s = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
            return positionOS * s;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP _FORWARD_PLUS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma shader_feature_local _USE_UV

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionObj : TEXCOORD2; // metres, object space
                float3 normalObj : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                float2 uv : TEXCOORD5;
                float4 tangentWS : TEXCOORD6;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionObj = MetricObjectPosition(v.positionOS.xyz);
                o.normalObj = v.normalOS;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.tangentWS = float4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                return o;
            }

            // Whiteout-blended triplanar normal (Ben Golus' method), returned in object space.
            float3 TriplanarNormal(TEXTURE2D_PARAM(tex, samp), float3 pos, float3 n, float3 w, float strength)
            {
                half3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, pos.zy), strength);
                half3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, pos.xz), strength);
                half3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, pos.xy), strength);
                tx = half3(tx.xy + n.zy, abs(tx.z) * n.x);
                ty = half3(ty.xy + n.xz, abs(ty.z) * n.y);
                tz = half3(tz.xy + n.xy, abs(tz.z) * n.z);
                return normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
            }

            half4 Triplanar(TEXTURE2D_PARAM(tex, samp), float3 pos, float3 w)
            {
                return SAMPLE_TEXTURE2D(tex, samp, pos.zy) * w.x
                     + SAMPLE_TEXTURE2D(tex, samp, pos.xz) * w.y
                     + SAMPLE_TEXTURE2D(tex, samp, pos.xy) * w.z;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 nObj = normalize(i.normalObj);
                float3 w = pow(abs(nObj), _Sharpness);
                w /= max(1e-4, w.x + w.y + w.z);

            #if defined(_USE_UV)
                // A scanned model with its own UVs: plain texture lookups, normal map in tangent space.
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half4 arm = SAMPLE_TEXTURE2D(_ARMMap, sampler_ARMMap, i.uv);
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv), _NormalStrength);
                float3 nW0 = normalize(i.normalWS);
                float3 tW = normalize(i.tangentWS.xyz);
                float3 bW = cross(nW0, tW) * i.tangentWS.w;
                float3 nWorldFromMap = normalize(mul(nTS, float3x3(tW, bW, nW0)));
                float3 nTex = normalize(mul(nWorldFromMap, (float3x3)GetObjectToWorldMatrix())); // object space, like the triplanar path
            #else
                float3 pos = i.positionObj / max(0.01, _Scale);
                half4 albedo = Triplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), pos, w) * _BaseColor;
                half4 arm = Triplanar(TEXTURE2D_ARGS(_ARMMap, sampler_ARMMap), pos, w);
                float3 nTex = TriplanarNormal(TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap), pos, nObj, w, _NormalStrength);
            #endif

                // Moss / grass on the upward faces, judged in world space so it stays on top as pieces turn.
                float3 nWS = normalize(i.normalWS);
                half top = 0;
                if (_TopAmount > 0.001)
                {
                    float3 tpos = i.positionObj / max(0.01, _TopScale);
                    half4 talb = Triplanar(TEXTURE2D_ARGS(_TopMap, sampler_TopMap), tpos, w) * _TopColor;
                    // Break up the edge with the stone's own brightness, so moss sits in the cracks.
                    half grit = dot(albedo.rgb, half3(0.33, 0.33, 0.33));
                    half threshold = 1 - _TopAmount * 1.6;
                    top = smoothstep(threshold - _TopSoftness, threshold + _TopSoftness, nWS.y + (0.5 - grit) * 0.4);
                    if (top > 0.001)
                    {
                        half4 tarm = Triplanar(TEXTURE2D_ARGS(_TopARMMap, sampler_TopARMMap), tpos, w);
                        float3 tn = TriplanarNormal(TEXTURE2D_ARGS(_TopNormalMap, sampler_TopNormalMap), tpos, nObj, w, _NormalStrength);
                        albedo = lerp(albedo, talb, top);
                        arm = lerp(arm, tarm, top);
                        nTex = normalize(lerp(nTex, tn, top));
                    }
                }

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = normalize(TransformObjectToWorldNormal(nTex));
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo.rgb;
                s.alpha = 1;
                s.metallic = arm.b;
                s.smoothness = saturate((1 - arm.g) * _Smoothness);
                s.occlusion = arm.r;
                s.emission = _EmissionColor.rgb;
                s.normalTS = half3(0, 0, 1);
                s.specular = half3(0, 0, 0);

                half4 color = UniversalFragmentPBR(inputData, s);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
