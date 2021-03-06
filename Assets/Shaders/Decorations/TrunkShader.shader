Shader "Unlit/TrunkShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NormMap ("Normal map", 2D) = "white" {}
        _GradientMap ("Gradient map", 2D) = "white" {}
        _RoughnessMap ("Roughness map", 2D) = "white" {}
        _ColorMultiplier ("Color Multiplier", Float) = 1.0
    }
    SubShader
    {
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #pragma multi_compile_fwdbase
            #include "AutoLight.cginc"
            
            float diffuse(float3 normal, float3 lightDir);
            float calcGradientAmount(float3 normal);

            // struct VertexAttributes
            // {
            //     float4 pos : POSITION;
            //     float2 uv : TEXCOORD0;
            //     float4 tan : TANGENT;
            //     float3 nor : NORMAL;
            //     float2 rng : TEXCOORD1;
            // };

            struct FragmentAttributes
            {
                float4 pos : POSITION;
                float4 worldPos : TEXCOORD0;
                float4 tideHeight : TIDE;
                float2 st : TEXCOORD1;
                float3x3 tbn : MATRIX;
                float3 nor : NORMAL;
                float rng : RANDOM;
                LIGHTING_COORDS(2,3)
            };

            sampler2D _MainTex;
            sampler2D _NormMap;
            sampler2D _GradientMap;
            sampler2D _RoughnessMap;

            float _ColorMultiplier;

            FragmentAttributes vert (appdata_full v)
            {
                // The output struct for the fragment stage.
                FragmentAttributes output;

                float tideChange = 0.15f;
                float PI = 3.1415927;
                float time = _Time.y * 0.2f;
                float animationTimeSkew = 0.3f;
                float waveTime = frac(time);

                float drySpeed = 0.5f;
                float dryTime = frac(time - animationTimeSkew) * drySpeed;
                
                float waveIn = -1.0f + smoothstep(0.0f, animationTimeSkew, waveTime) * 2.0f * step(waveTime, animationTimeSkew);
                float waveOut = 1.0f - smoothstep(0.0f, 1.0f, dryTime) * 2.0f;
                float tideOffset = max(waveIn, waveOut) * tideChange;
                
                output.tideHeight = float4(-0.8f + tideOffset, -0.8f - tideChange, -0.8f + tideChange, 0.0f);
                
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
                output.nor = UnityObjectToWorldNormal(v.normal);

                output.st = v.texcoord;

                output.rng = v.texcoord1.x;

                TRANSFER_VERTEX_TO_FRAGMENT(output);
                
                return output;
            }

            fixed4 frag (FragmentAttributes input) : SV_Target
            {
                // sample the texture
                float2 st = input.st * 2.0f;
                float3 color = tex2D(_MainTex, st).rgb;
                float3 normal = tex2D(_NormMap, st).rgb;
                normal = normalize(mul(input.tbn, normal));
                
                float2 gradientSample = float2(calcGradientAmount(normal), 0.0f);
                float3 gradient = tex2D(_GradientMap, gradientSample).rgb;
                float3 roughness = tex2D(_RoughnessMap, st).rgb;


                float3 cameraDir = normalize(_WorldSpaceCameraPos - input.worldPos.xyz);

                float specularStrength = 10.0f * roughness;
                float3 lightDir = normalize(float3(0.0f, 0.5f, 1.0f));
                float3 reflectDir = reflect(-lightDir, normal);
                float spec = pow(max(dot(cameraDir, reflectDir), 0.0f), 32);
                float3 specular = specularStrength * spec * (1.0f).xxx;

                color = color + gradient;
                color *= lerp(0.5f, 1.2f, input.rng);
                
                float ambient = 0.0f;
                float attenuation = LIGHT_ATTENUATION(input);
                float diff = diffuse(normal, _WorldSpaceLightPos0) * attenuation;
                float3 outColor = color * (ambient + diff) * _ColorMultiplier;

                // float wet = 1.0f - smoothstep(input.tideHeight.x - 0.03f, input.tideHeight.x, input.worldPos.y);//smoothstep(-0.5f, input.tideHeight.x, input.worldPos.y);
                // outColor *= (1.0f - ((1.0f - smoothstep(input.tideHeight.y, input.tideHeight.z, input.worldPos.y)) * 0.4f + 0.1f) * wet);
                
                
                return float4(outColor, 1.0f);
            }

            float diffuse(float3 normal, float3 lightDir)
            {
                float d = dot(lightDir, normal);
                return d;
            }

            float calcGradientAmount(float3 normal)
            {
                float d = dot(float3(0.0, 1.0, 0.0), normal);
                return d;//max((d - 0.75f) * 4.0f, 0.0f);
            }
            ENDHLSL
        }
    }
    
    Fallback "VertexLit"
}
