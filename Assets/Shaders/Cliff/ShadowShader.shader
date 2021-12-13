Shader "Unlit/ShadowShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "LightMode" = "ForwardBase" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            
            #pragma multi_compile_fwdbase
            #include "AutoLight.cginc"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 pos : SV_POSITION;
                LIGHTING_COORDS(2,3)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float attenuation = LIGHT_ATTENUATION(i);
                fixed4 col = tex2D(_MainTex, i.uv) * attenuation;
                col.a = 1.0f;
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDHLSL
        }
    }
    Fallback "VertexLit"
}
