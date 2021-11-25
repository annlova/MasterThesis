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
                float4 tan : TANGENT;
            };

            struct fragmentAttributes
            {
                float4 vertex : POSITION;
                float4 worldPos : TEXCOORD4;
                float2 uv : TEXCOORD0;
                float2 dir : TEXCOORD1;
                float3 worldNor : TEXCOORD2;
                float4 screenPos : TEXCOORD5;
                float3x3 tbn : MATRIX;
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

                float3 tangent = input.tan.xyz * input.tan.w;
                float3 bitangent = cross(input.nor, input.tan.xyz) * input.tan.w;
                float3 normal = input.nor;
                
                float3 T = normalize(float3(UnityObjectToWorldDir(tangent)));
                float3 B = normalize(float3(UnityObjectToWorldDir(bitangent)));
                float3 N = normalize(float3(UnityObjectToWorldDir(normal)));
                o.tbn = float3x3(T, B, N);
                
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

            float distPointSegment(float2 a, float2 b, float2 c)
            {
                float2 ab = b - a;
                float2 ac = c - a;
                float2 bc = b - c;

                float e = dot(ac, ab);
                if (e <= 0.0f) return dot(ac, ac);
                float f = dot(ab, ab);
                if (e >= f) return dot(bc, bc);
                return dot(ac, ac) - e * e / f;
            }
            
            float4 frag (fragmentAttributes i) : SV_Target
            {
                float2 p1 = float2(0.0f, 8.0f);
                float2 p2 = float2(8.0f, 16.0f);
                float2 v = p1 - p2;
                float2 n = normalize(float2(-v.y, v.x));

                float z = fmod(i.worldPos.z, 16.0f);
                float x = fmod(i.worldPos.x, 16.0f);
                float2 pos = float2(x, z);

                float centerLine = sin(z) + sin(z * 2.341);
                centerLine /= 2.0f;
                centerLine = centerLine * 4 + 8;
                x = abs(centerLine - x) / 16.0f;

                float abc = distPointSegment(p1, p2, pos);
                abc = sqrt(abc);
                abc /= 16.0f;
                return float4(abc.xxx, 1.0f);
            
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float depth = 1.0f - SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                
				float3 world = WorldPosFromDepth(uv, depth);
                float d = distance(i.worldPos, world);

                float is = step(d, 0.5f);
                float falloff = smoothstep(0.0f, 0.5f, d);
				float3 color = float3(0.4f, 0.6f, 0.8f);
				float3 white = float(1.0f).rrr;

                float3 noise = tex2D(_MainTex, i.uv + -i.dir * _Time.x);
                is = step(0.5f, is * noise);
                
				// return float4(d.xxx / 10.0f, 1.0f);
				// return float4(color * (1.0f - is) + lerp(white, color, falloff) * is, 1.0f);

                float2 samp = fmod(i.worldPos.xz + _Time.x * i.dir * float2(1.0f, -1.0f), 3.0f) / 3.0f;
                float3 normal = tex2D(_MainTex, samp).xyz;
                float3 normal2 = tex2D(_MainTex, samp * 3.65f).xyz;
                normal = (normal + normal2) - 1.0;
                // normal = float3(normal.x, normal.z, normal.y);
                normal = normalize(mul(i.tbn, normal));
                
                float3 cameraDir = normalize(_WorldSpaceCameraPos - i.worldPos.xyz);
                float fresnel = dot(normal, cameraDir);
                fresnel = smoothstep(0.4f, 0.6f, fresnel);
                // sample the default reflection cubemap, using the reflection vector
                float4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, cameraDir);
                // decode cubemap data into actual color
                float3 skyColor = DecodeHDR (skyData, unity_SpecCube0_HDR);

                float3 c = float3(0.0f, 0.2f, 0.3f);
                c = lerp(c, skyColor, 1.0f - fresnel);
                // return float4(normal, 1.0f);
                return float4(c, 0.7 + 0.3 * falloff);
            }
            ENDHLSL
        }
    }
}
