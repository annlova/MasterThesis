Shader "Unlit/FurShader"
{
    Properties
    {
        _ShellDistance ("Distance between shell layers", Float) = 0.0
        _Layer ("Layer number", Int) = 0
        _MaxLayer ("Highest Layer number", Int) = 0 // (i.e. if 10 layers, max number is 9) 
        
        _FurMultiplier ("Scale underlying noise for fur gen", Float) = 100.0
        _FurDensity ("Density of fur", Float) = 0.5
        
        _WindDirection ("Wind direction", Vector) = (0.0, 0.0, 0.0, 0.0)
        _WindForce ("Wind force", Float) = 0.0
        _WindFrequency ("Wind gust frequency", Float) = 0.0
        _WindSize ("Wind gust area size", Float) = 0.0
        
        _BottomTexture ("Texture of bottom layer", 2D) = "white" {}
        _FurTexture ("Texture of all fur layers, except for outmost", 2D) = "white" {}
        _OutmostLayerTexture ("Texture of outmost fur layer", 2D) = "white" {}
        _MaskTexture ("Texture of masking area", 2D) = "white" {}
        _EyeTexture ("Eye texture", 2D) = "white" {}
        _EyeMaskTexture ("Eye texture of masking area", 2D) = "white" {}
        
        _LightColor ("Color of light", Vector) = (1.0, 1.0, 1.0, 1.0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "LightMode" = "ForwardBase" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityShaderVariables.cginc"
            #include "UnityCG.cginc"

            #pragma multi_compile_fwdbase
            #include "AutoLight.cginc"
            
            float3 getColor(float3 bottom, float3 fur, float3 outmostFur);
            float3 ambient();
            float3 diffuse(float3 normal, float3 lightDir, float3 mask);
            float2 textureToFurCoords(float2 worldPlanePos, float windFactor);
            float calcWindFactor(float2 worldPlanePos);
            float genAlpha(float2 stTex, float3 mask);
            float layerFactor();
            float isBlinking(float start, float end);
            
            float random2 (float2 st);
            float random(float2 st);
            float noise (in float2 st);
            float fbm (in float2 st);
            float3 mod289(float3 x);
            float2 mod289(float2 x);
            float3 permute(float3 x);
            float snoise(float2 v);
            float snoiseNormalized(float2 v);
            float nsin(float val);

            struct FragmentAttributes
            {
                float4 pos : POSITION;
                float4 worldPos: TEXCOORD0;
                float3 worldNor : TEXCOORD1;
                float2 st : TEXCOORD2;
                LIGHTING_COORDS(3,4)
            };

            float _ShellDistance;
            int _Layer;
            int _MaxLayer;

            float _FurMultiplier;
            float _FurDensity;

            float2 _WindDirection;
            float _WindForce;
            float _WindFrequency;
            float _WindSize;

            sampler2D _BottomTexture;
            sampler2D _FurTexture;
            sampler2D _OutmostLayerTexture;
            sampler2D _MaskTexture;
            sampler2D _EyeTexture;
            sampler2D _EyeMaskTexture;

            float4 _LightColor;
            
            FragmentAttributes vert (appdata_base v)
            {
                // The output struct for the fragment stage.
                FragmentAttributes output;

                // Create the layers
                float4 modelPos = v.vertex + float4(v.normal * _ShellDistance * _Layer, 0.0f);
                modelPos.w = 1;
                
                output.worldPos = mul(unity_ObjectToWorld, modelPos);
                output.pos = UnityObjectToClipPos(modelPos);

                output.worldNor = normalize(UnityObjectToWorldNormal(v.normal.xyz));

                output.st = v.texcoord;

                TRANSFER_VERTEX_TO_FRAGMENT(output);
                
                return output;
            }

            float4 frag (FragmentAttributes input) : SV_Target
            {
                float2 eyeSt = float2(input.st.x - 65.0f / 256.0f, input.st.y - 74.0f / 256.0f); // Offset for eye texture
                eyeSt.x /= 16.0f / 256.0f;
                eyeSt.y /= 32.0f / 256.0f;
                eyeSt.x /= 3;
                
                // eyeSt.x += isBlinking(0.25f, 0.28f, 0.62f, 0.65f, 0.68f, 0.71f) * 0.5;
                eyeSt.x += min(isBlinking(0.25f, 0.26f) + isBlinking(0.62f, 0.63f) + isBlinking(0.68f, 0.69f), 1.0f) * 256.0f / 3.0f; // half-closed eye (down-way)
                eyeSt.x += min(isBlinking(0.26f, 0.27f) + isBlinking(0.63f, 0.64f) + isBlinking(0.69f, 0.70f), 1.0f) * 256.0f / 1.5f; // closed eye
                eyeSt.x += min(isBlinking(0.27f, 0.28f) + isBlinking(0.64f, 0.65f) + isBlinking(0.70f, 0.71f), 1.0f) * 256.0f / 3.0f; // half-closed eye (up-way)
                
                int isEyeX1 = step(65.0f / 256.0f, input.st.x);
                int isEyeX2 = step(input.st.x, 81.0f / 256.0f);
                int isEyeX = isEyeX1 * isEyeX2;
                
                int isEyeY1 = step(74.0f / 256.0f, input.st.y);
                int isEyeY2 = step(input.st.y, 106.0f / 256.0f);
                int isEyeY = isEyeY1 * isEyeY2;
                
                int isEye = isEyeX * isEyeY;

                
                /// Sample from textures //
                float3 bottom = tex2D(_BottomTexture, input.st);
                float3 fur = tex2D(_FurTexture, input.st);
                float3 outmostFur = (1 - isEye) * tex2D(_OutmostLayerTexture, input.st) +
                    isEye * tex2D(_EyeTexture, eyeSt);
                float3 mask = (1 - isEye) * tex2D(_MaskTexture, input.st) +
                    isEye * tex2D(_EyeMaskTexture, eyeSt);
                ///

                /// Calculate if in shadow //
                float attenuation = LIGHT_ATTENUATION(input);
                /// 
                

                /// For clamping color //
                float4 zeroVec = float4(0.0f, 0.0f, 0.0f, 0.0f);
                float4 oneVec = float4(1.0f, 1.0f, 1.0f, 1.0f);
                ///
                
                float windFactor = calcWindFactor(1 - isEye) * input.st + isEye * eyeSt;
                float2 furPlanePos = textureToFurCoords(input.st, windFactor);
                
                float alpha = genAlpha(furPlanePos, mask);

                float3 color = (ambient() + diffuse(input.worldNor,
                    normalize(_WorldSpaceCameraPos - input.worldPos),
                    mask) * attenuation) * getColor(bottom, fur, outmostFur);
                
                // To make sure color is between 0 and 1
                color = clamp(color, zeroVec, oneVec);

                return float4(color, alpha);
            }
            
            float3 getColor(float3 bottom, float3 fur, float3 outmostFur)
            {
                float isBottom = step(_Layer, 0);
                float isFur = step(_Layer, _MaxLayer - 1) * step(1, _Layer);
                float isOutermostLayer = step(_MaxLayer, _Layer);
                
                return bottom * isBottom + fur * isFur + outmostFur * isOutermostLayer;
            }
            
            float2 textureToFurCoords(float2 worldPlanePos, float windFactor)
            {
                return worldPlanePos * _FurMultiplier + -_WindDirection * windFactor * _WindForce * layerFactor();
            }
            
            float calcWindFactor(float2 worldPlanePos)
            {
                float2 windPlanePos = worldPlanePos + _WindDirection * _Time.y;
                float windFactor = snoiseNormalized(windPlanePos * _WindSize);

                return windFactor;
            }

            float3 ambient()
            {
                return float3(0.5f, 0.5f, 0.5f);
            }
            
            float3 diffuse(float3 normal, float3 lightDir, float3 mask)
            {
                return _LightColor.rgb * dot(lightDir, normal)* min(layerFactor() + (1 - mask.g), 1.0f);
            }

            float genAlpha(float2 stFur, float3 mask)
            {
                float isBottom = step(_Layer, 0.0f);
                float isOutermostLayer = step(_MaxLayer, _Layer);
                return min((noise(stFur)/3 + 0.1) * mask.g + (1 - mask.r) + isBottom + isOutermostLayer * mask.b, 1.0f);
            }

            float layerFactor()
            {
                float t = float(_Layer) / _MaxLayer;
                return pow(t, 1.2);
            }

            float isBlinking(float start, float end)
            {
                float timer = frac(_Time.y/4.5f);
                float lowCheck = step(start, timer);
                float highCheck = step(timer, end);
                
                return lowCheck * highCheck;
            }

            float random2 (float2 st) {
                return frac(sin(dot(st.xy, float2(12.9898,78.233))) * 43758.5453123);
            }

            float random(float2 st)
            {
                float r = random2(st);
                float b1 = step(r, _FurDensity);
                float b2 = step(_FurDensity + 1.0f, r);
                return 1.0f * b1 + (0.0f * b2 + r * (1.0f - b2)) * (1.0f - b1);
            }
            
            float noise (in float2 st) {
                float2 i = floor(st);
                float2 f = frac(st);

                // Four corners in 2D of a tile
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));
                
                // Smooth Interpolation

                // Cubic Hermine Curve.  Same as SmoothStep()
                float2 u = f*f*(3.0-2.0*f);
                // u = smoothstep(0.,1.,f);

                // Mix 4 coorners percentages
                return lerp(a, b, u.x) +
                        (c - a)* u.y * (1.0 - u.x) +
                        (d - b) * u.x * u.y;
            }

            float fbm (in float2 st) {
                // Initial values
                float value = 0.0;
                float amplitude = .5;
                float frequency = 0.;
                //
                // Loop of octaves
                for (int i = 0; i < 8; i++) {
                    value += amplitude * noise(st);
                    st *= 2.;
                    amplitude *= .5;
                }
                return value;
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

            float nsin(float val)
            {
                return (sin(val) + 1)/2;
            }
            
            ENDHLSL
        }
    }
    
    Fallback "VertexLit"
}
