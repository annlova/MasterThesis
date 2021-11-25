Shader "Unlit/BeachShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WorldBounds ("Min and Max points of world bounds", Vector) = (0.0, 0.0, 16.0, 16.0)
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

            float sqDistPointAABB(float2 p, float2 min, float2 max);
            
            struct VertexAttributes
            {
                float4 vertex : POSITION;
                float4 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct FragmentAttributes
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 nor : NORMAL;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _WorldBounds;

            FragmentAttributes vert (VertexAttributes input)
            {
                FragmentAttributes o;
                // float4 worldPos = mul(unity_ObjectToWorld, input.vertex);
                // float shoreDist = sqDistPointAABB(worldPos.xz, _WorldBounds.xy, _WorldBounds.zw);
                // float shoreDistFactor = shoreDist / (16*16);
                // float worldY = -(shoreDistFactor * 4.0f);
                // worldPos.y += worldY;
                // o.vertex = UnityWorldToClipPos(worldPos);
                o.vertex = UnityObjectToClipPos(input.vertex);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.nor = input.normal;//(UnityObjectToWorldNormal(input.normal.xyz), 0.0f);
                return o;
            }

            float sqDistPointAABB(float2 p, float2 min, float2 max)
            {
                float sqDist = 0.0f;
                {
                    float vx = p.x;
                    float vxSmaller = step(vx + 0.00001f, min.x);
                    sqDist += ((min.x - vx) * (min.x - vx)) * vxSmaller;
                    float vxBigger = step(max.x + 0.00001f, vx);
                    sqDist += ((vx - max.x) * (vx - max.x)) * vxBigger;
                }
                {
                    float vy = p.y;
                    float vySmaller = step(vy + 0.00001f, min.y);
                    sqDist += ((min.y - vy) * (min.y - vy)) * vySmaller;
                    float vyBigger = step(max.y + 0.00001f, vy);
                    sqDist += ((vy - max.y) * (vy - max.y)) * vyBigger;
                }

                return sqDist;
            }

            float4 frag (FragmentAttributes input) : SV_Target
            {
                // sample the texture
                float4 col = tex2D(_MainTex, input.uv) * dot(_WorldSpaceLightPos0, input.nor);
                col.a = 1.0f;
                return col;
            }
            ENDHLSL
        }
    }
}
