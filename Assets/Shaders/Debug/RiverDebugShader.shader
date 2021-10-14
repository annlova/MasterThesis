Shader "Unlit/RiverShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityShaderVariables.cginc"
            #include "UnityCG.cginc"

            struct vertexAttributes
            {
                float4 vertex : POSITION;
                float4 nor : NORMAL;
                float2 uv : TEXCOORD0;
                float2 dir : TEXCOORD1;
            };

            struct fragmentAttributes
            {
                float4 vertex : POSITION;
                float4 worldPos : TEXCOORD4;
                float2 uv : TEXCOORD0;
                float2 dir : TEXCOORD1;
                float3 worldNor : TEXCOORD2;
                float4 screenPos : TEXCOORD5;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4x4 _ProjInverse;
            float4x4 _ViewInverse;

            fragmentAttributes vert (vertexAttributes input)
            {
                fragmentAttributes o;
                o.worldPos = mul(unity_ObjectToWorld, input.vertex);
                o.vertex = UnityObjectToClipPos(input.vertex);
                o.worldNor = normalize(UnityObjectToWorldNormal(input.nor.xyz));
                o.uv = input.uv;
                o.dir = input.dir;

                o.screenPos = ComputeScreenPos(o.vertex);
                
                return o;
            }

            sampler2D _CameraDepthTexture;

            float3 WorldPosFromDepth(float2 uv, float depth) {
                float z = depth * 2.0 - 1.0;

                float4 clipSpacePosition = float4(uv * 2.0 - 1.0, z, 1.0);
                float4 viewSpacePosition = mul(_ProjInverse, clipSpacePosition);

                // Perspective division
                viewSpacePosition /= viewSpacePosition.w;

                float4 worldSpacePosition = mul(_ViewInverse, viewSpacePosition);

                return worldSpacePosition.xyz;
            }
            
            float4 frag (fragmentAttributes i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float depth = 1.0f - SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                
				float3 world = WorldPosFromDepth(uv, depth);
                float d = distance(i.worldPos, world);

                float is = step(d, 1.0f);
                float falloff = smoothstep(0.0f, 1.0f, d);
				float3 color = float3(0.4f, 0.6f, 0.8f);
				float3 white = float(1.0f).rrr;

                float3 noise = tex2D(_MainTex, i.uv + -i.dir * _Time.x);
                is = step(0.5f, is * noise);
                
				// return float4(d.xxx / 10.0f, 1.0f);
				return float4(color * (1.0f - is) + lerp(white, color, falloff) * is, 1.0f);
                
                // float3 cameraDir = normalize(_WorldSpaceCameraPos - input.worldPos.xyz);
                // // sample the default reflection cubemap, using the reflection vector
                // float4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, cameraDir);
                // // decode cubemap data into actual color
                // float3 skyColor = DecodeHDR (skyData, unity_SpecCube0_HDR);
                // float col = tex2D(_MainTex, input.uv + _Time.z * input.dir * float2(1.0f, -1.0f));
                // return float4(skyColor, col);
            }
            ENDHLSL
        }
    }
}
