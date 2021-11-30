Shader "Unlit/GenericSandShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NormMap ("Normal map", 2D) = "white" {}
        _GradientMap ("Gradient map", 2D) = "white" {}
        _RoughnessMap ("Roughness map", 2D) = "white" {}
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

            float3 diffuse(float3 normal, float3 lightDir);
            float calcGradientAmount(float3 normal);

            struct VertexAttributes
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
                float4 tan : TANGENT;
                float3 nor : NORMAL;
            };

            struct FragmentAttributes
            {
                float4 clipPos : POSITION;
                float4 worldPos : TEXCOORD0;
                float2 st : TEXCOORD1;
                float3x3 tbn : MATRIX;
            };

            sampler2D _MainTex;
            sampler2D _NormMap;
            sampler2D _GradientMap;
            sampler2D _RoughnessMap;

            FragmentAttributes vert (VertexAttributes input)
            {
                // The output struct for the fragment stage.
                FragmentAttributes output;

                float3 tangent = input.tan.xyz * input.tan.w;
                float3 bitangent = cross(input.nor, input.tan.xyz) * input.tan.w;
                float3 normal = input.nor;

                float3 T = normalize(float3(UnityObjectToWorldDir(tangent)));
                float3 B = normalize(float3(UnityObjectToWorldDir(bitangent)));
                float3 N = normalize(float3(UnityObjectToWorldDir(normal)));
                output.tbn = float3x3(T, B, N);

                float4 modelPos = input.pos;
                
                output.worldPos = mul(unity_ObjectToWorld, modelPos);
                output.clipPos = UnityObjectToClipPos(modelPos);

                output.st = input.uv;
                
                return output;
            }

            fixed4 frag (FragmentAttributes input) : SV_Target
            {
                // sample the texture
                float3 color = tex2D(_MainTex, input.st).rgb;
                float3 normal = tex2D(_NormMap, input.st).rgb;
                float3 gradient = tex2D(_GradientMap, input.st).rgb;
                float3 roughness = tex2D(_RoughnessMap, input.st).rgb;

                normal = normalize(mul(input.tbn, normal));

                float3 cameraDir = normalize(_WorldSpaceCameraPos - input.worldPos.xyz);

                float specularStrength = 2.0f * roughness;
                float3 reflectDir = reflect(-_WorldSpaceLightPos0.xyz, normal);
                float spec = pow(max(dot(cameraDir, reflectDir), 0.0f), 32);
                float3 specular = specularStrength * spec * (1.0f).xxx;

                // color = calcGradientAmount(normal) * gradient + color;

                float3 outColor = color * (diffuse(normal, _WorldSpaceLightPos0) + specular); 
                // float3 outColor = color * specular; 
                
                return float4(outColor, 1.0);
            }

            float3 diffuse(float3 normal, float3 lightDir)
            {
                return float3(1.0, 1.0, 1.0) * dot(lightDir, normal);
            }

            float calcGradientAmount(float3 normal)
            {
                return dot(float3(.0, 1.0, .0), normal);
            }
            ENDHLSL
        }
    }
}
