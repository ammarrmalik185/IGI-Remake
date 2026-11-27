Shader "Hidden/IGIRebuilt/WeaponScope"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "white" {}
        _Mask ("Original scope quadrant", 2D) = "white" {}
        _Horizontal ("Original horizontal reticle", 2D) = "white" {}
        _Vertical ("Original vertical reticle", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _Mask, _Horizontal, _Vertical;
            float4 _PixelSize;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 quarter = float2(1 - abs(uv.x - .5) * 2, abs(uv.y - .5) * 2);
                float alpha = tex2D(_Mask, quarter).a;
                float hy = (uv.y - .5) / (8 * _PixelSize.y) + .5;
                float vx = (uv.x - .5) / (8 * _PixelSize.x) + .5;
                if (hy >= 0 && hy <= 1) alpha = max(alpha, tex2D(_Horizontal, float2(uv.x, hy)).a);
                if (vx >= 0 && vx <= 1) alpha = max(alpha, tex2D(_Vertical, float2(vx, uv.y)).a);
                return fixed4(tex2D(_MainTex, uv).rgb * (1 - alpha), 1);
            }
            ENDCG
        }
    }
}
