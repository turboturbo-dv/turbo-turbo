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

        // NAMED grab: captured once per frame at the first user - every
        // shimmer effect (quad or particle renderer) samples that one
        // capture, so N effects cost a single framebuffer copy.
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
                // debug outline: bright border at the billboard's uv edges
                if (_Outline > 0.5)
                {
                    float2 e = min(i.uv, 1.0 - i.uv);
                    if (e.x < 0.02 || e.y < 0.02)
                    {
                        return half4(1.0, 0.0, 1.0, 1.0);
                    }
                }

                float2 suvBase = i.grabUV.xy / i.grabUV.w;

                // foreground bleed fix: opaque geometry nearer than the quad
                // (handrails, other cars) is inside the grab - leave it
                // undisplaced. Smoke/particles never write depth, so they
                // stay displaceable. rawZ == 0/1 means no valid depth reading
                // (depth texture unbound or sky), which never occludes.
                float rawZ = tex2D(_CameraDepthTexture, suvBase).r;
                float sceneZ = LinearEyeDepth(rawZ);
                float occluded = (rawZ > 0.0001 && rawZ < 0.9999 && sceneZ < i.eyeDepth - 0.05) ? 1.0 : 0.0;

                // centered on the billboard (not bottom-anchored): smoke puffs
                // sit in the middle of each particle. Vertical factor keeps a
                // slight flattening.
                float2 d = float2(abs(i.uv.x - 0.5) * 2.0, abs(i.uv.y - 0.5) * 2.0 * 1.35);
                float dn = length(d) / max(_EffectRadius, 0.05);
                float edgeFade = (1.0 - smoothstep(0.55, 1.0, dn)) * (1.0 - occluded);

                // rising turbulent field: vertically stretched cells, moving
                // up at the flow-dependent speed, slow lateral evolution.
                // Full amplitude - the alpha blend applies the edge fade.
                float2 np = i.uv * float2(4.0 * _Freq, 1.8 * _Freq)
                          - float2(_AnimTime * 0.13, _AnimTime);
                float n1 = fbm(np);
                float n2 = fbm(np + float2(37.2, 17.9));
                float2 offset = (float2(n1, n2) - 0.5) * _Strength;

                // debug 1: mask coverage
                if (_Debug > 0.5 && _Debug < 1.5)
                {
                    return half4(edgeFade.xxx, 1.0);
                }
                // debug 2: raw grab, no offset - validates the grab path
                if (_Debug > 1.5 && _Debug < 2.5)
                {
                    return tex2D(_TurboHeatGrab, suvBase);
                }
                // debug 3: computed offset (RG, +-0.05 = full swing) + mask (B)
                if (_Debug > 2.5 && _Debug < 3.5)
                {
                    return half4(offset * 20.0 + 0.5, edgeFade, 1.0);
                }
                // debug 4: solid magenta - proves geometry + material render
                if (_Debug > 3.5)
                {
                    return half4(1.0, 0.0, 1.0, 1.0);
                }

                // coverage: edge fade x per-particle shimmer envelope
                // (colorOverLifetime decay, via the color alpha stream),
                // transparent where occluded by foreground geometry - so
                // overlapping particles composite instead of overwriting
                // note: offset stays at full amplitude; the decay only blends the
                // displaced grab back over the original. Scaling offset by i.color.a
                // as well would make the wobble shrink instead of dissolve.
                half4 scene = tex2D(_TurboHeatGrab, suvBase + offset);
                float a = edgeFade * i.color.a;
                return half4(scene.rgb, a);
            }
            ENDCG
        }
    }
}

