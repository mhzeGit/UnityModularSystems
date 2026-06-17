Shader "Hidden/MHZE/RoughnessOutput"
{
    Properties
    {
        _RoughnessTex ("Roughness Texture", 2D) = "white" {}
        _RoughnessTex_ST ("Roughness Tex ST", Vector) = (1,1,0,0)
        _RoughnessParams ("Roughness Params", Vector) = (3,1,0,0)
        _RoughnessFallback ("Roughness Fallback", Range(0,1)) = 0.5
        _SampleUV ("Sample UV", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Pass
        {
            Cull Off ZTest Off ZWrite Off Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            TEXTURE2D(_RoughnessTex); SAMPLER(sampler_RoughnessTex);
            float4 _RoughnessTex_ST;
            float4 _RoughnessParams;
            float _RoughnessFallback;
            float2 _SampleUV;

            Varyings vert(Attributes input)
            {
                Varyings o;
                float2 pos = float2((input.vertexID << 1) & 2, input.vertexID & 2) * 2.0 - 1.0;
                o.positionCS = float4(pos, 0, 1);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = _SampleUV * _RoughnessTex_ST.xy + _RoughnessTex_ST.zw;
                half value;

                if (_RoughnessParams.z > 0.5)
                {
                    half4 texel = SAMPLE_TEXTURE2D(_RoughnessTex, sampler_RoughnessTex, uv);
                    half c = _RoughnessParams.x;
                    value = c < 0.5 ? texel.r : c < 1.5 ? texel.g : c < 2.5 ? texel.b : texel.a;
                }
                else
                {
                    value = _RoughnessFallback;
                }

                if (_RoughnessParams.y > 0.5)
                    value = 1.0h - value;

                return half4(saturate(value), saturate(value), saturate(value), 1);
            }
            ENDHLSL
        }
    }
}
