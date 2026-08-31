Shader "TurboTurbo/Smoke"
{
    Properties
    {
        _MainTex ("Smoke Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

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
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // unlit smoke: atlas tile (TSA UVs baked by the renderer)
                // tinted by the per-particle model color, faded by the
                // envelope alpha
                half4 tex = tex2D(_MainTex, i.uv);
                float alpha = tex.a * i.color.a;
                return half4(tex.rgb * i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
