// Samples a quad of _MainTex (the passthrough camera image) into a full rectangle. _Homography maps the output's
// uv onto the source's uv; its rows are the 3x3 projective matrix. Outside the source image is black.
Shader "Hidden/3DUI/QuadWarp"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
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
            float4x4 _Homography;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 q = mul(_Homography, float4(i.uv, 1, 0)).xyz;
                float2 uv = q.xy / q.z;
                if (any(uv < 0) || any(uv > 1))
                {
                    return fixed4(0, 0, 0, 1);
                }
                return fixed4(tex2D(_MainTex, uv).rgb, 1);
            }
            ENDCG
        }
    }
}
