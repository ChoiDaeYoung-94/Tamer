Shader "Custom/FogOfWar"
{
    Properties
    {
        _Persistent_FoW("Texture", 2D) = "white" {}
        _Dynamic_FoW("Texture", 2D) = "white" {}
        _EdgeMin("Edge Min", Float) = 0.0
        _EdgeMax("Edge Max", Float) = 1.0
        _Opacity("Opacity", Float) = 1.0
        _UseStatic("Use Static", Float) = 0.0
    }
        SubShader
        {
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Always

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"

                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f
                {
                    float2 uv : TEXCOORD0;
                    float4 vertex : SV_POSITION;
                };

                sampler2D _Persistent_FoW;
                sampler2D _Dynamic_FoW;

                float _EdgeMin;
                float _EdgeMax;
                float _Opacity;
                float _UseStatic;

                v2f vert(appdata v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    fixed4 col1 = tex2D(_Dynamic_FoW, i.uv);
                    fixed4 col2 = max(tex2D(_Persistent_FoW, i.uv), float4(_UseStatic, _UseStatic, _UseStatic, _UseStatic));

                    // OpenGL에서 pow 함수 사용 시 음수에 대한 처리
                    float opacityAdjusted = abs(_Opacity); // 절대값으로 변환하여 pow 사용
                    float result = max(pow(opacityAdjusted, 0.25) * smoothstep(_EdgeMin, _EdgeMax, 1.0 - col1.r), smoothstep(_EdgeMin, _EdgeMax, 1.0 - col2.r));

                    return fixed4(0.1, 0.1, 0.1, result);
                }
                ENDCG
            }
        }
}