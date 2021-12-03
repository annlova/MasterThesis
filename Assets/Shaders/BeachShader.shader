Shader "Unlit/BeachShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WorldBounds ("Min and Max points of world bounds", Vector) = (0.0, 0.0, 16.0, 16.0)
    }
    SubShader
    {
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
            float nsin(float v);
            
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
                float4 worldPos : TEXCOORD1;
                float4 nor : NORMAL;
                float4 tideHeight : TIDE;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _WorldBounds;
            
            FragmentAttributes vert (VertexAttributes input)
            {
                FragmentAttributes o;
                float tideChange = 0.15f;
                float PI = 3.1415927;
                float time = _Time.y * 0.2f;
                float animationTimeSkew = 0.3f;
                // float waveTime = frac(time) * PI * 2.0f;
                float waveTime = frac(time);

                float drySpeed = 0.5f;
                float dryTime = frac(time - animationTimeSkew) * drySpeed;
                
                float waveIn = -1.0f + smoothstep(0.0f, animationTimeSkew, waveTime) * 2.0f * step(waveTime, animationTimeSkew);
                float waveOut = 1.0f - smoothstep(0.0f, 1.0f, dryTime) * 2.0f;
                float tideOffset = max(waveIn, waveOut) * tideChange;
                
                // float x = waveTime * 0.3f;
                // float tideOffset = cos(x) * tideChange;
                o.tideHeight = float4(-0.8f + tideOffset, -0.8f - tideChange, -0.8f + tideChange, 0.0f);
                
                // float4 worldPos = mul(unity_ObjectToWorld, input.vertex);
                // float shoreDist = sqDistPointAABB(worldPos.xz, _WorldBounds.xy, _WorldBounds.zw);
                // float shoreDistFactor = shoreDist / (16*16);
                // float worldY = -(shoreDistFactor * 4.0f);
                // worldPos.y += worldY;
                // o.vertex = UnityWorldToClipPos(worldPos);
                o.worldPos = mul(unity_ObjectToWorld, input.vertex);
                o.vertex = UnityObjectToClipPos(input.vertex);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.nor = input.normal;//UnityObjectToWorldNormal(input.normal.xyz);
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
                float wet = 1.0f - smoothstep(input.tideHeight.x - 0.03f, input.tideHeight.x, input.worldPos.y);//smoothstep(-0.5f, input.tideHeight.x, input.worldPos.y);
                float4 col = tex2D(_MainTex, input.uv) * dot(_WorldSpaceLightPos0, input.nor) * (1.0f - ((1.0f - smoothstep(input.tideHeight.y, input.tideHeight.z, input.worldPos.y)) * 0.4f + 0.1f) * wet);
                col.a = 1.0f;               
                return col;
            }
            
            float nsin(float v)
            {
                return (sin(v) + 1.0f) / 2.0f;
            }
            ENDHLSL
        }
    }
}
