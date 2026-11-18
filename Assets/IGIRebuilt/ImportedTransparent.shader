Shader "IGI Rebuilt/Imported transparent surface"
{
    Properties
    {
        _MainTex ("Source texture", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Face culling", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull [_Cull]
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard fullforwardshadows alpha:fade
        sampler2D _MainTex;
        half _Glossiness;
        struct Input { float2 uv_MainTex; float facing : VFACE; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 texel = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = texel.rgb;
            o.Alpha = texel.a;
            o.Smoothness = _Glossiness;
            o.Normal = float3(0, 0, IN.facing >= 0 ? 1 : -1);
        }
        ENDCG
    }
    Fallback Off
}
