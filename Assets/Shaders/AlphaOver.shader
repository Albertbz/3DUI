// Composites _OverlayTex (virtual layer, transparent background) over _MainTex (passthrough camera image).
Shader "Hidden/3DUI/AlphaOver"
{
    Properties
    {
        _MainTex ("Base", 2D) = "black" {}
        _OverlayTex ("Overlay", 2D) = "clear" {}
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
            sampler2D _OverlayTex;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv);
                fixed4 overlay = tex2D(_OverlayTex, i.uv);
                return fixed4(lerp(baseCol.rgb, overlay.rgb, overlay.a), 1);
            }
            ENDCG
        }
    }
}
