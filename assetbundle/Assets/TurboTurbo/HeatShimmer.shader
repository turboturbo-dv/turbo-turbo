Shader "TurboTurbo/HeatShimmer"
{
    Properties
    {
        _MainTex ("Heat DUDV (RG = offset)", 2D) = "gray" {}
        _Strength ("Distortion Strength", Float) = 0.5
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
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _GrabTexture;
            float _Strength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 grabUV : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.grabUV = ComputeGrabScreenPos(o.pos);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // DUDV-style offset map: 0.5 = no distortion
                float2 offset = (tex2D(_MainTex, i.uv).rg - 0.5) * _Strength;
                float2 suv = i.grabUV.xy + offset;
                half4 scene = tex2Dproj(_GrabTexture, float4(suv, i.grabUV.z, i.grabUV.w));

                // rim fade: full refraction facing the camera, dissolving at
                // the column silhouette for a soft volumetric edge
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float rim = saturate(dot(normalize(i.normalWS), viewDir));
                float alpha = rim * rim;

                return half4(scene.rgb, alpha);
            }
            ENDCG
        }
    }
}
