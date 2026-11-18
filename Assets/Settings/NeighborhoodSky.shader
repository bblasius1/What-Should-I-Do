Shader "What Should I Do/Neighborhood Sky"
{
    Properties
    {
        _HorizonColor ("Horizon", Color) = (0.77, 0.89, 0.89, 1)
        _ZenithColor ("Zenith", Color) = (0.36, 0.69, 0.89, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            half4 _HorizonColor;
            half4 _ZenithColor;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(float4 position : POSITION)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(position);
                output.direction = position.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half blend = smoothstep(-0.09, 0.57, normalize(input.direction).y);
                return lerp(_HorizonColor, _ZenithColor, blend);
            }
            ENDCG
        }
    }
}
