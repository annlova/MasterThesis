Shader "Unlit/WaterShader"
{
    Properties
    {
        _CalmWaveTex ("Calm wave texture 1", 2D) = "white" {}
        _CalmWaveTex2 ("Calm Wave texture 2", 2D) = "white" {}
        _WaveDisplacementTex ("Displacement texture for calm waves", 2D) = "white" {}
        
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

            struct VertexAttributes
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct FragmentAttributes
            {
                float4 clipPos : POSITION;
                float4 worldPos : TEXCOORD0;
                float2 st : TEXCOORD1;
            };

            sampler2D _CalmWaveTex;
            sampler2D _CalmWaveTex2;
            sampler2D _WaveDisplacementTex;

            float4 _LightColor;

            FragmentAttributes vert (VertexAttributes input)
            {
                // The output struct for the fragment stage.
                FragmentAttributes output;

                float4 modelPos = input.pos;

                output.worldPos = mul(unity_ObjectToWorld, modelPos);
                output.clipPos = UnityObjectToClipPos(modelPos);
                
                output.st = input.uv;
                
                return output;
            }

            float4 frag (FragmentAttributes input) : SV_Target
            {
                // Change for more aggressive displacement
                float displacementFactor = 0.05f;

                // Change for faster scrolling. (Same for both texture and displacement)
                float timeFactor = _Time.y / 10;
                
                float3 displacement = tex2D(_WaveDisplacementTex, input.st + timeFactor);

                float2 scrolledSt = float2(input.st.x + timeFactor, input.st.y - timeFactor);
                float2 displacedSt = scrolledSt + getDisplacementVector(displacement, displacementFactor);
                
                
                float3 color = getColor(displacedSt);
            
                return float4(color, 1);
            }

            float3 getColor(float2 st)
            {
                float3 test = tex2D(_CalmWaveTex, st);

                return test;
            }

            float2 getDisplacementVector(float3 displacement, float displacementFactor)
            {
                float2 displacementVector = float2(displacement.r, displacement.r) * displacementFactor * 2 - 1;

                return displacementVector;
                //float2 displacedSt = st + displacementVector;
                
                //return displacedSt;
            }
            
            float3 ambient()
            {
                return float3(0.5f, 0.5f, 0.5f);
            }

            float3 diffuse(float3 normal)
            {
                return _LightColor.rgb * dot(_WorldSpaceLightPos0, normal);
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
