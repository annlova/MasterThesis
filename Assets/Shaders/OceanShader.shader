Shader "Unlit/OceanShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityShaderVariables.cginc"
            #include "UnityCG.cginc"

            float nsin(float v);
            float ncos(float v);
            float snoise(float2 v);
            float nsnoise(float2 v);
            
            struct VertexAttributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 beachHeights : TEXCOORD1;
            };

            struct FragmentAttributes
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float heightMap : HEIGHTMAP;
                float4 tideHeight : TIDE;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            FragmentAttributes vert (VertexAttributes input)
            {
                FragmentAttributes o;
                float tideChange = 0.15f;
                float PI = 3.1415927;                             
                float time = _Time.y * 0.2f;            
                float waveTime = frac(time) * PI * 2.0f;       

                float animationTimeSkew = 0.3f;
                float waveIn = -1.0f + smoothstep(0.0f, animationTimeSkew, frac(time)) * 2.0f;
                float waveOut = 1.0f - smoothstep(animationTimeSkew, 1.0f, frac(time)) * 2.0f;
                float goingOut = step(animationTimeSkew, frac(time));
                float tideOffset = (waveIn * (1.0f - goingOut) + waveOut * goingOut) * tideChange;
                // float tideOffset = cos(waveTime) * tideChange;
                o.tideHeight = float4(-0.8f + tideOffset, -0.8f - tideChange, -0.8f + tideChange, 0.0f);
                
                float4 modelPos = input.vertex + float4(0.0f, tideOffset, 0.0f, 0.0f);
                o.worldPos = mul(unity_ObjectToWorld, modelPos);
                o.vertex = UnityObjectToClipPos(modelPos);
                
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);

                float beachPosY = -0.3f;
                o.heightMap = input.beachHeights.x + beachPosY;
                return o;
            }

            float4 frag (FragmentAttributes input) : SV_Target
            {
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

                float foamEdgeThicknessMin = 0.05f;
                // float foamEdgeThicknessMax = smoothstep(input.tideHeight.y, input.tideHeight.z, input.worldPos.y) * 0.3f;
                float foamEdgeThicknessMax = (cos(waveTime) + 1.0f) / 2.0f * 0.3f;
                float foam = nsnoise(input.worldPos.xz) * foamEdgeThicknessMax + foamEdgeThicknessMin;
                float waveFactor = step(input.worldPos.y - foam, h);
                float a = lerp(0.0f, PI * 1.7f, frac(_Time.y * 0.2f));
                waveFactor = max(step(0.995f, ncos(h - a)), waveFactor);
                float3 waveCol = lerp(skyColor, white, waveFactor);
                return float4(waveCol, 1.0f);
            }

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
