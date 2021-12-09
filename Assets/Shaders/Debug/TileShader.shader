Shader "Unlit/TileShader"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque"}
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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 d : TEXCOORD2;
                float4 normal : NORMAL;
            };

            struct v2f
            {
                // float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 p : TEXCOORD0;
                float4 normal : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float2 d : TEXCOORD3;
            };

            StructuredBuffer<float4> _TileValues;
            int _AcreSize;
            int _MapWidth;
            int _MapHeight;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o,o.vertex);

                o.p = float4(v.vertex.x, v.vertex.z, 0.0f, 0.0f);
                // int x = (int) (v.vertex.x + 0.2f);
                // int z = (int) (v.vertex.z + 0.2f);
                // o.id = float4(0.0f, 0.0f, 0.0f, 0.0f);
                // o.id = x + (16 * 6 - 1 - z) * (16 * 6);
                // o.id = x + z * (16 * 6 + 1);
                o.normal = v.normal;
                o.uv = v.uv;
                o.d = v.d;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // float id = floor(i.id.x);
                int x = int(i.p.x);
                int y = int(i.p.y);
                float4 tileColor = _TileValues[x + (_AcreSize * _MapHeight - 1 - y) * _AcreSize * _MapWidth];
                // float s = (sin(i.d.y * frac(_Time.y) * 3.14 * 4) + 1) / 2;
                // if (abs(s - i.d.x) < 0.2f)
                // {
                //     tileColor = float4(0.0f, 1.0f, 0.0f, 1.0f);
                // }
                // sample the texture
                float4 ambient = float4(0.0f, 0.0f, 0.0f, 0.0f);
                float4 diffuse = tileColor;// * dot(i.normal, _WorldSpaceLightPos0);
                float4 col = ambient + diffuse;
                col.a = 1.0f;
                // float4 spec = 
                // apply fog
                // float4 col = i.color;
                UNITY_APPLY_FOG(i.fogCoord, col);

                return col;//float4(0.01f * id, 0.0f, 0.0f, 1.0f);
            }
            ENDHLSL
        }
    }
}
