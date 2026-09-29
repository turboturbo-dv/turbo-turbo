Shader "TurboTurbo/Smoke"
{
    Properties
    {
        _MainTex ("Smoke Texture", 2D) = "white" {}
        // this deserves some testing to see if it's actually necessary
        _FacingFloor ("Facing Floor", Range(0, 1)) = 0.6
        _Saturation ("Light Saturation", Range(0, 1)) = 0.35
        _MaxShadowFloor ("Max Shadow Floor", Range(0, 1)) = 0.65
        _MinFadeDist ("Min Camera Fade Distance", Range(0, 10)) = 1
        _MaxFadeDist ("Max Camera Fade Distance", Range(0, 10)) = 5
        _DensityScale ("Density Scale", Float) = 1
        _DensityFalloff ("Density Falloff", Float) = 1.5
        _SoftParticlesFade ("Soft Particles Fade (m)", Range(0, 1)) = 0.15
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
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float _FacingFloor;
            float _Saturation;
            float _MaxShadowFloor;
            float _MinFadeDist;
            float _MaxFadeDist;
            float _DensityScale;
            float _DensityFalloff;
            float _SoftParticlesFade;
            sampler2D_float _CameraDepthTexture;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR; // rgb = tint, a = encoded particulate mass
                float4 texcoords : TEXCOORD0; // xy = uv, z = size, w = normalized age
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : TEXCOORD1;
                float4 screenPos : TEXCOORD3;
                float eyeDepth : TEXCOORD4;
                UNITY_FOG_COORDS(2)
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

                // note: gamma decode requires corresponding encode on the emitter side
                // gamma decode -> rescale -> apply size fade
                float tau = (v.color.a * v.color.a) * _DensityScale / pow(max(1e-6, size), _DensityFalloff);

                // brief fade in at birth avoids pop-in
                float fadeIn = saturate(age / 0.025);
                
                // size fade alone gives polynomial decay, which does not reach zero,
                // so multiply by a smooth fade-to-zero at end of life
                float ageFade = 1.0 - smoothstep(0.0, 1.0, saturate((age - 0.75) / 0.25));

                // decode + rescale process can result in tau > 1, which is deliberate as it allows particles
                // to hold at max opacity for a while, so it needs a saturate
                o.color.a = saturate(tau) * fadeIn * ageFade * camFade;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // fade the particle out as it approaches opaque geometry, so a quad
                // poking through a surface doesn't end in a hard clipped edge
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float rawZ = tex2D(_CameraDepthTexture, screenUV).r;
                float sceneZ = LinearEyeDepth(rawZ);
                float softFade = saturate((sceneZ - i.eyeDepth) / max(1e-4, _SoftParticlesFade));

                half4 tex = tex2D(_MainTex, i.uv);
                float alpha = tex.a * i.color.a * softFade;

                // compress the dynamic range on bright colours, to avoid white
                // smoke having unnaturally dark shadows, while preserving
                // detail in darker colours
                half tintLum = Luminance(i.color.rgb);
                half dynamicFloor = tintLum * _MaxShadowFloor;
                half3 remappedTex = lerp(dynamicFloor.xxx, 1.0.xxx, tex.rgb);

                float3 L = _WorldSpaceLightPos0.xyz;
                float facing = lerp(_FacingFloor, 1.0, saturate(L.y));
                half3 light = ShadeSH9(half4(0, 1, 0, 1)) + _LightColor0.rgb * facing;

                // de-intensify the light colour tint, otherwise the smoke turns
                // too yellow at sunset and too blue at night
                half lightLum = Luminance(light);
                light = lerp(lightLum.xxx, light, _Saturation);

                half4 col = half4(remappedTex * i.color.rgb * light, alpha);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
