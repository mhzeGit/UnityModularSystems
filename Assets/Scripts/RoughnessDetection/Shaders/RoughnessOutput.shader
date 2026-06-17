Shader "Hidden/MHZE/RoughnessOutput"
{
    Properties
    {
        _MetallicGlossMap ("Metallic Smoothness Map", 2D) = "white" {}
        _SpecGlossMap ("Specular Smoothness Map", 2D) = "white" {}
        _BaseMap ("Base Map (for alpha smoothness)", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _SampleUV ("Sample UV", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Pass
        {
            Cull Off
            ZTest Off
            ZWrite Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_SpecGlossMap); SAMPLER(sampler_SpecGlossMap);
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            float2 _SampleUV;
            half _Smoothness;
            float4 _BaseMap_ST;
            half _SmoothnessTextureChannel;
            half _WorkflowMode;

            Varyings vert(Attributes input)
            {
                Varyings output;
                float2 pos = float2((input.vertexID << 1) & 2, input.vertexID & 2) * 2.0 - 1.0;
                output.positionCS = float4(pos, 0, 1);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = _SampleUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
                half smoothness = _Smoothness;

                if (_SmoothnessTextureChannel > 0.5)
                {
                    smoothness *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
                }
                else if (_WorkflowMode > 0.5)
                {
                    smoothness *= SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv).a;
                }
                else
                {
                    smoothness *= SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv).a;
                }

                half roughness = 1.0h - saturate(smoothness);
                return half4(roughness, roughness, roughness, 1);
            }
            ENDHLSL
        }
    }
}
