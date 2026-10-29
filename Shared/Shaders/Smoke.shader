Shader "TurboTurbo/Smoke"
{
    Properties
    {
        _MainTex ("Smoke Texture", 2D) = "white" {}
        // this deserves some testing to see if it's actually necessary
        _FacingFloor ("Facing Floor", Range(0, 1)) = 0.6
        _Saturation ("Light Saturation", Range(0, 1)) = 0.35
        _MaxShadowFloor ("Max Shadow Floor", Range(0, 1)) = 0.65
        _TexKnee ("Atlas Knee", Range(0.01, 2)) = 0.15
        _TexShoulder ("Atlas Shoulder", Range(0, 1)) = 1
        _MinFadeDist ("Min Camera Fade Distance", Range(0, 10)) = 1
        _MaxFadeDist ("Max Camera Fade Distance", Range(0, 10)) = 5
        _DensityScale ("Density Scale", Float) = 1
        _DensityFalloff ("Density Falloff", Float) = 1.5
        _SoftParticlesFade ("Soft Particles Fade", Range(0, 1)) = 0.3
        _FadeInSeconds ("Fade In (s)", Range(0, 0.25)) = 0.11
        // DV applies 1.8 to smoke, but 1 will case the smoke to fade just as fast as surrounding objects
        _FogBoost ("Fog Boost", Range(0, 5)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            // pins the per-draw light data to the main directional (sun/moon)
            Tags { "LightMode" = "ForwardBase" }

            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float _FacingFloor;
            float _Saturation;
            float _MaxShadowFloor;
            float _TexKnee;
            float _TexShoulder;
            float _MinFadeDist;
            float _MaxFadeDist;
            float _DensityScale;
            float _DensityFalloff;
            float _SoftParticlesFade;
            float _FadeInSeconds;
            float _FogBoost;
            sampler2D_float _CameraDepthTexture;

            // DV controls these globals to apply fog
            float4 _SceneFogParams; // x = density * sqrt(1/ln 2), y = density / ln 2, zw = linear scale/offset
            float4 _SceneFogMode; // x = UnityEngine.FogMode
            float4 _DistanceParams; // x = -start distance, z = distance fog on, w = height fog on
            float4 _DensityParams; // x = distance density
            float4 _HeightParams[2]; // per eye: x = fog top, y = camera y - fog top, z = camera inside fog, w = height density / 2

            // Fraction of the smoke that remains visible through the game's fog
            float SceneFogFactor(float3 worldPos)
            {
                float3 toCamera = _WorldSpaceCameraPos - worldPos;

                float g = _DistanceParams.x;
                if (_DistanceParams.z == 1)
                {
                    g += (length(toCamera) - _ProjectionParams.y) * _DensityParams.x;
                }
                if (_DistanceParams.w == 1)
                {
                    float4 h = _HeightParams[unity_StereoEyeIndex];
                    float fDotP = worldPos.y - h.x;
                    float c1 = h.z * (fDotP + h.y);
                    float c2 = min((1 - 2 * h.z) * fDotP, 0);
                    g -= length(h.w * toCamera) * (c1 - c2 * c2 / abs(toCamera.y + 1e-5));
                }
                g = max(g * _FogBoost, 0);

                if (_SceneFogMode.x == 1) return saturate(g * _SceneFogParams.z + _SceneFogParams.w);
                if (_SceneFogMode.x == 2) return saturate(exp2(-g * _SceneFogParams.y));
                if (_SceneFogMode.x == 3)
                {
                    float f = g * _SceneFogParams.x;
                    return saturate(exp2(-f * f));
                }
                return 1;
            }

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR; // rgb = tint, a = encoded particulate mass
                float4 texcoords : TEXCOORD0; // xy = uv, z = size, w = normalized age
                float invStartLifetime : TEXCOORD1; // x = 1/lifetime
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1; // rgb = lit tint (may exceed 1), a = opacity
                float4 screenPos : TEXCOORD3;
                float eyeDepth : TEXCOORD4;
                half shadowFloor : TEXCOORD5;
                float softFadeDist : TEXCOORD6;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoords.xy;

                // fade out near the camera so smoke doesn't enter the cab if
                // you're inside it
                float eyeDepth = -UnityObjectToViewPos(v.vertex).z;
                float camFade = smoothstep(_MinFadeDist, _MaxFadeDist, eyeDepth);

                o.screenPos = ComputeScreenPos(o.pos);
                o.eyeDepth = eyeDepth;

                o.color = v.color;
                float size = v.texcoords.z;
                float age = v.texcoords.w;

                // larger particles have expanded more, so their smoke should blend more smoothly against geometry
                o.softFadeDist = max(1e-4, _SoftParticlesFade * size);

                // note: gamma decode requires corresponding encode on the emitter side
                // gamma decode -> rescale -> apply size fade
                float tau = (v.color.a * v.color.a) * _DensityScale / pow(max(1e-6, size), _DensityFalloff);

                // brief fade in at birth avoids pop-in.
                // recover age in seconds to allow fade in to be constant over different lifetimes.
                // lets you adjust plume length without opening/closing a gap above the exhaust stack.
                float ageSeconds = age / max(1e-4, v.invStartLifetime);
                float fadeIn = saturate(ageSeconds / max(1e-4, _FadeInSeconds));
                
                // size fade alone gives polynomial decay, which does not reach zero,
                // so multiply by a smooth fade-to-zero at end of life
                float ageFade = 1.0 - smoothstep(0.0, 1.0, saturate((age - 0.75) / 0.25));

                // by the time we get to draw, the foggy scene is already rendered, so for us
                // fog is just another transparency factor
                float fog = SceneFogFactor(mul(unity_ObjectToWorld, v.vertex).xyz);

                // decode + rescale process can result in tau > 1, which is deliberate as it allows particles
                // to hold at max opacity for a while, so it needs a saturate
                o.color.a = saturate(tau) * fadeIn * ageFade * camFade * fog;

                // compress the dynamic range on bright colours, to avoid white
                // smoke having unnaturally dark shadows, while preserving
                // detail in darker colours
                o.shadowFloor = Luminance(v.color.rgb) * _MaxShadowFloor;

                // lighting is uniform across the particle, so we run this in the vertex stage
                float3 L = _WorldSpaceLightPos0.xyz;
                float facing = lerp(_FacingFloor, 1.0, saturate(L.y));

                half3 light = ShadeSH9(half4(0, 1, 0, 1)) + _LightColor0.rgb * facing;

                // de-intensify the light colour tint, otherwise the smoke turns
                // too yellow at sunset and too blue at night
                half lightLum = Luminance(light);
                light = lerp(lightLum.xxx, light, _Saturation);

                o.color.rgb = v.color.rgb * light;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // fade the particle out as it approaches opaque geometry, so a quad
                // poking through a surface doesn't end in a hard clipped edge
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float rawZ = tex2D(_CameraDepthTexture, screenUV).r;
                float sceneZ = LinearEyeDepth(rawZ);
                float softFade = saturate((sceneZ - i.eyeDepth) / i.softFadeDist);

                half4 tex = tex2D(_MainTex, i.uv);
                float alpha = tex.a * i.color.a * softFade;

                // the atlas sits mostly around the grey point, so to make light
                // smoke look bright enough, we lift it a bit
                half3 shoulder = tex.rgb * (1.0 + _TexKnee) / (tex.rgb + _TexKnee);
                half3 curve = lerp(tex.rgb, shoulder, _TexShoulder);

                half3 remappedTex = lerp(i.shadowFloor.xxx, 1.0.xxx, curve);

                return half4(remappedTex * i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
