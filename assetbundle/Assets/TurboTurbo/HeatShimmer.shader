Shader "TurboTurbo/HeatShimmer"
{
    Properties
    {
        _MainTex ("Heat DUDV (RG = offset)", 2D) = "gray" {}
        _Strength ("Distortion Strength", Float) = 0.5
        _Debug ("Debug View", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        // grab the lit scene behind the quad; works in DV's deferred path
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

            sampler2D _MainTex;
            sampler2D _GrabTexture;
            float _Strength;
            float _Debug;

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

            half4 frag (v2f i) : SV_Target
            {
                float4 map = tex2D(_MainTex, i.uv);
                float2 offset = (map.rg - 0.5) * _Strength;

                // debug 1: raw map sample (R,G = offsets, B = mask)
                if (_Debug > 0.5 && _Debug < 1.5)
                {
                    return half4(map.rgb, 1.0);
                }
                // debug 2: raw grab, no offset - validates the grab path
                if (_Debug > 1.5 && _Debug < 2.5)
                {
                    return tex2D(_GrabTexture, i.grabUV.xy / i.grabUV.w);
                }
                // debug 3: computed offset (RG, +-0.05 = full swing) and
                // _Strength (B, 0..1) - validates the distortion math
                if (_Debug > 2.5)
                {
                    return half4(offset * 20.0 + 0.5, saturate(_Strength), 1.0);
                }
                // debug 4: solid magenta - proves geometry + material render
                if (_Debug > 3.5)
                {
                    return half4(1.0, 0.0, 1.0, 1.0);
                }

                // DUDV-style offset map: 0.5 = no distortion. The mask (map
                // blue) fades the offset to zero at the quad's edges, so no
                // rim/alpha trickery is needed: the displaced background IS
                // the effect. Offset is applied AFTER projection so
                // _Strength is in true screen-UV units.
                float2 suv = i.grabUV.xy / i.grabUV.w + offset;
                return tex2D(_GrabTexture, suv);
            }
            ENDCG
        }
    }
}
