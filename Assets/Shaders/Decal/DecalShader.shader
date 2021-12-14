Shader "Unlit/FootstepDecalShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _CutOff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "LightMode"="ForwardBase" }
        Blend SrcAlpha OneMinusSrcAlpha
//        ZTest Always
        ZWrite Off
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityShaderVariables.cginc"

            // #pragma multi_compile_fwdbase
            // #include "AutoLight.cginc"
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                // LIGHTING_COORDS(1,2)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _CutOff;

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                // TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                clip(col.a - _CutOff);
                
                // float attenuation = LIGHT_ATTENUATION(i);
                return float4(col.xyz, col.a);
            }
            ENDHLSL
        }
    }
    
//    Fallback "VertexLit"
}
