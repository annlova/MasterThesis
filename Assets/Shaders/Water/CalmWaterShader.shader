Shader "Unlit/CalmWaterShader"
{
    Properties
    {
        _CalmWaveTex ("Calm wave texture 1", 2D) = "white" {}
        _CalmWaveTex2 ("Calm Wave texture 2", 2D) = "white" {}
        _WaveDisplacementTex ("Displacement texture for calm waves", 2D) = "white" {}
        _WaterTex ("Texture for water (color mainly) 1", 2D) = "white" {}
        _WaterTex2 ("Texture for water (color mainly) 2", 2D) = "white" {}
        
        _LightColor ("Color of light", Vector) = (1.0, 1.0, 1.0, 1.0)
    }
    SubShader
    {
        Tags { "Queue"="Geometry+1" "RenderMode"="Opaque"  "LightMode" = "ForwardBase" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityShaderVariables.cginc"

            #pragma multi_compile_fwdbase
            #include "AutoLight.cginc"

            float3 getColor(float2 st);
            float2 getDisplacementVector(float3 displacement, float displacementFactor);
            float3 ambient();
            float3 diffuse(float3 normal);
            float computeAlpha(float value, float distance, float higherThreshold, float lowerThreshold);
            float computeGlitterIntensity(float3 glitterProperties);
            float nsin(float rad);
            float random2 (float2 st);

            struct FragmentAttributes
            {
                float4 pos : POSITION;
                float4 worldPos : TEXCOORD0;
                float2 st : TEXCOORD1;
                float2 dir : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                LIGHTING_COORDS(4,5)
            };

            sampler2D _CalmWaveTex;
            sampler2D _CalmWaveTex2;
            sampler2D _WaveDisplacementTex;
            sampler2D _WaterTex;
            sampler2D _WaterTex2;
            
            sampler2D _CameraDepthTexture;

            float4 _LightColor;

            float4x4 _ProjInverse;
            float4x4 _ViewInverse;

            FragmentAttributes vert (appdata_full v)
            {
                // The output struct for the fragment stage.
                FragmentAttributes output;

                float4 modelPos = v.vertex;

                output.worldPos = mul(unity_ObjectToWorld, modelPos);
                output.pos = UnityObjectToClipPos(modelPos);
                
                output.st = v.texcoord;
                output.dir = v.texcoord1;

                output.screenPos = ComputeScreenPos(output.pos);

                TRANSFER_VERTEX_TO_FRAGMENT(output);
                
                return output;
            }

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
                input.dir = -input.dir;
                /// To calculate depth //
                float2 uv = input.screenPos.xy / input.screenPos.w;
                float depth = 1.0f - SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                                
                float3 world = WorldPosFromDepth(uv, depth);
                float d = distance(input.worldPos, world);
                ///

                /// Calculate if in shadow //
                float attenuation = LIGHT_ATTENUATION(input);
                float inLight = step(1.0f, attenuation);
                /// 
                
                /// Change for more aggressive displacement //
                float displacementFactor = 0.1f;//0.03f;
                float displacementFactorWhite = 0.015f;//0.03f;
                ///

                /// Change for faster scrolling //
                float timeFactor = _Time.y / 15;
                ///

                /// Scrolling texture thresholds //
                float higherThresholdWaves = 0.70f;//1.0f;
                float lowerThresholdWaves = 0.68f;
                ///

                /// Calculate displacement with displacement texture //
                float3 displacement = tex2D(_WaveDisplacementTex, input.st * 0.1f + timeFactor* 0.5);
                ///

                /// Get scrolled and displaced wave texture coordinates //
                float2 scrolledStTex1 = fmod(input.worldPos.xz / 1.5f + float2(timeFactor, -timeFactor),
                    10.0f) / 10.0f + input.dir * float2(-1.0f, -1.0f) * timeFactor * (-0.55);
                float2 displacedStTex1 =  scrolledStTex1 + getDisplacementVector(displacement, displacementFactorWhite);
                float2 scrolledStTex2 = fmod(input.worldPos.xz / 1.5f + float2(timeFactor / 3.0f, timeFactor),
                    10.0f) / 10.0f + input.dir * float2(-1.0f, -1.0f) * timeFactor * (0.9);
                float2 displacedStTex2 =  scrolledStTex2 + getDisplacementVector(displacement, displacementFactorWhite);
                ///

                /// Get scrolled and displaced water texture coordinates//
                //TODO FIX WATER DIRECTION MISSTAKE HERE 
                float2 displacedStWaterTex1 =  scrolledStTex2 + getDisplacementVector(displacement, displacementFactor);
                float2 displacedStWaterTex2 =  scrolledStTex1 + getDisplacementVector(displacement, displacementFactor);
                //

                /// Sample from textures //
                float3 tex1Color = tex2D(_CalmWaveTex, displacedStTex1 / 2.0f);
                float3 tex2Color = tex2D(_CalmWaveTex2, displacedStTex2 / 2.0f);
                
                float3 waterColor = tex2D(_WaterTex, input.st) * (clamp(1.0 - d * 0.5, 0.0f, 1.0f) / 3 + 0.5); // reverse depth colors
                // float3 waterColor = tex2D(_WaterTex, displacedStWaterTex1) * clamp(d, 0.5f, 1.0f);
                float3 waterColor2 = tex2D(_WaterTex2, displacedStWaterTex2) * clamp(d, 0.5f, 1.0f);
                ///
                
                waterColor = lerp(waterColor, waterColor2, d * 0.6f); // Interpolate water textures

                float3 outColor = tex1Color + tex2Color; // Add together wave textures
                
                float alpha = computeAlpha(outColor.r, clamp(d, 0.0f, 1.0f), higherThresholdWaves, lowerThresholdWaves); // Compute alpha for waves
                
                outColor = float3(1.0f, 1.0f, 1.0f); // Make white
                
                // float mixFactor = 0.035f - abs(alpha - 0.715f);
                
                // outColor = lerp(waterColor, outColor, smoothstep(0.68f, 0.75f, alpha) * lerp(1,0.2, clamp(d*0.45, 0,1)));
                outColor = lerp(waterColor, outColor, smoothstep(0.68f, 0.75f, alpha) * 0.25); // Change smoothstep to change wave color intensity

                int isWater = step(alpha, 0.01f);

                // float specularStrength = 1000.0f;
                // float spec = pow(max(displacement.r+0.2, 0.0f), 32);
                // float3 specular = specularStrength * spec * (1.0f).xxx;
                // specular = clamp(0.0f, 1.0f, specular);

                // return float4(displacement, 1.0f);
                
                // return float4(d,d,d, 1);
                // return float4(waterColor2, 1.0f);
                float shadowFactor = lerp(0.5f, 1.0f, attenuation);
                return float4(lerp(waterColor2, waterColor2 * 1.5f, alpha) * float3(0.3f, 0.99f, 1.2f) * shadowFactor, 1.0f);
                return float4(outColor * min((1 - isWater), 1)
                    + waterColor * min(isWater, 1)
                    , 1);
                // return float4(specular, 1.0f);
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

            float computeAlpha(float value, float distance, float higherThreshold, float lowerThreshold)
            {
                // Makes values <= higherThreshold visible
                //int isOpaque1 = step(value, higherThreshold - distance * 0.25);
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
        
//        Pass
//        {
//            Tags{ "Queue"="Transparent" "RenderMode"="Transparent" "LightMode" = "ShadowCaster" }
//            ZWrite On
//            CGPROGRAM
//            #pragma vertex VSMain
//            #pragma fragment PSMain
//
//            #include "UnityCG.cginc"
//            
//            float4 VSMain (float4 vertex:POSITION) : SV_POSITION
//            {
//                return UnityObjectToClipPos(vertex);
//            }
// 
//            float4 PSMain (float4 vertex:SV_POSITION) : SV_TARGET
//            {
//                if (unity_LightShadowBias.z != 0.0) discard;
//                return 0;
//            }
//
//            ENDCG
//        }
    }
//    Fallback "VertexLit"
}
