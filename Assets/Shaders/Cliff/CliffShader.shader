Shader "Unlit/CliffShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _CleffMask ("Cleff mask", 2D) = "white" {}
        _NMap ("Normal map", 2D) = "white" {}
        _NMap2 ("Normal map", 2D) = "white" {}
        _NMap3 ("Normal map", 2D) = "white" {}
        _NMap4 ("Normal map", 2D) = "white" {}
        _SpecMap ("Glitter Map", 2D) = "black" {}
    }
    SubShader
    {
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            #pragma multi_compile_fwdbase
            #include "AutoLight.cginc"

            float2 calculateTextureUV (float2 uv, float cliffNum, float width);
            float diffuse(float3 normal, float3 lightDir);

            // struct VertexAttributes
            // {
            //     float4 pos : POSITION;
            //     float2 uv : TEXCOORD0;
            //     float2 cliffTextureNumber : TEXCOORD1;
            //     float4 tan : TANGENT;
            //     float3 nor : NORMAL;
            // };

            struct FragmentAttributes
            {
                float4 pos : SV_POSITION;
                float4 worldPos : TEXCOORD0;
                float2 st : TEXCOORD1;
                float cliffTextureNumber : TEXNUM0;
                float3x3 tbn : MATRIX;
                LIGHTING_COORDS(1,2)
            };

            sampler2D _MainTex;
            sampler2D _CleffMask;
            sampler2D _NMap;
            sampler2D _NMap2;
            sampler2D _NMap3;
            sampler2D _NMap4;
            sampler2D _SpecMap;

            FragmentAttributes vert (appdata_full v)
            {
                // The output struct for the fragment stage.
                FragmentAttributes output;
                
                float3 tangent = v.tangent.xyz * v.tangent.w;
                float3 bitangent = cross(v.normal, v.tangent.xyz) * v.tangent.w;
                float3 normal = v.normal;

                float3 T = normalize(float3(UnityObjectToWorldDir(tangent)));
                float3 B = normalize(float3(UnityObjectToWorldDir(bitangent)));
                float3 N = normalize(float3(UnityObjectToWorldDir(normal)));
                output.tbn = transpose(float3x3(T, B, N));

                float4 modelPos = v.vertex;
                
                output.worldPos = mul(unity_ObjectToWorld, modelPos);
                output.pos = UnityObjectToClipPos(modelPos);
                output.cliffTextureNumber = v.texcoord1.x;

                output.st = v.texcoord;

                TRANSFER_VERTEX_TO_FRAGMENT(output);
                
                return output;
            }

            fixed4 frag (FragmentAttributes input) : SV_Target
            {
                // sample the texture
                float2 st = input.st;
                float4 color = tex2D(_MainTex, calculateTextureUV(input.st, input.cliffTextureNumber, 6.0));
                float4 cleffMask = tex2D(_CleffMask, calculateTextureUV(input.st, input.cliffTextureNumber, 6.0));
                float isCleff = step(1.0f, cleffMask.r);
                
                float3 nmap = tex2D(_NMap, calculateTextureUV(input.st, input.cliffTextureNumber, 3.0)).rgb;
                float3 nmap2 = tex2D(_NMap2, calculateTextureUV(input.st, input.cliffTextureNumber, 3.0)).rgb;
                float3 nmap3 = tex2D(_NMap3, calculateTextureUV(input.st, input.cliffTextureNumber, 6.0)).rgb;
                float3 nmap4 = tex2D(_NMap4, calculateTextureUV(input.st, input.cliffTextureNumber, 3.0)).rgb;
                float3 nnn = nmap + nmap4;
                float3 normal = normalize(mul(input.tbn, nnn));
                // float3 normal = normalize(mul(input.tbn, nmap + nmap2 + nmap3 + nmap4));

                float3 cameraDir = normalize(_WorldSpaceCameraPos - input.worldPos.xyz);

                float3 roughness = tex2D(_SpecMap, calculateTextureUV(input.st, input.cliffTextureNumber, 3.0)).rgb;
                float specularStrength = 10.0f;
                float3 lightDir = normalize(float3(0.0f, 1.0f, 1.0f));
                float3 reflectDir = reflect(-lightDir, float3(0.0f, 1.0f, 0.0f));
                float spec = pow(max(dot(cameraDir, reflectDir), 0.0f), 32);
                float3 specular = specularStrength * spec * roughness.rgb;
                
                float ambient = 0.2f;
                float attenuation = LIGHT_ATTENUATION(input);
                float3 outColor = color * (ambient + diffuse(normal, _WorldSpaceLightPos0) * attenuation);
                
                float d = dot(_WorldSpaceLightPos0, normal);
                float3 diffColor = float3(0.3, 0.3f, 0.3f);
                outColor = diffColor * ambient + diffColor * (d * d + 0.2f) * attenuation + specular * attenuation * 1.0f;
                return float4(outColor, 1.0f);
            }

            float2 calculateTextureUV (float2 uv, float cliffNum, float width)
            {
                return float2(1.0/width * cliffNum + 1.0/width * uv.x, uv.y);
            }

            float diffuse(float3 normal, float3 lightDir)
            {
                float d = dot(lightDir, normal);
                return d;
            }
            ENDHLSL
        }
    }
    Fallback "VertexLit"
}
