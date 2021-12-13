Shader "Custom/PostProcessShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OtherTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityShaderVariables.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            
            sampler2D _OtherTex;
            float4 _OtherTex_ST;

            sampler2D _CameraDepthTexture;

            fixed4 frag (const v2f input) : SV_Target
            {
                return tex2D(_MainTex, input.uv);
                // DecodeDepthNormal();
                // float depth = 1.0f - SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, input.uv);
                // float z = depth; // TODO
                // float3 normal = ;
                // float3 centerPoint = ; // View space
                //
                // // Calculate A
                // float integral = 0.0f;
                // for (int i = 1; i <= _NumSamples; i++)
                // {
                //     float3 samplePoint = ; // View space
                //     float3 sampleVector = samplePoint - centerPoint;
                //     float numerator = dot(sampleVector, normal) + z * _BiasDistance;
                //     float denominator = dot(sampleVector, sampleVector) + _Epsilon;
                //     integral += max(0.0f, numerator) / denominator;
                // }
                // float sigmaDiv = 2.0f * _Sigma / _NumSamples;
                // float obscurance = 1.0f - sigmaDiv * integral;
                // float A = max(0.0f, obscurance);
                //
                // return float4(A.xxx, 1.0f);
            }
            ENDHLSL
        }
    }
}
