Shader "Unlit/PureShadowCasterShader"
{
    Properties
    {
    }
    SubShader
    {
        LOD 100

        Pass
        {
            Tags{ "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMain

            #include "UnityCG.cginc"
            
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
