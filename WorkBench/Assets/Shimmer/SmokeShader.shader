Shader "TurboTurbo/Smoke"
{
    Properties
    {
        _MainTex ("Smoke Texture", 2D) = "white" {}
        // this deserves some testing to see if it's actually necessary
        _FacingFloor ("Facing Floor", Range(0, 1)) = 0.6
        _Saturation ("Light Saturation", Range(0, 1)) = 0.35
        _MaxShadowFloor ("Max Shadow Floor", Range(0, 1)) = 0.65
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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR; // rgb = model color, a = fade envelope
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
                float alpha = tex.a * i.color.a;

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
