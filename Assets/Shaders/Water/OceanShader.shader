Shader "Unlit/OceanShader"
{
    Properties
    {
        _CalmWaveTex ("Calm wave texture 1", 2D) = "white" {}
        _CalmWaveTex2 ("Calm Wave texture 2", 2D) = "white" {}
        _WaveDisplacementTex ("Displacement texture for calm waves", 2D) = "white" {}
        _WaterTex ("Texture for water (color mainly) 1", 2D) = "white" {}
        _WaterTex2 ("Texture for water (color mainly) 2", 2D) = "white" {}
        
        _CausTex1 ("Caustic texture 1", 2D) = "white" {}
        _CausTex2 ("Caustic texture 2", 2D) = "white" {}
        
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
//        Tags { "RenderType"="Opaque" }
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

            float2 getDisplacementVector(float3 displacement, float displacementFactor);
            float computeAlpha(float value, float distance, float higherThreshold, float lowerThreshold);
            
            float nsin(float v);
            float ncos(float v);
            float snoise(float2 v);
            float nsnoise(float2 v);

            float4x4 _ProjInverse;
            float4x4 _ViewInverse;
            
            struct VertexAttributes
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
                float2 beachHeights : TEXCOORD1;
            };

            struct FragmentAttributes
            {
                float2 st : TEXCOORD0;
                float4 clipPos : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float heightMap : HEIGHTMAP;
                float4 tideHeight : TIDE;

                float4 screenPos : TEXCOORD3;
                float2 dir : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _CalmWaveTex;
            sampler2D _CalmWaveTex2;
            sampler2D _WaveDisplacementTex;
            sampler2D _WaterTex;
            sampler2D _WaterTex2;

            sampler2D _CausTex1;
            sampler2D _CausTex2;
            
            sampler2D _CameraDepthTexture;
            
            FragmentAttributes vert (VertexAttributes input)
            {
                FragmentAttributes output;
                const float tideChange = 0.15f;
                const float PI = 3.1415927;                             
                const float time = _Time.y * 0.2f;            
                const float waveTime = frac(time) * PI * 2.0f;       

                float animationTimeSkew = 0.3f;
                float waveIn = -1.0f + smoothstep(0.0f, animationTimeSkew, frac(time)) * 2.0f;
                float waveOut = 1.0f - smoothstep(animationTimeSkew, 1.0f, frac(time)) * 2.0f;
                float goingOut = step(animationTimeSkew, frac(time));
                float tideOffset = (waveIn * (1.0f - goingOut) + waveOut * goingOut) * tideChange;
                // float tideOffset = cos(waveTime) * tideChange;
                output.tideHeight = float4(-0.8f + tideOffset, -0.8f - tideChange, -0.8f + tideChange, 0.0f);

                float4 modelPos = input.pos + float4(0.0f, tideOffset, 0.0f, 0.0f);
                
                output.worldPos = mul( modelPos, unity_ObjectToWorld);
                output.clipPos = UnityObjectToClipPos(modelPos);
                
                output.st = input.uv;
                output.dir = float2(0.0f, 1.0f);
                output.screenPos = ComputeScreenPos(output.clipPos);
                
                float beachPosY = -0.3f;
                output.heightMap = input.beachHeights.x + beachPosY;
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
                /// To calculate depth //
                float2 uv = input.screenPos.xy / input.screenPos.w;
                float depth = 1.0f - SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                                
                float3 world = WorldPosFromDepth(uv, depth);
                float d = distance(input.worldPos, world);
                // d *= 0.1;
                // d = depth;
                d = smoothstep(-2.0f, 0.0f, input.heightMap);
                ///

                /// Change for more aggressive displacement //
                float displacementFactor = 0.1f;//0.03f;
                float displacementFactorWhite = 0.015f;//0.03f;
                ///

                /// Change for faster scrolling //
                float timeFactor = _Time.y / 30;
                ///

                /// Scrolling texture thresholds //
                float higherThresholdWaves = 0.9f;//1.0f;
                float lowerThresholdWaves = 0.10f;

                float highCaus = 0.70f;
                float lowCaus = 0.68f;
                ///

                /// Calculate displacement with displacement texture //
                float3 displacement = tex2D(_WaveDisplacementTex, input.st + timeFactor* 0.1);
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
                float3 tex1Color = tex2D(_CalmWaveTex, displacedStTex1 *  1.0f);
                float3 tex2Color = tex2D(_CalmWaveTex2, displacedStTex2 * 1.0f);
                
                float3 caustic1Color = tex2D(_CausTex1, displacedStTex1 *  0.4f);
                float3 caustic2Color = tex2D(_CausTex2, displacedStTex2 * 0.4f);
                
                float3 waterColor2 = tex2D(_WaterTex2, displacedStWaterTex2) * lerp(0.5f, 1.5f, d);

                float3 outColor = tex1Color + tex2Color; // Add together wave textures
                float3 causColor = caustic1Color + caustic2Color; // Add together wave textures
                
                float alphaCalmWater = computeAlpha(outColor.r, clamp(d, 0.0f, 1.0f), higherThresholdWaves, lowerThresholdWaves); // Compute alpha for waves
                float alphaCasutics = computeAlpha(causColor.r, clamp(d, 0.0f, 1.0f), highCaus, lowCaus); // Compute alpha for waves

                float alphaWaveAndCaus = alphaCalmWater + alphaCasutics;
                
                float3 calmWaterColor = lerp(waterColor2, waterColor2 * 1.5f, alphaWaveAndCaus) * float3(0.3f, 0.99f, 1.2f);
                calmWaterColor.r -= 0.2f;
                calmWaterColor.g -= 0.2f;
                calmWaterColor.b -= 0.1f;
                calmWaterColor *= 1.2f;
                
                float tideChange = 0.15f;
                float PI = 3.1415927;                             
                float time = _Time.y * 0.2f;            
                float waveTime = frac(time) * PI * 2.0f;
                
                // sample the texture
                float3 cameraDir = normalize(_WorldSpaceCameraPos - input.worldPos.xyz);
                // sample the default reflection cubemap, using the reflection vector
                float4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, cameraDir);
                // decode cubemap data into actual color
                float3 skyColor = DecodeHDR (skyData, unity_SpecCube0_HDR);
                
                float3 white = (1.0f).xxx;
                float h = input.heightMap;
                float factor = 1.0f - step(input.worldPos.y - 3.0f, h);

                float foamEdgeThicknessMin = 0.07f;
                // float foamEdgeThicknessMax = smoothstep(input.tideHeight.y, input.tideHeight.z, input.worldPos.y) * 0.3f;
                float foamEdgeThicknessMax = (cos(waveTime) + 1.0f) / 2.0f * 0.3f;
                float foam = nsnoise(input.worldPos.xz) * foamEdgeThicknessMax + foamEdgeThicknessMin;
                float waveFactor = step(input.worldPos.y - foam, h);
                float a = lerp(0.0f, PI * 1.7f, frac(_Time.y * 0.2f));
                // waveFactor = max(step(0.995f, ncos(h - a)), waveFactor);
                float3 waveCol = lerp(calmWaterColor, white, waveFactor);
                return float4(waveCol, min(1.0f, 0.4f + 0.6f * smoothstep(0.0f, 4.0f, input.worldPos.y - h) + step(1.0f, waveFactor)));
            }

             /**
             * Returns displacement direction
             */
            float2 getDisplacementVector(float3 displacement, float displacementFactor)
            {
                float2 displacementVector = float2(displacement.r, displacementFactor) * displacementFactor * 2 - 1;

                return displacementVector;
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

            //////////////////

            float nsin(float v)
            {
                return (sin(v) + 1.0f) / 2.0f;
            }
            float ncos(float v)
            {
                return (cos(v) + 1.0f) / 2.0f;
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
            float nsnoise(float2 v)
            {
                return (snoise(v) + 1.0f) / 2.0f;
            }
            ENDHLSL
        }
    }
}
