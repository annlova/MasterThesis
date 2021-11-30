Shader "Unlit/NewUnlitShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            float nsin(float rad);

            struct VertexAttributes
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct FragmentAttributes
            {
                float4 clipPos : POSITION;
                float4 worldPos : TEXCOORD0;
                float2 st : TEXCOORD1;
            };

            sampler2D _MainTex;

            FragmentAttributes vert (VertexAttributes input)
            {
                // The output struct for the fragment stage.
                FragmentAttributes output;

                float4 modelPos = input.pos;
                
                output.worldPos = mul(unity_ObjectToWorld, modelPos);
                output.clipPos = UnityObjectToClipPos(modelPos);

                output.st = input.uv;
                
                return output;
            }

            fixed4 frag (FragmentAttributes input) : SV_Target
            {
                float4 light = float4(float3(0.97, 0.9, 0.66), 1);
                float4 dark = float4(float3(0.8, 0.64, 0.34), 1);
                
                // sample the texture
                float4 color = lerp(light, dark, nsin(input.st.x * 20));

                float4 outColor = color; 
                
                return outColor;
            }

            float nsin(float rad)
            {
                return (sin(rad) + 1) / 2;
            }
            
            ENDHLSL
        }
    }
}
