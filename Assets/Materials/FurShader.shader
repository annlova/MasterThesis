Shader "Unlit/FurShader"
{
    Properties
    {
        _FurTexture ("Texture", 2D) = "white" {}
        _FurTextureBottom ("TextureBottom", 2D) = "white" {}
        _FurLength ("Fur Length", Float) = 0.0
        _UVScale ("UV Scale", Float) = 1.0
        _Layer ("Layer", Float) = 0.0 // 0 to 1 for the level
        _VGravity ("Gravity float3", Vector) = (0,-2.0,0,0)
        _Thickness("Hair Thickness", Float) = 0.5
        _Falloff("Hair Length Falloff factor", Float) = 10.0
        _HairAmount("Number of Hair Strands", Float) = 1000.0
        _ColorVariation("Color Variation", Float) = 0.6
        _Glitter("Glitter", Float) = 50.0
    }
    SubShader
    {
//        Tags {"RenderType"="Opaque"}
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite off
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

            struct vertexInput
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float2 texCoordDiffuse : TEXCOORD0;
            };

            struct vertexOutput
            {
                float4 HPOS : POSITION;
                float4 WORLD_POS : TEXCOORD1;
                float2 T0 : TEXCOORD0; // fur alpha
                UNITY_FOG_COORDS(1)
                float3 normal : NORMAL;
            };

            float _FurLength;
            float _UVScale;
            float _Layer;
            float3 _VGravity;
            
            sampler2D _FurTexture;
            float4 _FurTexture_TexelSize;
            float4 _FurTexture_ST;
            sampler2D _FurTextureBottom;
            float4 _FurTextureBottom_ST;

            float _Thickness;
            float _Falloff;
            float _HairAmount;
            float _ColorVariation;

            float _Glitter;
            
            vertexOutput vert (vertexInput IN)
            {
                vertexOutput OUT;
                
                // OUT.T0 = TRANSFORM_TEX(IN.texCoordDiffuse, _FurTexture);

                //** MAIN LINE ** MAIN LINE ** MAIN LINE ** MAIN LINE ** MAIN LINE **//
                //** MAIN LINE ** MAIN LINE ** MAIN LINE ** MAIN LINE ** MAIN LINE **//
                //This single line is responsible for creating the layers!  This is it! Nothing
                //more nothing less!
                float3 P = IN.position.xyz + (IN.normal * _FurLength);

                //Modify our normal so it faces the correct direction for lighting if we
                //want any lighting
                float3 normal = normalize(UnityObjectToWorldNormal(IN.normal));

                // Couple of lines to give a swaying effect!
                // Additional Gravit/Force Code
                _VGravity = mul(_VGravity, UNITY_MATRIX_M);
                float k =  pow(_Layer, 3);  // We use the pow function, so that only the tips of the hairs bend
                                           // As layer goes from 0 to 1, so by using pow(..) function is still
                                           // goes form 0 to 1, but it increases faster! exponentially

                P = P + _VGravity*k;
                // End Gravity Force Addit Code

                OUT.T0 = IN.texCoordDiffuse * _UVScale; // Pass long texture data
                // UVScale??  Well we scale the fur texture alpha coords so this effects the fur thickness
                // thinness, sort of stretches or shrinks the fur over the object!

                // OUT.HPOS = mul(float4(P, 1.0f), UNITY_MATRIX_MVP); // Output Vertice Position Data
                OUT.WORLD_POS = mul(unity_ObjectToWorld, float4(P, 1.0f));
                OUT.HPOS = UnityObjectToClipPos(P);
                OUT.normal = normal; // Output Normal

                // UNITY_TRANSFER_FOG(o, o.vertex);
                
                return OUT;
            }

            float random2 (float2 st) {
                return frac(sin(dot(st.xy, float2(12.9898,78.233))) * 43758.5453123);
            }

            float random(float2 st)
            {
                float r = random2(st);
                float b1 = step(r, _Thickness);
                float b2 = step(_Thickness + 1.0f, r);
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

            //
            // Description : GLSL 2D simplex noise function
            //      Author : Ian McEwan, Ashima Arts
            //  Maintainer : ijm
            //     Lastmod : 20110822 (ijm)
            //     License :
            //  Copyright (C) 2011 Ashima Arts. All rights reserved.
            //  Distributed under the MIT License. See LICENSE file.
            //  https://github.com/ashima/webgl-noise
            //
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
            
            float4 frag (vertexOutput IN) : COLOR
            {
                // sample the texture
                // float4 col = tex2D(_MainTex, i.uv);
                // apply fog
                // return col;

                float hairLimit = _Thickness + _Layer * _Falloff;
                float2 st = IN.T0;
                float2 stSame = IN.T0 + float2(1.0f, 1.0f) * (_Time.y * 0.1f);
                float2 stUp = stSame + float2(0.0f, 1.0f) * _FurTexture_TexelSize.y;
                float2 stRight = stSame + float2(1.0f, 0.0f) * _FurTexture_TexelSize.x;                
                float2 stShadowHair = st + _WorldSpaceLightPos0.xz * _FurTexture_TexelSize.xy;
                st *= _HairAmount;
                stSame *= _Glitter;
                stUp *= _Glitter;
                stRight *= _Glitter;
                stShadowHair *= _HairAmount;
                // float2 ipos = floor(st);  // get the integer coords
                // float2 fpos = frac(st);  // get the fractional coords
                float hair = noise(st);
                float pA = (snoise(stSame) + 1.0f) / 2.0f * 1.0f;
                float pB = (snoise(stUp) + 1.0f) / 2.0f * 1.0f;
                float pC = (snoise(stRight) + 1.0f) / 2.0f * 1.0f;
                float3 vA = float3(stUp.x, pB, stUp.y) - float3(stSame.x, pA, stSame.y);
                float3 vB = float3(stRight.x, pC, stRight.y) - float3(stSame.x, pA, stSame.y);
                float3 snoiseNormal = normalize(cross(vA, vB));
                
                float shadowHair = noise(stShadowHair);
                shadowHair = max((shadowHair - 0.3f), 0.0f);
                // shadowHair *= 0.1f;
                // float isHair = step(hairLimit, hair);
                
                //--------------------------
                //
                //Basic Directional Lighting
                //float4(0.66f, 0.33f, 0.21f, 0.0f)
                float4 zeroVec = float4(0.0f, 0.0f, 0.0f, 0.0f);
                float4 oneVec = float4(1.0f, 1.0f, 1.0f, 0.0f);
                float variation = 0.1f * _ColorVariation;
                float4 color = tex2D(_FurTexture, IN.T0);//* lerp(1.0 - variation, 1.0f, hair);// * clamp(random(ipos + 5.0f), _ColorVariation, 1.0f);
                color.r += lerp(-variation, variation, hair);
                color.g += lerp(-variation, variation, hair);
                color.b += lerp(-variation, variation, hair);
                color = clamp(color, zeroVec, oneVec);
                float4 ambient = {0.5f, 0.5f, 0.5f, 0.0f};
                float4 diffuse = float4(1.0f, 1.0f, 1.0f, 0.0f) * dot(_WorldSpaceLightPos0, IN.normal);
                diffuse = clamp(diffuse, zeroVec, oneVec);

                float specularStrength = 2.f;
                float3 viewDir = normalize(IN.WORLD_POS - _WorldSpaceCameraPos);
                float3 reflectDir = reflect(-_WorldSpaceLightPos0, snoiseNormal);
                float spec = pow(max(dot(viewDir, reflectDir), 0.0f), 16);
                float4 specular = specularStrength * spec * oneVec;
                
                float4 FinalColour = (ambient + diffuse + specular) * color;
                // FinalColour.g = step(shadowHair, 0.5f) * 0.5f;
                // FinalColour -= float4(shadowHair, shadowHair, shadowHair, 0.0f);
                FinalColour = clamp(FinalColour, zeroVec, oneVec);

                // FinalColour = float4(shadowHair, shadowHair, shadowHair, 0.0f);
                // FinalColour = float4(specular.xyz, 1.0f);
                
                float isBottomLayer = step(_Layer, 0.0f);
                float4 bottomColor = tex2D(_FurTextureBottom, IN.T0);
                FinalColour = isBottomLayer * bottomColor + (1.0f - isBottomLayer) * FinalColour;
                float layer = _Layer * (0.03f / 1.0f);
                FinalColour.a = clamp(max((hair), 0.0f) + isBottomLayer, 0.0f, 1.0f);
                //End Basic Lighting Code    
                //-------------------------
                
                //FinalColour.a = f + FurColour.a * (1.0f -f);
                // FinalColour.a *= 1.0 - _Layer * 20;
                //return FinalColour;      // fur colour only!
                // UNITY_APPLY_FOG(i.fogCoord, FinalColour);
                return FinalColour;       // Use texture colour
                // return float4(0,0,0,0); // Use for totally invisible!  Can't see
            }

            ENDHLSL
        }
    }
}
