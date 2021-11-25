Shader "Unlit/CalmWaterShader"
{
    Properties
    {
        _CalmWaveTex ("Calm wave texture 1", 2D) = "white" {}
        _CalmWaveTex2 ("Calm Wave texture 2", 2D) = "white" {}
        _WaveDisplacementTex ("Displacement texture for calm waves", 2D) = "white" {}
        _NormalTex ("Water Normals", 2D) = "white" {}
        
        _LightColor ("Color of light", Vector) = (1.0, 1.0, 1.0, 1.0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityShaderVariables.cginc"

            float3 getColor(float2 st);
            float2 getDisplacementVector(float3 displacement, float displacementFactor);
            float3 ambient();
            float3 diffuse(float3 normal);
            float computeAlpha(float value, float high, float low);

            float rando(float2 x);
            float snoiseNormalized(float2 v);

            struct VertexAttributes
            {
                float4 pos : POSITION;
                float3 nor : NORMAL;
                float2 uv : TEXCOORD0;
                float2 dir : TEXCOORD1;
                float4 tan : TANGENT;
            };

            struct FragmentAttributes
            {
                float4 clipPos : POSITION;
                float4 worldPos : TEXCOORD0;
                float2 st : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float2 dir : TEXCOORD3;
                float3x3 tbn : MATRIX;
            };

            sampler2D _CalmWaveTex;
            sampler2D _CalmWaveTex2;
            sampler2D _WaveDisplacementTex;
            sampler2D _CameraDepthTexture;
            sampler2D _NormalTex;

            float4 _LightColor;

            FragmentAttributes vert (VertexAttributes input)
            {
                // The output struct for the fragment stage.
                FragmentAttributes output;

                float4 modelPos = input.pos;

                output.worldPos = mul(unity_ObjectToWorld, modelPos);
                output.clipPos = UnityObjectToClipPos(modelPos);
                
                output.st = input.uv;
                output.dir = input.dir;
                
                output.screenPos = ComputeScreenPos(output.clipPos);

                float3 tangent = input.tan.xyz * input.tan.w;
                float3 bitangent = cross(input.nor, input.tan.xyz) * input.tan.w;
                float3 normal = input.nor;
                
                float3 T = normalize(float3(UnityObjectToWorldDir(tangent)));
                float3 B = normalize(float3(UnityObjectToWorldDir(bitangent)));
                float3 N = normalize(float3(UnityObjectToWorldDir(normal)));
                output.tbn = float3x3(T, B, N);
                
                return output;
            }

            float4x4 _ProjInverse;
            float4x4 _ViewInverse;

            float3 WorldPosFromDepth(float2 uv, float depth) {
                float z = depth * 2.0 - 1.0;

                float4 clipSpacePosition = float4(uv * 2.0 - 1.0, z, 1.0);
                float4 viewSpacePosition = mul(_ProjInverse, clipSpacePosition);

                // Perspective division
                viewSpacePosition /= viewSpacePosition.w;

                float4 worldSpacePosition = mul(_ViewInverse, viewSpacePosition);

                return worldSpacePosition.xyz;
            }
            
            float4 frag (FragmentAttributes input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;
                float depth = 1.0f - SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                
				float3 world = WorldPosFromDepth(uv, depth);
                float d = distance(input.worldPos, world);
                
                // Change for faster scrolling
                float timeFactor = _Time.y / 10;

                float2 scrolledStTex1St = float2(input.st.x + timeFactor, input.st.y - timeFactor);
                float2 scrolledStTex2St = float2(input.st.x + timeFactor / 3, input.st.y + timeFactor);

                float2 abc1 = fmod(input.worldPos.xz + float2(timeFactor, -timeFactor), 10.0f) / 10.0f;
                float2 abc2 = fmod(input.worldPos.xz + float2(timeFactor / 3.0f, timeFactor), 10.0f) / 10.0f;
                
                float3 tex1Color = tex2D(_CalmWaveTex, abc1);
                float3 tex2Color = tex2D(_CalmWaveTex2, abc2);

                float3 outColor = tex1Color + tex2Color;
                float alpha = computeAlpha(outColor.r, 0.75f, 0.7f);
                alpha = step(0.1f, alpha);
                
                float3 normal = tex2D(_NormalTex, input.st + _Time.x * input.dir * float2(1.0f, -1.0f)).xyz;
                float3 normal2 = tex2D(_NormalTex, input.st * 3.65f + _Time.x * input.dir * float2(1.0f, -1.0f)).xyz;
                normal = (normal + normal2) - 1.0;
                // normal = float3(normal.x, normal.z, normal.y);
                normal = normalize(mul(input.tbn, normal));
                
                float3 cameraDir = normalize(_WorldSpaceCameraPos - input.worldPos.xyz);
                float fresnel = dot(normal, cameraDir);
                fresnel = smoothstep(0.4f, 0.6f, fresnel);
                // sample the default reflection cubemap, using the reflection vector
                float4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, cameraDir);
                // decode cubemap data into actual color
                float3 skyColor = DecodeHDR (skyData, unity_SpecCube0_HDR);
                skyColor *= 0.7f;
                
                float is = step(d, 1.0f);
                float falloff = 1.0f - smoothstep(0.0f, 1.0f, d);
				float3 color = float3(0.3f, 0.4f, 1.0f) * d;
				float3 white = float(1.0f).rrr;
                float n = snoiseNormalized(input.worldPos.xz * 5.0 + -input.dir * _Time.y);
                n = step(0.5f, n);
                // is *= n;

                float a = computeAlpha(outColor.r, 0.75 + 0.25 * falloff, 0.7 - 0.7 * falloff);
                float3 w = float3(1.0f, 1.0f, 1.0f) * (a * is + alpha * (1.0f - is));
                float3 c = skyColor + w * 0.7f;
                c = clamp(c, (0.0f).xxx, (1.0f).xxx);

                float3 tempCol = lerp(float3(0.0f, 0.0f, 0.0f) * falloff, skyColor, 1.0f - fresnel);
                // outColor = lerp(tempCol, outColor, );
                float3 water = lerp(lerp(tempCol, outColor, alpha), tempCol, step(fresnel, 0.6f));

                float specularStrength = 1.0f;
                float3 reflectDir = reflect(_WorldSpaceLightPos0, normal);
                float spec = pow(max(dot(-cameraDir, reflectDir), 0.0f), 64);
                float3 specular = specularStrength * spec * (1.0f).xxx;
                return float4(tempCol + specular, (alpha + 1.0 - falloff) * 0.1f + 0.9f);
                // return float4(water, (alpha + 1.0 - falloff) * 0.1f + 0.9f);
                float isWhite = step(0.1f, alpha);
                return float4(white * isWhite + color * (1.0f - isWhite), 1.0f);
            }
            
            float3 ambient()
            {
                return float3(0.5f, 0.5f, 0.5f);
            }

            float3 diffuse(float3 normal)
            {
                return _LightColor.rgb * dot(_WorldSpaceLightPos0, normal);
            }

            float computeAlpha(float value, float high, float low)
            {
                float higherThreshold = high;
                // Makes values <= higherThreshold visible
                int isOpaque1 = step(value, higherThreshold);

                float lowerThreshold = low;
                // Makes values >= lowerThreshold visible
                int isOpaque2 = step(lowerThreshold, value);

                
                return value * isOpaque1 * isOpaque2;
            }

            uint baseHash(uint2 p)
            {
                p = 1103515245U*((p >> 1U)^(p.yx));
                uint h32 = 1103515245U*((p.x)^(p.y>>3U));
                return h32^(h32 >> 16);
            }

            float rando(float2 x)
            {
                uint n = baseHash(asuint(x));
                uint4 rz = uint4(n, n*16807U, n*48271U, n*69621U);
                return float4((rz >> 1) & uint4((0x7fffffffU).xxxx))/float(0x7fffffff).x;
            }
            
            // Some useful functions
            float3 mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float2 mod289(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float3 permute(float3 x) { return mod289(((x*34.0)+1.0)*x); }
            
            float snoise(float2 v) {

                // Precompute values for skewed triangular grid
                const float4 C = float4(0.211324865405187,
                                    // (3.0-sqrt(3.0))/6.0
                                    0.366025403784439,
                                    // 0.5*(sqrt(3.0)-1.0)
                                    -0.577350269189626,
                                    // -1.0 + 2.0 * C.x
                                    0.024390243902439);
                                    // 1.0 / 41.0

                // First corner (x0)
                float2 i  = floor(v + dot(v, C.yy));
                float2 x0 = v - i + dot(i, C.xx);

                // Other two corners (x1, x2)
                float2 i1 = float2(0.0f, 0.0f);
                i1 = (x0.x > x0.y)? float2(1.0, 0.0):float2(0.0, 1.0);
                float2 x1 = x0.xy + C.xx - i1;
                float2 x2 = x0.xy + C.zz;

                // Do some permutations to avoid
                // truncation effects in permutation
                i = mod289(i);
                float3 p = permute(
                        permute( i.y + float3(0.0, i1.y, 1.0))
                            + i.x + float3(0.0, i1.x, 1.0 ));

                float3 m = max(0.5 - float3(
                                    dot(x0,x0),
                                    dot(x1,x1),
                                    dot(x2,x2)
                                    ), 0.0);

                m = m*m ;
                m = m*m ;

                // Gradients:
                //  41 pts uniformly over a line, mapped onto a diamond
                //  The ring size 17*17 = 289 is close to a multiple
                //      of 41 (41*7 = 287)

                float3 x = 2.0 * frac(p * C.www) - 1.0;
                float3 h = abs(x) - 0.5;
                float3 ox = floor(x + 0.5);
                float3 a0 = x - ox;

                // Normalise gradients implicitly by scaling m
                // Approximation of: m *= inversesqrt(a0*a0 + h*h);
                m *= 1.79284291400159 - 0.85373472095314 * (a0*a0+h*h);

                // Compute final noise value at P
                float3 g = float3(0.0f, 0.0f, 0.0f);
                g.x  = a0.x  * x0.x  + h.x  * x0.y;
                g.yz = a0.yz * float2(x1.x,x2.x) + h.yz * float2(x1.y,x2.y);
                return 130.0 * dot(m, g);
            }
            // snoise, but returns values from 0.0 -> 1.0 (instead of -1.0 -> 1.0)
            float snoiseNormalized(float2 v)
            {
                return (snoise(v) + 1.0f) / 2.0f;
            }
            
            ENDHLSL
        }
    }
}
