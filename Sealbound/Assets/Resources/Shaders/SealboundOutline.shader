// Cartoon outline for animated (skinned) characters and the red attack warning.
// Draws the model a little "puffed out" along its normals with the front faces hidden,
// so only a rim shows around the body. Width is in metres, so it looks the same on any model scale.
Shader "Sealbound/Outline"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.08, 0.06, 0.05, 1)
        _Width ("Width (metres)", Float) = 0.02
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        HLSLINCLUDE
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

        float3 PuffedWorldPosition(Attributes v, out float3 normalWS)
        {
            float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
            normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
            return positionWS + normalWS * _Width;
        }
        ENDHLSL

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float fogFactor : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 n;
                o.positionCS = TransformWorldToHClip(PuffedWorldPosition(v, n));
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(MixFog(_BaseColor.rgb, i.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Front
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 vert(Attributes v) : SV_POSITION
            {
                float3 n;
                return TransformWorldToHClip(PuffedWorldPosition(v, n));
            }

            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(PuffedWorldPosition(v, o.normalWS));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(normalize(i.normalWS), 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
