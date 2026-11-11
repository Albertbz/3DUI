// Samples a quad of _MainTex (the passthrough camera image) into a full rectangle. _Homography maps the output's
// uv onto the source's uv; its rows are the 3x3 projective matrix. With _UseSecond, _SecondTex (the other passthrough
// camera, mapped by _SecondHomography) fills in past _MainTex's edges, blended across a narrow band to hide the seam.
// Where neither image reaches, the nearest image's edge is stretched, so the photo never has black areas.
Shader "Hidden/3DUI/QuadWarp"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
        _SecondTex ("Second source", 2D) = "black" {}
        _Feather ("Seam blend width (uv)", Float) = 0.04
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _SecondTex;
            float4x4 _Homography;
            float4x4 _SecondHomography;
            float _UseSecond;
            float _Feather;

            float2 Warp(float4x4 homography, float2 uv)
            {
                float3 q = mul(homography, float4(uv, 1, 0)).xyz;
                return q.xy / q.z;
            }

            // Distance to the nearest image edge in uv, negative outside the image
            float EdgeDistance(float2 uv)
            {
                float2 inside = min(uv, 1 - uv);
                return min(inside.x, inside.y);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = Warp(_Homography, i.uv);
                float edge = EdgeDistance(uv);
                fixed3 color = tex2D(_MainTex, saturate(uv)).rgb;

                if (_UseSecond > 0.5 && edge < _Feather)
                {
                    float2 secondUv = Warp(_SecondHomography, i.uv);
                    float secondEdge = EdgeDistance(secondUv);
                    if (secondEdge > edge)
                    {
                        fixed3 second = tex2D(_SecondTex, saturate(secondUv)).rgb;
                        // Fully the second image past the first one's edge, blended just inside it
                        float weight = edge >= 0 ? saturate(edge / _Feather) : 0;
                        color = lerp(second, color, weight);
                    }
                }
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}
