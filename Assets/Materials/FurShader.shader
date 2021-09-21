Shader "Unlit/FurShader"
{
    Properties
    {
        _FurTexture ("Texture", 2D) = "white" {}
        _FurTextureBottom ("TextureBottom", 2D) = "white" {}
        _FurLength ("Fur Length", Float) = 0.0
        _UVScale ("UV Scale", Float) = 1.0
        _Layer ("Layer", Float) = 0.0 // 0 to 1 for the level
        _VGravity ("Gravity float3", Vector) = (0,-2.0,0,0)
        _VecLightDir ("Light Dir", Vector) = (0.8,0.8,1,0)
        _Thickness("Hair Thickness", Float) = 0.5
        _Falloff("Hair Length Falloff factor", Float) = 10.0
        _HairAmount("Number of Hair Strands", Float) = 1000.0
        _ColorVariation("Color Variation", Float) = 0.6
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
            sampler2D _FurTextureBottom;
            float4 _FurTextureBottom_ST;

            float _Thickness;
            float _Falloff;
            float _HairAmount;
            float _ColorVariation;
            
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

            float random (float2 st) {
                return frac(sin(dot(st.xy, float2(12.9898,78.233))) * 43758.5453123);
            }
            
            float4 frag (vertexOutput IN) : COLOR
            {
                // sample the texture
                // float4 col = tex2D(_MainTex, i.uv);
                // apply fog
                // return col;

                float hairLimit = _Thickness + _Layer * _Falloff;
                float2 st = IN.T0;
                st *= _HairAmount;
                float2 ipos = floor(st);  // get the integer coords
                float2 fpos = frac(st);  // get the fractional coords
                float hair = random(ipos);
                float isHair = step(hairLimit, hair);
                
                float4 FurColour = tex2D(_FurTexture,  IN.T0); // Fur Texture - alpha is VERY IMPORTANT!
                float4 FinalColour = FurColour;
                
                //--------------------------
                //
                //Basic Directional Lighting
                //float4(0.66f, 0.33f, 0.21f, 0.0f)
                float4 zeroVec = float4(0.0f, 0.0f, 0.0f, 0.0f);
                float4 oneVec = float4(1.0f, 1.0f, 1.0f, 0.0f);
                float4 color = tex2D(_FurTexture, IN.T0) * clamp(random(ipos + 5.0f), _ColorVariation, 1.0f);
                float4 ambient = {0.5f, 0.5f, 0.5f, 0.0f};
                float4 diffuse = float4(1.0f, 1.0f, 1.0f, 0.0f) * dot(_VecLightDir, IN.normal);
                diffuse = clamp(diffuse, zeroVec, oneVec);
                FinalColour = ambient * color + diffuse * color;
                FinalColour = clamp(FinalColour, zeroVec, oneVec);

                float isBottomLayer = step(_Layer, 0.0f);
                
                FinalColour.a = clamp(isHair + isBottomLayer, 0.0f, 1.0f);
                //End Basic Lighting Code    
                //-------------------------
                
                //FinalColour.a = f + FurColour.a * (1.0f -f);
                //FinalColour.a *= 1.0 - _Layer * 2;
                //return FinalColour;      // fur colour only!
                // UNITY_APPLY_FOG(i.fogCoord, FinalColour);
                return FinalColour;       // Use texture colour
                // return float4(0,0,0,0); // Use for totally invisible!  Can't see
            }

            ENDHLSL
        }
    }
}
