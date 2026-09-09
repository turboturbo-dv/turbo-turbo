Shader "TurboTurbo/HeatShimmer"
{
    Properties
    {
        _Strength ("Distortion Strength", Float) = 0.25
        _EffectRadius ("Effect Radius (0..1 of quad)", Float) = 1.0
        _AnimTime ("Animation Time", Float) = 0.0
        _Freq ("Noise Frequency", Float) = 1.0
        _Debug ("Debug View", Float) = 0
        _Outline ("Outline Billboard (0/1)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        // named grab, as multiple instances of the effect will be live at the same time
        GrabPass { "_TurboHeatGrab" }

        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Strength;
            float _EffectRadius;
            float _AnimTime;
            float _Freq;
            float _Debug;
            float _Outline;
            sampler2D _TurboHeatGrab;
            sampler2D_float _CameraDepthTexture;

            static const float MinVisibleAlpha = 0.004;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 grabUV : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                fixed4 color : TEXCOORD3;
            };

            float fbm (float2 p);
            float vnoise (float2 p);
            float hash12 (float2 p);

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.grabUV = ComputeGrabScreenPos(o.pos);
                o.eyeDepth = -UnityObjectToViewPos(v.vertex).z;
                o.color = v.color;
                return o;
            }

            // we don't use `i.color.rgb` here, but we do use alpha for the
            // decay envelope
            half4 frag (v2f i) : SV_Target
            {
                // adds a debug outline to the quads
                if (_Outline > 0.5)
                {
                    float2 e = min(i.uv, 1.0 - i.uv);
                    if (e.x < 0.02 || e.y < 0.02)
                    {
                        return half4(1.0, 0.0, 1.0, 1.0);
                    }
                }

                float2 suvBase = i.grabUV.xy / i.grabUV.w;

                // experiment: effect area is an ellipse, EffectRadius sets the
                // horizontal semi-axis: b = EffectRadius / 2
                float rh = clamp(_EffectRadius, 0.05, 1.0) * 0.5;
                float2 exy = float2(abs(i.uv.x - 0.5), abs(i.uv.y - 0.5));
                float dn = length(float2(exy.x / rh, exy.y / 0.5));
                float uvMask = 1.0 - smoothstep(0.55, 1.0, dn);

                // early out on masked corners that fall outside the effect area
                if (uvMask * i.color.a < MinVisibleAlpha)
                {
                    // the jury is still out on whether this is faster or slower
                    // than return half4(0); probably insignificant anyway
                    discard;
                }

                // when sampling at an offset, we need to perform the depth test
                // manually. Makes sure we don't mix foreground objects into the
                // effect
                float rawZ = tex2D(_CameraDepthTexture, suvBase).r;
                float sceneZ = LinearEyeDepth(rawZ);
                float occluded = (rawZ > 0.0001 && rawZ < 0.9999 && sceneZ < i.eyeDepth - 0.05) ? 1.0 : 0.0;
                float edgeFade = uvMask * (1.0 - occluded);

                // debug 1: show effect area, transparent where faded
                if (_Debug > 0.5 && _Debug < 1.5)
                {
                    return half4(edgeFade.xxx, edgeFade);
                }

                // decay envelope scales effect blend strength. Scaling shimmer
                // intensity instead is an alternative worth looking into.
                float a = edgeFade * i.color.a;

                // early out on fragments that are completely faded or that
                // sample an occluding object
                if (a < MinVisibleAlpha)
                {
                    discard;
                }

                // sample the noise field to calculate shimmer offset.
                // relatively expensive so it may be worth benchmarking this
                // vs. lookup on a precomputed noise texture.
                // overall shouldn't be too big of a deal though, as the shader
                // effect takes up little screen space.
                float2 np = i.uv * float2(4.0 * _Freq, 1.8 * _Freq)
                          - float2(_AnimTime * 0.13, _AnimTime);
                float n1 = fbm(np);
                float n2 = fbm(np + float2(37.2, 17.9));
                float2 offset = (float2(n1, n2) - 0.5) * _Strength;

                // debug 2: show computed offset (RG = xy, B = mask)
                if (_Debug > 1.5 && _Debug < 2.5)
                {
                    return half4(offset * 20.0 + 0.5, edgeFade, 1.0);
                }

                // this is where the magic happens
                half4 scene = tex2D(_TurboHeatGrab, suvBase + offset);
                return half4(scene.rgb, a);
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

            // (c) David Hoskins: https://www.shadertoy.com/view/4djSRW (MIT)
            float hash12 (float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            ENDCG
        }
    }
}

