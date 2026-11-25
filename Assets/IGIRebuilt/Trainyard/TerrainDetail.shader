Shader "IGI Rebuilt/Terrain detail" {
 Properties { _MainTex ("Original terrain tile", 2D) = "white" {} }
 SubShader { Tags { "Queue"="Geometry+2" "RenderType"="Transparent" } Cull Off ZWrite Off Offset -2,-2 Blend SrcAlpha OneMinusSrcAlpha
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #include "UnityCG.cginc"
 sampler2D _MainTex;
 struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
 struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float3 world:TEXCOORD1; UNITY_FOG_COORDS(2) };
 v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; o.world=mul(unity_ObjectToWorld,v.vertex).xyz; UNITY_TRANSFER_FOG(o,o.pos); return o; }
 fixed4 frag(v2f i):SV_Target { fixed4 c=tex2D(_MainTex,i.uv); c.a=i.color.a * saturate((65-distance(_WorldSpaceCameraPos,i.world))/40); UNITY_APPLY_FOG(i.fogCoord,c); return c; }
 ENDCG
 } } }
