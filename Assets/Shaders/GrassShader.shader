Shader "Unlit/GrassShader"
{
    Properties
    {
        _ShellDistance ("Distance between shell layers", Float) = 0.0
        _Layer ("Layer number", Int) = 0
        _MaxLayer ("Highest Layer number", Int) = 0 // (i.e. if 10 layers, max number is 9) 
        _LengthVariationSize ("Noise scalar for length variation", Float) = 0.0
        
        _GrassMultiplier ("Scale underlying noise for grass gen", Float) = 100.0
        _GrassDensity ("Density of grass", Float) = 0.5
        
        _WindDirection ("Wind direction", Vector) = (0.0, 0.0, 0.0, 0.0)
        _WindForce ("Wind force", Float) = 0.0
        _WindFrequency ("Wind gust frequency", Float) = 0.0
        _WindSize ("Wind gust area size", Float) = 0.0
        
        _GroundTexture ("Texture of bottom layer", 2D) = "white" {}
        _GrassTexture ("Texture of grass layers", 2D) = "white" {}
        _MaskTexture ("Texture of masking area", 2D) = "black" {}
        
        _DirtTexture ("Texture of dirt patch", 2D) = "white" {}
        _DirtNormal ("Normal texture of dirt patch", 2D) = "white" {}
        _DirtMask ("Mask", 2D) = "white" {}
        _PatchRadiusMin ("Dirt patch min radius", Float) = 2.0
        _PatchRadiusMax ("Dirt patch max radius", Float) = 2.0
        
        _LightColor ("Color of light", Vector) = (1.0, 1.0, 1.0, 1.0)
        
        _X ("X", Range(0.0, 100.0)) = 0.0
        _Y ("Y", Range(0.0, 100.0)) = 0.0
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "LightMode" = "ForwardBase"}
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
            
            float3 getColor(float2 st);
            float3 ambient();
            float3 diffuse(float3 normal, float attenuation);
            float2 worldToGrassPlanePos(float2 worldPlanePos, float windFactor);
            float calcWindFactor(float2 worldPlanePos);
            float genAlpha(float2 st, float strawLength);
            float layerFactor();
            float layerFactorLinear();
            
            float4 hash42(float2 x);
            float random2 (float2 st);
            float random(float2 st);
            float noise (in float2 st);
            float fbm (in float2 st);
            float3 mod289(float3 x);
            float2 mod289(float2 x);
            float3 permute(float3 x);
            float snoise(float2 v);
            float snoiseNormalized(float2 v);

            struct FragmentAttributes
            {
                float4 pos : POSITION;
                float4 worldPos : TEXCOORD0;
                float3 worldNor : TEXCOORD1;
                float2 st : TEXCOORD2;
                nointerpolation float2 patchData1 : TEXCOORD3;
                nointerpolation float2 patchData2 : TEXCOORD4;
                nointerpolation float2 rng : RANDOM;
                // nointerpolation float2 patchData3 : TEXCOORD5;
                float3x3 tbn : MATRIX;
                LIGHTING_COORDS(6,7)
            };

            float _ShellDistance;
            int _Layer;
            int _MaxLayer;

            float _GrassMultiplier;
            float _GrassDensity;
            float _LengthVariationSize;

            float2 _WindDirection;
            float _WindForce;
            float _WindFrequency;
            float _WindSize;

            sampler2D _GroundTexture;
            sampler2D _GrassTexture;
            sampler2D _MaskTexture;
            
            sampler2D _DirtTexture;
            sampler2D _DirtNormal;
            sampler2D _DirtMask;
            
            float _PatchRadiusMin;
            float _PatchRadiusMax;
            
            float4 _LightColor;

            
            
            FragmentAttributes vert (appdata_full v)
            {
                // The output struct for the fragment stage.
                FragmentAttributes output;
                output.patchData1 = v.texcoord1;
                output.patchData2 = v.texcoord2;
                output.rng = v.texcoord3;
                
                // Create the layers
                float4 modelPos = v.vertex + float4(v.normal, 0.0f) * _ShellDistance * _Layer;
                modelPos.w = 1;
                
                output.worldPos = mul(modelPos, unity_ObjectToWorld);
                output.pos = UnityObjectToClipPos(modelPos);

                output.worldNor = normalize(UnityObjectToWorldNormal(v.normal.xyz));

                output.st = v.texcoord;

                float3 tangent = v.tangent.xyz * v.tangent.w;
                float3 bitangent = cross(v.normal, v.tangent.xyz) * v.tangent.w;
                float3 normal = v.normal;

                float3 T = normalize(float3(UnityObjectToWorldDir(tangent)));
                float3 B = normalize(float3(UnityObjectToWorldDir(bitangent)));
                float3 N = normalize(float3(UnityObjectToWorldDir(normal)));
                output.tbn = transpose(float3x3(T, B, N));

                TRANSFER_VERTEX_TO_FRAGMENT(output);
                
                return output;
            }

            float4 frag (FragmentAttributes input) : SV_Target
            {
                const float PI = 3.1415927f;
                float dist1 = distance(input.patchData1, input.worldPos.xz);
                float dist2 = distance(input.patchData2, input.worldPos.xz);
                float dist1Closer = step(dist1, dist2);
                float rng = input.rng.x * dist1Closer + input.rng.y * (1.0f - dist1Closer);
                float patchRadius = lerp(_PatchRadiusMin, _PatchRadiusMax, rng);
                 
                float dist = min(dist1, dist2) / patchRadius;
                float2 patchPos = input.patchData1 * dist1Closer + input.patchData2 * (1.0f - dist1Closer);
                float2 toPatch = patchPos - input.worldPos.xz;
                float angle = (atan2(toPatch.y, toPatch.x) + PI) / (2 * PI);
                
                float2 patchUv = toPatch / patchRadius / 2.0f + 0.5f;
                
                float3 patchNormal = tex2D(_DirtNormal, patchUv/2.0f);
                patchNormal = normalize(mul(input.tbn, patchNormal));
                float3 patchColor = tex2D(_DirtTexture, input.worldPos.xz / 5.0f);
                float patchMask = tex2D(_DirtMask, float2(angle, dist));
                // patchMask = 0.0f;
                float mask = min(step(1.0f, dist) + patchMask, 1.0f);//tex2D(_MaskTexture, input.st).r;
                
                /// For clamping color //
                float4 zeroVec = float4(0.0f, 0.0f, 0.0f, 0.0f);
                float4 oneVec = float4(1.0f, 1.0f, 1.0f, 1.0f);
                ///
                
                float2 worldPlanePos = input.worldPos.xz; // For seamless wind over entire map
                float windFactor = calcWindFactor(worldPlanePos);
                float2 grassPlanePos = worldToGrassPlanePos(worldPlanePos, windFactor);

                float isGround = step(_Layer, 0.0f);
                
                float strawLength = snoiseNormalized(worldPlanePos * _LengthVariationSize) * 2.0f + 0.25f;
                float alpha = genAlpha(grassPlanePos, strawLength);
                alpha *= min(1.0f, mask + isGround);

                float attenuation = LIGHT_ATTENUATION(input);
                float inLight = step(1.0f, attenuation);
                float l = layerFactor();
                float3 amb = ambient() + windFactor * 0.15f;
                float3 color = (amb * inLight + (amb - (0.3f - l * 0.3f)) * (1.0f - inLight) + diffuse(input.worldNor, attenuation) * l) * getColor(worldPlanePos * 0.1f);

                patchColor = patchColor * amb + patchColor * diffuse(patchNormal, attenuation);
                
                // patchColor = patchColor * ();
                // To make sure color is between 0 and 1
                color = clamp(color, zeroVec, oneVec);

                color = color * mask + patchColor * (1.0f - mask);
                
                return float4(color, alpha);
            }

            /// Get color from bottom texture and fur texture and output. Outputs color from bottom texture if
            /// bottom layer. Outputs color from fur texture if not bottom layer. 
            float3 getColor(float2 st)
            {
                /// Sample from textures //
                float3 grass = tex2D(_GrassTexture, st);
                float3 ground = tex2D(_GroundTexture, st);
                ///

                float isGround = step(_Layer, 0.0f); // Check if ground layer
                return (ground * isGround) + grass * (1.0f - isGround);
            }

            /// Convert noise from world pos to grass plane pos (scale noise coordinates)
            float2 worldToGrassPlanePos(float2 worldPlanePos, float windFactor)
            {
                return worldPlanePos * _GrassMultiplier + -_WindDirection * windFactor * _WindForce * layerFactor();
            }

            float calcWindFactor(float2 worldPlanePos)
            {
                float2 windPlanePos = worldPlanePos + _WindDirection * _Time.y;
                float windFactor = snoiseNormalized(windPlanePos * _WindSize);

                return windFactor;
            }

            float3 ambient()
            {
                return (0.80f).xxx;
            }

            float3 diffuse(float3 normal, float attenuation)
            {
                return _LightColor.rgb * dot(_WorldSpaceLightPos0, normal) * attenuation;
            }

            float genAlpha(float2 st, float strawLength)
            {
                float isGround = step(_Layer, 0.0f);
                float isTransparent = step(strawLength, layerFactorLinear());
                return min((1 - isTransparent) * noise(st) + isGround, 1.0f);
            }

            float layerFactor()
            {
                float t = float(_Layer) / _MaxLayer;
                return pow(t, 3);
            }
            float layerFactorLinear()
            {
                float t = float(_Layer) / _MaxLayer;
                return t;
            }

            /// Hashing functions to remove artifacts //
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

            float random(float2 st)
            {
                float r = random2(st);
                float b1 = step(r, _GrassDensity);
                float b2 = step(_GrassDensity + 1.0f, r);
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
            
            ENDHLSL
        }
        
        Pass
        {
            Tags{ "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMain

            #include "UnityCG.cginc"
            
            float4 VSMain (float4 vertex:POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }
 
            float4 PSMain (float4 vertex:SV_POSITION) : SV_TARGET
            {
                if (unity_LightShadowBias.z != 0.0) discard;
                return 0;
            }

            ENDCG
        }
    }
//        Fallback "VertexLit"
}
