Shader "TurboTurbo/SmokeDebug"
{
    Properties
    {
        _CheckerScale ("Checker Cells", Float) = 8
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

            float _CheckerScale;

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
                // red/dark-red checkerboard: proves UVs arrive correctly
                float2 cell = floor(i.uv * _CheckerScale);
                float on = fmod(cell.x + cell.y, 2.0);

                // vertex color ignored for now (isolating UV delivery)
                float a = 1.0;
                return half4(on.xxx * half3(1.0, 0.1, 0.1), a);
            }
            ENDCG
        }
    }
}
