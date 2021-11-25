Shader "Unlit/OceanShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
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

            float nsin(float v);
            
            struct VertexAttributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 beachHeights : TEXCOORD1;
            };

            struct FragmentAttributes
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float heightMap : HEIGHTMAP;
                float tideHeight : TIDE;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            
            
            FragmentAttributes vert (VertexAttributes input)
            {
                FragmentAttributes o;
                float tideChange = 0.2f;
                
                float tideOffset = nsin(_Time.y) * tideChange;
                o.tideHeight = 0.5f - tideOffset;
                
                float4 modelPos = input.vertex + float4(0.0f, tideOffset, 0.0f, 0.0f);
                o.worldPos = mul(unity_ObjectToWorld, modelPos);
                o.vertex = UnityObjectToClipPos(modelPos);
                
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                o.heightMap = input.beachHeights.x;
                return o;
            }

            float4 frag (FragmentAttributes input) : SV_Target
            {
                // sample the texture
                float3 cameraDir = normalize(_WorldSpaceCameraPos - input.worldPos.xyz);
                // sample the default reflection cubemap, using the reflection vector
                float4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, cameraDir);
                // decode cubemap data into actual color
                float3 skyColor = DecodeHDR (skyData, unity_SpecCube0_HDR);
                
                float3 white = (1.0f).xxx;
                float h = abs(input.heightMap);
                float factor = smoothstep(input.tideHeight, 2.0f, h);
                float waveFactor = nsin(factor * 3.14f * 2.0f + _Time.y);
                waveFactor += nsin(factor * 3.14f * 2.0f * 3.0f + _Time.y);
                waveFactor /= 2.0f;
                float3 waveCol = lerp(white, skyColor, waveFactor);
                return float4(lerp(waveCol, skyColor, factor), 1.0f);
            }

            float nsin(float v)
            {
                return (sin(v) + 1.0f) / 2.0f;
            }
            ENDHLSL
        }
    }
}
