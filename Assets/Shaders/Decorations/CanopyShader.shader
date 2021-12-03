Shader "Unlit/CanopyShader"
{
    Properties
    {
        _MainTex ("Cut Off Texture", 2D) = "white" {}
        _Scale ("Scale", Float) = 1.0
        _Inflate ("Inflation", Float) = 0.0
        _Rotate ("Rotation Degrees", Float) = 0.0
        _Diffuse ("Diffuse Color", Color) = (0.5, 0.8, 0.5)
        _Smoothness ("Smoothness", Float) = 1.0
        _Specular ("Specular Color", Color) = (0.5, 0.8, 0.5)
        _FresnelColor ("Fresnel Color", Color) = (0.5, 0.8, 0.5)
        _FresnelPower ("Fresnel Power", Int) = 16
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
            };

            struct FragmentAttributes
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float3 worldNor : NORMAL;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Scale;
            float _Inflate;
            float _Rotate;
            float4 _Diffuse;
            float _Smoothness;
            float4 _Specular;
            float4 _FresnelColor;
            int _FresnelPower;
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
                float rotation = sin(_Time.w * 1.0f) * 1.0f;
                Unity_Rotate_Degrees_float(uvRemapped, float2(0.0f, 0.0f), rotation, uvRotated);

                float4 uv4Remapped = float4(uvRotated, 0.0f, 0.0f);
                
                // View matrix
                float4x4 view = unity_WorldToCamera;

                // Multiply
                float4 uvView;
                Unity_Multiply_float4_float4x4(uv4Remapped, view, uvView);

                // Object to world matrix
                float4x4 objToWorld = unity_ObjectToWorld;

                // Multiply
                float4 uvWorld;
                Unity_Multiply_float4_float4x4(uvView, objToWorld, uvWorld);

                // Normalize
                float3 objectScale = float3(length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x)),
                                            length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y)),
                                            length(float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z)));
                float4 uvNormalized;
                Unity_Normalize_float4(float4(uvWorld.xyz * objectScale, 0.0f), uvNormalized);

                // Scale
                float4 uvScaled;
                Unity_Multiply_float4_float4(_Scale, uvNormalized, uvScaled);

                // Inflate
                float4 inflation;
                Unity_Multiply_float4_float4(float4(o.worldNor, 0.0f), _Inflate, inflation);

                // Add
                float4 uvInflated;
                Unity_Add_float4(inflation, uvScaled, uvInflated);
                
                // Lerp
                float4 offset;
                Unity_Lerp_float4((0.0f).xxxx, uvInflated, _Blend, offset);

                float4 localPos = i.vertex + offset;
                o.worldPos = mul(localPos, unity_ObjectToWorld);
                o.vertex = UnityObjectToClipPos(localPos);
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

                // Specular term
                float specularStrength = _Smoothness;
                float3 reflectDir = reflect(-_WorldSpaceLightPos0.xyz, i.worldNor);
                float spec = pow(max(dot(viewDir, reflectDir), 0.0f), 32) * specularStrength;
                
                // Fresnel term
                float fresnel;
                Unity_FresnelEffect_float(i.worldNor, viewDir, _FresnelPower, fresnel);

                // Final Color
                float3 color = _Diffuse * diffuse + _Specular * spec + fresnel * _FresnelColor;
                return float4(color, 1.0f);
            }
            ENDHLSL
        }
    }
}
