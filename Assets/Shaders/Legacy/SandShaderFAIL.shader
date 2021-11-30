Shader "Unlit/SandShader"
{
    Properties
    {
        _SandWaveTex ("Calm wave texture 1 for shape", 2D) = "white" {}
        _SandWaveTex2 ("Calm Wave texture 2 for shape", 2D) = "white" {}
        _WaveDisplacementTex ("Displacement texture for sand waves", 2D) = "white" {}
        _BackgroundTex ("Background sand texture", 2D) = "white" {}
        _WaveTex ("material/Color of sand wave", 2D) = "white" {}
        
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
            float computeAlpha(float value, float higherThreshold, float lowerThreshold);
            float random2 (float2 st);

            struct VertexAttributes
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
                float2 dir : TEXCOORD1;
            };

            struct FragmentAttributes
            {
                float4 clipPos : POSITION;
                float4 worldPos : TEXCOORD0;
                float2 st : TEXCOORD1;
                float2 dir : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
            };

            sampler2D _SandWaveTex;
            sampler2D _SandWaveTex2;
            sampler2D _WaveDisplacementTex;
            sampler2D _BackgroundTex;
            sampler2D _WaveTex;

            float4 _LightColor;

            float4x4 _ProjInverse;
            float4x4 _ViewInverse;

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
                
                return output;
            }

            float4 frag (FragmentAttributes input) : SV_Target
            {
                
                /// Change for more aggressive displacement //
                float displacementFactor = 0.1f;//0.03f;
                float displacementFactorWhite = 0.015f;//0.03f;
                ///

                /// Scrolling texture thresholds //
                float higherThresholdWaves = 0.75f;//1.0f;
                float lowerThresholdWaves = 0.68f;
                ///

                /// Calculate displacement with displacement texture //
                float3 displacement = tex2D(_WaveDisplacementTex, input.st);
                ///

                /// Get displaced wave texture coordinates //
                float2 displacedStTex1 =  input.st + getDisplacementVector(displacement, displacementFactorWhite);
                float2 displacedStTex2 =  input.st + getDisplacementVector(displacement, displacementFactorWhite);
                ///

                /// Sample from textures //
                float3 waveTex1 = tex2D(_SandWaveTex, displacedStTex1);
                float3 waveTex2 = tex2D(_SandWaveTex2, displacedStTex2);
                float3 backgroundColor = float3(0.97, 0.90, 0.66);//tex2D(_BackgroundTex, input.st);
                float3 waveColor = tex2D(_WaveTex, input.st);

                float3 wave = waveTex1 + waveTex2; // Add together wave textures
                
                float alpha = computeAlpha(wave.r, higherThresholdWaves, lowerThresholdWaves); // Compute alpha for waves
                
                //outColor = float3(1.0f, 1.0f, 1.0f); // Make white

                int isWater = step(alpha, 0.01f);

                float3 outColor = (1 - isWater) * waveColor + backgroundColor;
                
                return float4(outColor, 1);
            }

            /**
             * Returns displacement direction
             */
            float2 getDisplacementVector(float3 displacement, float displacementFactor)
            {
                float2 displacementVector = float2(displacement.r, displacementFactor) * displacementFactor * 2 - 1;

                return displacementVector;
            }
            
            float3 ambient()
            {
                return float3(0.5f, 0.5f, 0.5f);
            }

            float3 diffuse(float3 normal)
            {
                return _LightColor.rgb * dot(_WorldSpaceLightPos0, normal);
            }

            float computeAlpha(float value, float higherThreshold, float lowerThreshold)
            {
                // Makes values <= higherThreshold visible
                int isOpaque1 = step(value, higherThreshold);

                // Makes values >= lowerThreshold visible
                int isOpaque2 = step(lowerThreshold, value);
                
                return value * isOpaque1 * isOpaque2;
            }
            
            // Some useful functions
            float3 mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float2 mod289(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float3 permute(float3 x) { return mod289(((x*34.0)+1.0)*x); }

            uint baseHash(uint2 p)
            {
                p = 1103515245U * ((p >> 1U)^(p.yx));
                uint h32 = 1103515245U * ((p.x)^(p.y>>3U));
                return h32^(h32 >> 16);
            }

            float4 hash42(float2 x)
            {
                uint n = baseHash(asuint(x));
                uint4 rz = uint4(n, n * 16807U, n * 48271U, n * 69621U);
                return float4((rz >> 1) & uint4((0x7fffffffU).xxxx))/float(0x7fffffff);
            }
            ///

            float random2(float2 st) {
                return hash42(st).x;
            }
            
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
