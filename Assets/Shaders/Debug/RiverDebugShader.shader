Shader "Unlit/RiverShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityShaderVariables.cginc"
            #include "UnityCG.cginc"

            struct vertexAttributes
            {
                float4 vertex : POSITION;
                float4 nor : NORMAL;
                float2 uv : TEXCOORD0;
                float2 dir : TEXCOORD1;
            };

            struct fragmentAttributes
            {
                float4 vertex : POSITION;
                float4 worldPos : TEXCOORD4;
                float2 uv : TEXCOORD0;
                float2 dir : TEXCOORD1;
                float3 worldNor : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fragmentAttributes vert (vertexAttributes input)
            {
                fragmentAttributes o;
                o.worldPos = mul(unity_ObjectToWorld, input.vertex);
                o.vertex = UnityObjectToClipPos(input.vertex);
                o.worldNor = normalize(UnityObjectToWorldNormal(input.nor.xyz));
                o.uv = input.uv;
                o.dir = input.dir;
                return o;
            }

            fixed4 frag (fragmentAttributes input) : SV_Target
            {
                float3 cameraDir = normalize(_WorldSpaceCameraPos - input.worldPos.xyz);
                // sample the default reflection cubemap, using the reflection vector
                float4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, cameraDir);
                // decode cubemap data into actual color
                float3 skyColor = DecodeHDR (skyData, unity_SpecCube0_HDR);
                float col = tex2D(_MainTex, input.uv + _Time.z * input.dir * float2(1.0f, -1.0f));
                return float4(skyColor, col);
            }
            ENDHLSL
        }
    }
}
