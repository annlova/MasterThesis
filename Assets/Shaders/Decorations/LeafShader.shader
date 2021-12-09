Shader "Unlit/LeafShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Geometry" }
        ZWrite On
//        Blend SrcAlpha OneMinusSrcAlpha
        Blend Off
        AlphaTest Greater 0.5
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

            struct VertexAttributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct FragmentAttributes
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float3 worldNor : NORMAL;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            FragmentAttributes vert (VertexAttributes i)
            {
                FragmentAttributes o;
                o.worldNor = UnityObjectToWorldNormal(i.normal);
                o.worldPos = mul(unity_ObjectToWorld, i.vertex);
                o.vertex = UnityObjectToClipPos(i.vertex);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }

            float4 frag (FragmentAttributes i) : SV_Target
            {
                // sample the texture
                // float4 sample = tex2D(_MainTex, i.uv);
                // if (sample.a < 0.5f)
                // {
                //     discard;
                // }
                float diffuse = dot(_WorldSpaceLightPos0, i.worldNor);
                float3 col = float3(0.4f, 1.0f, 0.5f) * diffuse;
                return float4(i.uv, 0.0f, 1.0f);
            }
            ENDHLSL
        }
    }
}
