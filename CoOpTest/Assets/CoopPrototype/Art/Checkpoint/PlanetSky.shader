Shader "Coop/Planet Sunset Sky"
{
 Properties { _Top("Lavender zenith",Color)=(0.28,0.16,0.65,1) _Horizon("Peach horizon",Color)=(1,0.55,0.3,1) }
 SubShader { Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" } Cull Off ZWrite Off
 Pass { HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct v2f { float4 pos:SV_POSITION; float3 dir:TEXCOORD0; };
 float4 _Top,_Horizon;
 v2f vert(float4 p:POSITION) { v2f o; o.pos=UnityObjectToClipPos(p); o.dir=p.xyz; return o; }
 half4 frag(v2f i):SV_Target { float3 d=normalize(i.dir); float h=saturate(d.y*1.8); float warm=saturate(d.x*.5+.5); float3 horizon=lerp(float3(.58,.32,.72),_Horizon.rgb,warm); return half4(lerp(horizon,_Top.rgb,pow(h,.65)),1); }
 ENDHLSL }
 } Fallback Off
}
