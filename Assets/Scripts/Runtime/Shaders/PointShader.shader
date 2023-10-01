// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx



Shader "Point Cloud/Regular"
{
    Properties
    {
        _Tint("Tint", Color) = (0.5, 0.5, 0.5, 1)
    }
    SubShader
    {
        

        //Tags { "RenderType"="Opaque" }
        Tags{ "RenderType"="Transparent" "Queue"="Transparent"}

		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite on

        Pass
        {
            CGPROGRAM

            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "UnityCG.cginc"
            #include "Common.cginc"
            #include "UnityShaderVariables.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_Position;
                half4 color : COLOR;
            };

            StructuredBuffer<Point> _PointBuffer;


            CBUFFER_START(Params)
                float3 _cameraForward;
                float _distFactor;
                float4x4 _Transform;
                float _gammaIntensity;
            CBUFFER_END
            

            Varyings Vertex(uint vid : SV_VertexID)
            {
                
                    Point pt = _PointBuffer[vid];
                    Varyings o;
                   
                    o.position = mul(_Transform, float4(pt.position, 1));
                    o.color = PcxDecodeColorAlphaFinal(pt.color);

                    o.color = IncreaseLightAlpha(o.color, _gammaIntensity );
                    
                    o.color.xyz *= LinearToGammaSpace(half3(0.5,0.5,0.5));
                    o.color.xyz = GammaToLinearSpace(o.color.xyz);

                    o.position = UnityObjectToClipPos(o.position);
                    return o;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return input.color;
            }

            ENDCG
        }
    }
}
