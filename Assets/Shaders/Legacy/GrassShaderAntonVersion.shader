Shader "Unlit/GrassShader"
{
    Properties
    {
        _ShellDistance ("Distance between shell layers", Float) = 0.0
        _Layer ("Layer number", Int) = 0
        _MaxLayer ("Highest layer number", Int) = 0
        
        _GrassMultiplier ("Scale underlying noise for grass gen", Float) = 100.0 
        _GrassDensity ("Density of grass", Float) = 0.5
        
        _WindDirection ("Wind direction", Vector) = (0.0, 0.0, 0.0, 0.0)
        _WindForce ("Wind force", Float) = 0.0
        _WindFrequency ("Wind gust frequency", Float) = 0.0
        _WindSize ("Wind gust area size", Float) = 0.0
        
        _GroundTexture ("Texture of bottom layer", 2D) = "white" {}
        _GrassTexture ("Texture of grass layers", 2D) = "white" {}
        
        _LightColor ("Color of light", Vector) = (1.0, 1.0, 1.0, 1.0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "LightMode"="ForwardBase" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityShaderVariables.cginc"
            #include "UnityCG.cginc"

            float3 getColor(float2 st, float2 world);
            float3 ambient();
            float3 diffuse(float3 normal);
            float2 worldToGrassPlanePos(float2 worldPlanePos, float windFactor);
            float calcWindFactor(float2 worldPlanePos);
            float genAlpha(float2 st);
            float layerFactor();

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
            
            struct VertexAttributes
            {
                float4 pos : POSITION;
                float4 nor : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct FragmentAttributes
            {
                float4 clipPos : POSITION;
                float4 worldPos : TEXCOORD0;
                float3 worldNor : TEXCOORD1;
                float2 st : TEXCOORD2;
            };

            float _ShellDistance;
            int _Layer;
            int _MaxLayer;

            float _GrassMultiplier;
            float _GrassDensity;

            float2 _WindDirection;
            float _WindForce;
            float _WindFrequency;
            float _WindSize;
            
            sampler2D _GroundTexture;
            sampler2D _GrassTexture;

            float4 _LightColor;
            
            FragmentAttributes vert (VertexAttributes input)
            {
                // The output for the fragment stage.
                FragmentAttributes output;

                // Create the layers
                float4 modelPos = input.pos + input.nor * _ShellDistance * _Layer;
                modelPos.w = 1.0f;
                
                output.worldPos = mul(unity_ObjectToWorld, modelPos);
                output.clipPos = UnityObjectToClipPos(modelPos);

                output.worldNor = normalize(UnityObjectToWorldNormal(input.nor.xyz));
                
                output.st = input.uv;
                
                return output;
            }
            
            float4 frag (FragmentAttributes input) : SV_Target
            {
                float4 zeroVec = float4(0.0f, 0.0f, 0.0f, 0.0f);
                float4 oneVec = float4(1.0f, 1.0f, 1.0f, 1.0f);

                float2 worldPlanePos = input.worldPos.xz;
                float windFactor = calcWindFactor(worldPlanePos);
                float2 grassPlanePos = worldToGrassPlanePos(worldPlanePos, windFactor);
                float alpha = genAlpha(grassPlanePos);

                float3 color = (ambient() + diffuse(input.worldNor)) * getColor(worldPlanePos * 0.1f, worldPlanePos * 0.1f);
                // color *= 1.0f + 0.3f * layerFactor() * windFactor;
                // color *= 0.3f + smoothstep(0.0f, 1.0f, layerFactor()) * 0.7f;

                // color += windFactor;
                color = clamp(color, zeroVec.xyz, oneVec.xyz);

                return float4(color, alpha);
            }

            float3 getColor(float2 st, float2 world)
            {
                const float2 offset = floor(st) + 5.0f;
                const float2 rotatedST000 = st - offset;
                const float2 rotatedST090 = float2(-st.y, st.x);
                const float2 rotatedST180 = float2(-st.x, -st.y);
                const float2 rotatedST270 = float2(st.y, -st.x);

                const float factor = hash42(floor(world)) + _WindFrequency;
                const float a = step(0.0f, factor);
                const float b = step(0.25f, factor);
                const float c = step(0.5f, factor);
                const float d = step(0.75f, factor);
                float2 coord = rotatedST000 * (a*b*c*d) +
                               rotatedST090 * (a*b*c*(1.0f-d)) +
                               rotatedST180 * (a*b*(1.0f-c)*(1.0f-d)) +
                               rotatedST270 * (a*(1.0f-b)*(1.0f-c)*(1.0f-d));
                coord = coord + offset;
                
                // float3 grass = factor;
                float3 grass = tex2D(_GrassTexture, coord);
                float3 ground = tex2D(_GroundTexture, st);

                float isGround = step(_Layer, 0.0f);
                return ground * isGround + grass * (1.0f - isGround);
            }
            
            float3 ambient()
            {
                return float3(0.5f, 0.5f, 0.5f);
            }
            
            float3 diffuse(float3 normal)
            {
                return _LightColor.rgb * 1.5f * dot(_WorldSpaceLightPos0, normal) * layerFactor();
            }

            float2 worldToGrassPlanePos(float2 worldPlanePos, float windFactor)
            {
                return worldPlanePos * _GrassMultiplier + -_WindDirection * windFactor * _WindForce * layerFactor();
            }

            float calcWindFactor(float2 worldPlanePos)
            {
                float2 windPlanePos = worldPlanePos + _WindDirection * _Time.y;
                return snoiseNormalized(windPlanePos * _WindSize);
            }

            float genAlpha(float2 st)
            {
                float isGround = step(_Layer, 0.0f);
                return min(noise(st) + isGround, 1.0f);
            }

            float layerFactor()
            {
                float t = (float(_Layer) / _MaxLayer);
                return pow(t, 3);
            }

            // float random (float2 st) {
            //     float rng = frac(sin(dot(st.xy, float2(12.9898,78.233))) * 43785.245145);
            //     return rng * step(rng, _GrassDensity);
            // }

            uint baseHash(uint2 p)
            {
                p = 1103515245U*((p >> 1U)^(p.yx));
                uint h32 = 1103515245U*((p.x)^(p.y>>3U));
                return h32^(h32 >> 16);
            }

            float4 hash42(float2 x)
            {
                uint n = baseHash(asuint(x));
                uint4 rz = uint4(n, n*16807U, n*48271U, n*69621U);
                return float4((rz >> 1) & uint4((0x7fffffffU).xxxx))/float(0x7fffffff);
            }
            
            float random2 (float2 st) {
                // return frac(sin(dot(st.xy, float2(12.9898,78.233))) * 43758.545);
                return hash42(st).x;
            }
            
            float random(float2 st)
            {
                fixed r = random2(st);
                float b1 = step(r, _GrassDensity);
                float b2 = step(_GrassDensity + 1.0f, r);
                return 1.0f * b1 + (0.0f * b2 + r * (1.0f - b2)) * (1.0f - b1);
            }
            
            float noise (in float2 st) {
                float2 i = floor(st);
                float2 f = frac(st);

                // Four corners in 2D of a tile
                float a = random(i).x;
                float b = random(i + float2(1.0, 0.0)).x;
                float c = random(i + float2(0.0, 1.0)).x;
                float d = random(i + float2(1.0, 1.0)).x;
                
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
            // snoise, but returns values from 0.0 -> 1.0
            float snoiseNormalized(float2 v)
            {
                return (snoise(v) + 1.0f) / 2.0f;
            }
            ENDHLSL
        }
    }
}
