Shader "TurboTurbo/HeatShimmer"
{
    Properties
    {
        _Strength ("Distortion Strength", Float) = 0.25
        _EffectRadius ("Effect Radius (0..1 of quad)", Float) = 1.0
        _AnimTime ("Animation Time", Float) = 0.0
        _Freq ("Noise Frequency", Float) = 1.0
        _Debug ("Debug View", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        // UNNAMED grab: captured fresh per object per camera. A named grab is
        // captured once per frame at the first object that uses it - if a
        // reflection probe or secondary camera renders the quad first, the
        // view camera reuses a stale/wrong-viewpoint grab (invisible shimmer).
        GrabPass { }

        Pass
        {
            ZWrite Off
            Cull Off
            Blend Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Strength;
            float _EffectRadius;
            float _AnimTime;
            float _Freq;
            float _Debug;
            sampler2D _GrabTexture;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 grabUV : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.grabUV = ComputeGrabScreenPos(o.pos);
                return o;
            }

            // hash + value noise (Dave Hoskins style, no trig): stays stable
            // at large coordinates so the animation time can run for hours
            float hash12 (float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float vnoise (float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash12(i);
                float b = hash12(i + float2(1.0, 0.0));
                float c = hash12(i + float2(0.0, 1.0));
                float d = hash12(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 3-octave fractal noise, normalized to ~0..1
            float fbm (float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int o = 0; o < 3; o++)
                {
                    v += a * vnoise(p);
                    p = p * 2.07 + float2(13.7, 5.1);
                    a *= 0.5;
                }
                return v / 0.875;
            }

            half4 frag (v2f i) : SV_Target
            {
                // quad-uv mask: blob anchored at the bottom-center (the stack
                // mouth). The radius grows with engine flow; the taper always
                // reaches zero at dn = 1, which at full flow (radius 1) is
                // exactly the quad edge - rescaling the quad rescales the
                // whole effect. Vertical extent is weighted so the column
                // stretches upward.
                float2 d = float2(abs(i.uv.x - 0.5) * 2.0, i.uv.y * 1.35);
                float dn = length(d) / max(_EffectRadius, 0.05);
                float mask = 1.0 - smoothstep(0.55, 1.0, dn);

                // rising turbulent field: vertically stretched cells, moving
                // up at the flow-dependent speed, slow lateral evolution
                float2 np = i.uv * float2(4.0 * _Freq, 1.8 * _Freq)
                          - float2(_AnimTime * 0.13, _AnimTime);
                float n1 = fbm(np);
                float n2 = fbm(np + float2(37.2, 17.9));
                float2 offset = (float2(n1, n2) - 0.5) * _Strength * mask;

                // debug 1: mask coverage
                if (_Debug > 0.5 && _Debug < 1.5)
                {
                    return half4(mask.xxx, 1.0);
                }
                // debug 2: raw grab, no offset - validates the grab path
                if (_Debug > 1.5 && _Debug < 2.5)
                {
                    return tex2D(_GrabTexture, i.grabUV.xy / i.grabUV.w);
                }
                // debug 3: computed offset (RG, +-0.05 = full swing) + mask (B)
                if (_Debug > 2.5 && _Debug < 3.5)
                {
                    return half4(offset * 20.0 + 0.5, mask, 1.0);
                }
                // debug 4: solid magenta - proves geometry + material render
                if (_Debug > 3.5)
                {
                    return half4(1.0, 0.0, 1.0, 1.0);
                }

                // offset applied AFTER projection: _Strength is in true
                // screen-UV units, independent of view distance
                float2 suv = i.grabUV.xy / i.grabUV.w + offset;
                return tex2D(_GrabTexture, suv);
            }
            ENDCG
        }
    }
}
