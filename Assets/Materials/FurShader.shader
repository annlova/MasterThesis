Shader "Unlit/FurShader"
{
    Properties
    {
        _FurTexture ("Texture", 2D) = "white" {}
        _FurLength ("Fur Length", Float) = 0.0
        _UVScale ("UV Scale", Float) = 1.0
        _Layer ("Layer", Float) = 0.0 // 0 to 1 for the level
        _VGravity ("Gravity float3", Vector) = (0,-2.0,0,0)
        _VecLightDir ("Light Dir", Vector) = (0.8,0.8,1,0)
    }
    SubShader
    {
//        Tags {"RenderType"="Opaque"}
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite off
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

            struct vertexInput
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float2 texCoordDiffuse : TEXCOORD0;
            };

            struct vertexOutput
            {
                float4 HPOS : POSITION;
                float2 T0 : TEXCOORD0; // fur alpha
                UNITY_FOG_COORDS(1)
                float3 normal : NORMAL;
            };

            float _FurLength;
            float _UVScale;
            float _Layer;
            float3 _VGravity;
            float4 _VecLightDir;
            
            sampler2D _FurTexture;
            float4 _FurTexture_ST;
            
            vertexOutput vert (vertexInput IN)
            {
                vertexOutput OUT;
                
                // OUT.T0 = TRANSFORM_TEX(IN.texCoordDiffuse, _FurTexture);

                //** MAIN LINE ** MAIN LINE ** MAIN LINE ** MAIN LINE ** MAIN LINE **//
                //** MAIN LINE ** MAIN LINE ** MAIN LINE ** MAIN LINE ** MAIN LINE **//
                //This single line is responsible for creating the layers!  This is it! Nothing
                //more nothing less!
                float3 P = IN.position.xyz + (IN.normal * _FurLength);

                //Modify our normal so it faces the correct direction for lighting if we
                //want any lighting
                float3 normal = normalize(UnityObjectToWorldNormal(IN.normal));

                // Couple of lines to give a swaying effect!
                // Additional Gravit/Force Code
                _VGravity = mul(_VGravity, UNITY_MATRIX_M);
                float k =  pow(_Layer, 3);  // We use the pow function, so that only the tips of the hairs bend
                                           // As layer goes from 0 to 1, so by using pow(..) function is still
                                           // goes form 0 to 1, but it increases faster! exponentially

                P = P + _VGravity*k;
                // End Gravity Force Addit Code

                OUT.T0 = IN.texCoordDiffuse * _UVScale; // Pass long texture data
                // UVScale??  Well we scale the fur texture alpha coords so this effects the fur thickness
                // thinness, sort of stretches or shrinks the fur over the object!

                // OUT.HPOS = mul(float4(P, 1.0f), UNITY_MATRIX_MVP); // Output Vertice Position Data
                OUT.HPOS = UnityObjectToClipPos(P);
                OUT.normal = normal; // Output Normal

                // UNITY_TRANSFER_FOG(o, o.vertex);
                
                return OUT;
            }

            float4 frag (vertexOutput IN) : COLOR
            {
                // sample the texture
                // float4 col = tex2D(_MainTex, i.uv);
                // apply fog
                // return col;

                float4 FurColour = tex2D(_FurTexture,  IN.T0); // Fur Texture - alpha is VERY IMPORTANT!
                float4 FinalColour = FurColour;
                //--------------------------
                //
                //Basic Directional Lighting
                float4 ambient = {0.3, 0.3, 0.3, 0.0};
                ambient = ambient * FinalColour;
                float4 diffuse = FinalColour;
                FinalColour = ambient + diffuse * dot(_VecLightDir, IN.normal);
                //End Basic Lighting Code    
                //-------------------------
                //float f = step(_Layer, 0.0f);
                
                if(_Layer > 0.0f)
                {
                    FinalColour.a = FurColour.a;    
                } else
                {
                    FinalColour = float4(0.0f, 0.0f, 0.0f, 1.0f);
                }

                //FinalColour.a = FurColour.a;
                
                //FinalColour.a = f + FurColour.a * (1.0f -f);
                //FinalColour.a *= 1.0 - _Layer * 1;
                //return FinalColour;      // fur colour only!
                // UNITY_APPLY_FOG(i.fogCoord, FinalColour);
                return FinalColour;       // Use texture colour
                // return float4(0,0,0,0); // Use for totally invisible!  Can't see
            }
            ENDHLSL
        }
    }
}
