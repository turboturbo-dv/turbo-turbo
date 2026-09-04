Shader "TurboTurbo/Smoke"
{
    Properties
    {
        _MainTex ("Smoke Texture", 2D) = "white" {}
        // this deserves some testing to see if it's actually necessary
        _FacingFloor ("Facing Floor", Range(0, 1)) = 0.6
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
                // atlas tile (TSA UVs baked by the renderer) tinted by the
                // per-particle model color, faded by the envelope alpha
                half4 tex = tex2D(_MainTex, i.uv);
                float alpha = tex.a * i.color.a;

                // fixed UP normal (top-lit fake lighting, like vanilla): the
                // facing factor is the sun's elevation; the floor keeps a low
                // sun from blacking the plume out. directional dirs are unit
                // length, no normalize needed
                float3 L = _WorldSpaceLightPos0.xyz;
                float facing = lerp(_FacingFloor, 1.0, saturate(L.y));
                half3 light = ShadeSH9(half4(0, 1, 0, 1)) + _LightColor0.rgb * facing;

                half4 col = half4(tex.rgb * i.color.rgb * light, alpha);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
