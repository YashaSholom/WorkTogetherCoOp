Shader "Coop/Portal Energy"
{
 SubShader { Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" } Cull Off
 Pass { HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
 V vert(float4 p:POSITION,float2 uv:TEXCOORD0) { V o; o.p=TransformObjectToHClip(p.xyz); o.uv=uv; return o; }
 half4 frag(V i):SV_Target {
   float2 p=(i.uv-.5)*2; float r=length(p); clip(1-r); float a=atan2(p.y,p.x);
   float turbulence=sin(a*13+r*26+_Time.y)*sin(p.x*29-p.y*18)*.18;
   float f=pow(saturate(sin(r*42-a*3-_Time.y*1.6+turbulence)),14);
   float threads=pow(saturate(sin(r*103+a*7+_Time.y*2+turbulence)),24)*.35;
   float edge=pow(saturate(r),14);
   float2 cell=floor(i.uv*180);float seed=frac(sin(dot(cell,float2(12.9898,78.233)))*43758.5453);
   float stars=step(.998,seed)*pow(saturate(1-length(frac(i.uv*180)-.5)*2),2);
   float3 c=lerp(float3(.055,.015,.18),float3(.015,.30,.63),r);
   c+=f*lerp(float3(.6,.04,.9),float3(.02,1.4,2),r)+threads*float3(.14,.42,.7)+edge*float3(.05,2,3)+stars*float3(1,1.4,2);
   return half4(c,1);
 }
 ENDHLSL }
 } Fallback Off
}
