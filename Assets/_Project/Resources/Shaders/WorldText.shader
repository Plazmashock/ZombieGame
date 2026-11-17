// Text for 3D signs (TextMesh). Unlike Unity's built-in text shader, it is hidden by walls
// (depth test ON), so signs never show through things. Lives in Resources so it's included in builds.
Shader "LastCheckout/WorldText"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = i.color;
                c.a *= tex2D(_MainTex, i.uv).a; // font letters are stored in the alpha channel
                return c;
            }
            ENDCG
        }
    }
}
