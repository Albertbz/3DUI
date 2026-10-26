// Procedural capture frame drawn on a single quad. Everything is computed in meters from _Size,
// so the border keeps a constant thickness no matter how far the hands are pulled apart.
// _Style: 0 = Viewfinder, 1 = Glow, 2 = Marching ants, 3 = Glass
Shader "3DUI/CaptureFrame"
{
    Properties
    {
        _Color ("Color", Color) = (0.55, 0.9, 1, 1)
        _InvalidColor ("Too Small Color", Color) = (1, 0.45, 0.35, 1)
        _Style ("Style", Float) = 0
        _Size ("Frame Size (m)", Vector) = (0.2, 0.15, 0, 0)
        _Padding ("Quad Padding (m)", Float) = 0.03
        _Thickness ("Line Thickness (m)", Float) = 0.004
        _CornerRadius ("Corner Radius (m)", Float) = 0.01
        _Valid ("Valid", Range(0, 1)) = 1
        _Activity ("Resize Activity", Range(0, 1)) = 0
        _Appear ("Appear", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        // Premultiplied alpha so the frame also writes alpha for the passthrough underlay
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 p : TEXCOORD0; // position in meters relative to the frame center
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _InvalidColor;
            float _Style;
            float4 _Size;
            float _Padding;
            float _Thickness;
            float _CornerRadius;
            float _Valid;
            float _Activity;
            float _Appear;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.p = (v.uv - 0.5) * (_Size.xy + 2.0 * _Padding);
                return o;
            }

            float sdRoundBox(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - halfSize + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            // Anti-aliased band of the given width centered on d = 0
            float band(float d, float width)
            {
                float aa = max(fwidth(d), 1e-5);
                return 1.0 - smoothstep(width * 0.5 - aa, width * 0.5 + aa, abs(d));
            }

            // Arc length along the rectangle's outline, clockwise from the top-left corner
            float perimeterCoord(float2 p, float2 h)
            {
                float ex = h.x - abs(p.x);
                float ey = h.y - abs(p.y);
                if (ey < ex)
                {
                    return p.y > 0 ? p.x + h.x : 2.0 * h.x + 2.0 * h.y + (h.x - p.x);
                }
                return p.x > 0 ? 2.0 * h.x + (h.y - p.y) : 4.0 * h.x + 2.0 * h.y + (p.y + h.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.p;
                float2 h = max(_Size.xy * 0.5, 1e-4);
                float r = min(_CornerRadius, min(h.x, h.y));
                float d = sdRoundBox(p, h, r);
                float t = _Thickness;
                float inside = 1.0 - smoothstep(-max(fwidth(d), 1e-5), 0.0, d);

                float line_ = 0;  // bright strokes
                float fill = 0;   // faint interior tint
                float glow = 0;   // soft halo outside the border

                if (_Style < 0.5)
                {
                    // Viewfinder: camera-style corner brackets, faint outline, thirds grid and center cross
                    float bracketLen = min(0.035, min(h.x, h.y) * 0.45);
                    float2 fromCorner = h - abs(p);
                    float bracketMask = step(fromCorner.x, bracketLen) * step(fromCorner.y, bracketLen);
                    float brackets = band(d + t, t * 2.0) * bracketMask;

                    float outline = band(d + t * 0.25, t * 0.5) * 0.35;

                    // Thirds grid fades in once the frame is big enough to be useful
                    float gridFade = smoothstep(0.08, 0.16, min(_Size.x, _Size.y));
                    float2 thirds = abs(abs(p) - h / 3.0);
                    float2 gw = max(fwidth(p), 1e-5);
                    float grid = max(1.0 - smoothstep(t * 0.15, t * 0.15 + gw.x, thirds.x),
                                     1.0 - smoothstep(t * 0.15, t * 0.15 + gw.y, thirds.y));
                    grid *= inside * 0.18 * gridFade;

                    float crossLen = min(0.01, min(h.x, h.y) * 0.3);
                    float2 ap = abs(p);
                    float cross_ = max(step(ap.x, t * 0.25) * step(ap.y, crossLen),
                                       step(ap.y, t * 0.25) * step(ap.x, crossLen)) * 0.6;

                    line_ = max(max(brackets, outline), max(grid, cross_));
                    fill = inside * 0.04;
                    // Round falloff around each corner so the halo has no hard edges
                    float cornerFade = 1.0 - smoothstep(bracketLen * 0.5, bracketLen * 1.2, length(fromCorner));
                    glow = exp(-max(d, 0) / 0.006) * (1.0 - inside) * 0.25 * cornerFade;
                }
                else if (_Style < 1.5)
                {
                    // Glow: rounded neon outline with a highlight sweeping around the perimeter
                    float2 pc = p / h;
                    float angle = atan2(pc.y, pc.x);
                    float sweep = pow(saturate(cos(angle - _Time.y * 2.5)), 12.0);

                    line_ = band(d, t) * (0.85 + 0.15 * sweep);
                    glow = exp(-abs(d) / (0.008 + 0.006 * _Activity)) * (0.45 + 0.35 * sweep);
                    fill = inside * 0.05;
                }
                else if (_Style < 2.5)
                {
                    // Marching ants: dashed outline that tiles exactly around the perimeter
                    float perimeter = 4.0 * (h.x + h.y);
                    float dashCount = max(4.0, round(perimeter / 0.02));
                    float s = perimeterCoord(p, h) / perimeter * dashCount - _Time.y * 1.5;
                    float dash = smoothstep(0.45, 0.55, frac(s)) * (1.0 - smoothstep(0.95, 1.0, frac(s)));
                    dash = max(dash, 0.25);

                    line_ = band(d, t) * dash;
                    glow = exp(-abs(d) / 0.004) * 0.2;
                    fill = inside * 0.06;
                }
                else
                {
                    // Glass: frosted pane with a thin rim and brighter top edge
                    float rim = band(d + t * 0.25, t * 0.5);
                    float topLight = saturate(p.y / h.y * 0.5 + 0.5);
                    float innerShade = exp(d / 0.012) * inside; // darker toward the rim from inside

                    line_ = rim * (0.6 + 0.4 * topLight);
                    fill = inside * (0.1 + 0.06 * topLight) + innerShade * 0.12;
                    glow = exp(-max(d, 0) / 0.005) * (1.0 - inside) * 0.15;
                }

                // Fade the halo out before it reaches the quad's edge
                glow *= 1.0 - smoothstep(_Padding * 0.4, _Padding * 0.95, d);

                fixed3 col = lerp(_InvalidColor.rgb, _Color.rgb, _Valid);

                // Brighten while the frame is actively being resized
                float boost = 1.0 + 0.6 * _Activity;
                float alpha = saturate(line_ * boost + glow * boost + fill);
                // Strokes go slightly toward white so they read as light rather than paint
                fixed3 rgb = lerp(col, 1.0, saturate(line_) * 0.35);

                alpha *= _Appear * _Color.a;
                return fixed4(rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
