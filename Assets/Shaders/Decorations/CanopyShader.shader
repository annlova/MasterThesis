Shader "Unlit/CanopyShader"
{
    Properties
    {
        _MainTex ("Cut Off Texture", 2D) = "white" {}
        _Scale ("Scale", Float) = 1.0
        _Inflate ("Inflation", Float) = 0.0
        _Rotate ("Rotation Degrees", Float) = 0.0
        _WindStrength ("Wind Strength", Float) = 1.0
        _Diffuse ("Diffuse Color", Color) = (0.5, 0.8, 0.5)
        _Smoothness ("Smoothness", Float) = 1.0
        _Specular ("Specular Color", Color) = (0.5, 0.8, 0.5)
        _FresnelColor ("Fresnel Color", Color) = (0.5, 0.8, 0.5)
        _FresnelPower ("Fresnel Power", Int) = 16
        _ColorRandomizer ("Color Randomizer", 2D) = "white" {}
        _Blend ("Blend", Range(0.0, 1.0)) = 1.0
        _CutOff ("Cut Off", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="Opaque" }
        ZWrite On
        Blend Off
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

            struct VertexAttributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float2 rng : TEXCOORD1;
            };

            struct FragmentAttributes
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float3 worldNor : NORMAL;
                float rng : RANDOM;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Scale;
            float _Inflate;
            float _Rotate;
            float _WindStrength;
            float4 _Diffuse;
            float _Smoothness;
            float4 _Specular;
            float4 _FresnelColor;
            int _FresnelPower;
            sampler2D _ColorRandomizer;
            float4 _ColorRandomizer_ST;
            float _Blend;
            float _CutOff;

            void Unity_Remap_float2(float2 In, float2 InMinMax, float2 OutMinMax, out float2 Out)
            {
                Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
            }

            void Unity_Add_float4(float4 A, float4 B, out float4 Out)
            {
                Out = A + B;
            }
            
            void Unity_Multiply_float4_float4x4(float4 A, float4x4 B, out float4 Out)
            {
                Out = mul(A, B);
            }

            void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
            {
                Out = A * B;
            }

            void Unity_Normalize_float4(float4 In, out float4 Out)
            {
                Out = normalize(In);
            }

            void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
            {
                Out = lerp(A, B, T);
            }

            void Unity_Rotate_Degrees_float(float2 UV, float2 Center, float Rotation, out float2 Out)
            {
                Rotation = Rotation * (3.1415926f/180.0f);
                UV -= Center;
                float s = sin(Rotation);
                float c = cos(Rotation);
                float2x2 rMatrix = float2x2(c, -s, s, c);
                rMatrix *= 0.5;
                rMatrix += 0.5;
                rMatrix = rMatrix * 2 - 1;
                UV.xy = mul(UV.xy, rMatrix);
                UV += Center;
                Out = UV;
            }

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

            float random2(float2 st) {
                return hash42(st).x;
            }
            
            FragmentAttributes vert (VertexAttributes i)
            {
                FragmentAttributes o;

                o.worldNor = UnityObjectToWorldNormal(i.normal);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                
                // Vertex Texcoord
                float2 uv = i.uv;
                
                // Remap
                float2 oldMinMax = float2(0.0f, 1.0f);
                float2 newMinMax = float2(-1.0f, 1.0f);
                float2 uvRemapped;
                Unity_Remap_float2(uv, oldMinMax, newMinMax, uvRemapped);
                
                // Rotate
                float2 uvRotated;
                float rng = random2(i.vertex.xz);
                float rotation = (sin(_Time.w * 10.0f * rng) + sin(_Time.w + rng)) / 2.0f * _WindStrength * rng;
                rotation += _Rotate;
                // float rotation = _Rotate * rng * 90.0f;
                Unity_Rotate_Degrees_float(uvRemapped, float2(0.0f, 0.0f), rotation, uvRotated);

                // float4 uv4Remapped = float4(uvRotated, 0.0f, 0.0f);
                float3 uv4Remapped = float3(uvRotated, 0.0f);
                
                // View matrix
                // float4x4 view = unity_WorldToCamera;
                float3x3 view = UNITY_MATRIX_V;

                // Multiply
                // float4 uvView;
                // Unity_Multiply_float4_float4x4(uv4Remapped, view, uvView);
                float3 uvView;
                uvView = mul(uv4Remapped, view);

                // Object to world matrix
                // float4x4 objToWorld = unity_ObjectToWorld;
                float3x3 objToWorld = UNITY_MATRIX_M;

                // Multiply
                // float4 uvWorld;
                // Unity_Multiply_float4_float4x4(uvView, objToWorld, uvWorld);
                float3 uvWorld;
                uvWorld = mul(uvView, objToWorld);

                // Normalize
                float3 objectScale = float3(length(float3(objToWorld[0].x, objToWorld[1].x, objToWorld[2].x)),
                                            length(float3(objToWorld[0].y, objToWorld[1].y, objToWorld[2].y)),
                                            length(float3(objToWorld[0].z, objToWorld[1].z, objToWorld[2].z)));
                // float4 uvNormalized;
                // Unity_Normalize_float4(float4(uvWorld.xyz * objectScale, 0.0f), uvNormalized);
                float3 uvNormalized;
                uvNormalized = normalize(uvWorld * objectScale);

                // Scale
                // float4 uvScaled;
                // Unity_Multiply_float4_float4(_Scale, uvNormalized, uvScaled);
                float3 uvScaled;
                uvScaled = _Scale * uvNormalized;

                // Inflate
                // float4 inflation;
                // Unity_Multiply_float4_float4(float4(o.worldNor, 0.0f), _Inflate, inflation);
                float3 inflation;
                inflation = mul(o.worldNor, _Inflate);

                // Add
                // float4 uvInflated;
                // Unity_Add_float4(inflation, uvScaled, uvInflated);
                float3 uvInflated;
                uvInflated = inflation + uvScaled;
                
                // Lerp
                // float4 offset;
                // Unity_Lerp_float4((0.0f).xxxx, uvInflated, _Blend, offset);
                float3 offset;
                offset = lerp((0.0f).xxx, uvInflated, _Blend);

                float4 localPos = i.vertex + float4(offset, 0.0f);
                o.worldPos = mul(localPos, unity_ObjectToWorld);
                o.vertex = UnityObjectToClipPos(localPos);

                o.rng = i.rng.x;
                
                return o;
            }

            void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
            {
                Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
            }
            
            float4 frag (FragmentAttributes i) : SV_Target
            {
                float4 cutOffTex = tex2D(_MainTex, i.uv);
                clip(cutOffTex.r - _CutOff);

                // View Direction
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                
                // Diffuse term
                float diffuse = dot(i.worldNor, _WorldSpaceLightPos0);
                // Randomize diffuse color
                float3 diffuseColor = _Diffuse * lerp(0.5f, 1.5f, i.rng);
                
                // Specular term
                float specularStrength = _Smoothness;
                float3 reflectDir = reflect(-_WorldSpaceLightPos0.xyz, i.worldNor);
                float spec = pow(max(dot(viewDir, reflectDir), 0.0f), 32) * specularStrength;
                // Randomize specular color
                float3 specularColor = _Specular * lerp(0.5f, 1.0f, i.rng);

                // Fresnel term
                float fresnel;
                Unity_FresnelEffect_float(i.worldNor, viewDir, _FresnelPower, fresnel);
                // Randomize fresnel color
                float3 fresnelColor = _FresnelColor * lerp(0.5f, 1.0f, i.rng);

                // Color randomizer
                float3 colorRandomizer = tex2D(_ColorRandomizer, i.uv).rgb;
                
                // Final Color
                float3 color = diffuseColor * diffuse + specularColor * spec + fresnelColor * fresnel;
                color *= colorRandomizer;
                
                return float4(color, 1.0f);
            }
            ENDHLSL
        }
    }
}
