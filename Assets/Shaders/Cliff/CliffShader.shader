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
    }
    SubShader
    {
//        Tags { "Queue"="Opaque" "RenderType"="Geometry" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float2 calculateTextureUV (float2 uv, float cliffNum, float width);
            float diffuse(float3 normal, float3 lightDir);

            struct VertexAttributes
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
                float2 cliffTextureNumber : TEXCOORD1;
                float4 tan : TANGENT;
                float3 nor : NORMAL;
            };

            struct FragmentAttributes
            {
                float4 clipPos : POSITION;
                float4 worldPos : TEXCOORD0;
                float2 st : TEXCOORD1;
                float cliffTextureNumber : TEXNUM0;
                float3x3 tbn : MATRIX;
                float3 nor : NORMAL;
            };

            sampler2D _MainTex;
            sampler2D _CleffMask;
            sampler2D _NMap;
            sampler2D _NMap2;
            sampler2D _NMap3;
            sampler2D _NMap4;

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
                output.tbn = transpose(float3x3(T, B, N));

                float4 modelPos = input.pos;
                
                output.worldPos = mul(unity_ObjectToWorld, modelPos);
                output.clipPos = UnityObjectToClipPos(modelPos);
                output.cliffTextureNumber = input.cliffTextureNumber.x;

                output.st = input.uv;
                
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
                float3 normal = normalize(mul(input.tbn, nmap * (1 - isCleff) + nmap2 + nmap3 + nmap4));
                // float3 normal = normalize(mul(input.tbn, nmap + nmap2 + nmap3 + nmap4));

                float ambient = 0.0f;
                float3 outColor = color * (ambient + diffuse(normal, _WorldSpaceLightPos0));
                
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
        Pass
        {
            Tags{ "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMain
 
            float4 VSMain (float4 vertex:POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }
 
            float4 PSMain (float4 vertex:SV_POSITION) : SV_TARGET
            {
                return 0;
            }

            ENDCG
        }
    }
}
