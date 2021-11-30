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
                // sample the texture
                float4 color = tex2D(_MainTex, input.st);

                float4 outColor = color; 
                
                return outColor;
            }
            ENDHLSL
        }
    }
}
