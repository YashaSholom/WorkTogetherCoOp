Shader "Coop/Waterfall Veil"
{
    Properties {_BaseColor("Water",Color)=(.65,.92,1,1)}
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half4 _BaseColor;
            struct A{float4 pos:POSITION;float2 uv:TEXCOORD0;};struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;half fog:TEXCOORD1;};
            V Vert(A i){V o;o.pos=TransformObjectToHClip(i.pos.xyz);o.uv=i.uv;o.fog=ComputeFogFactor(o.pos.z);return o;}
            half4 Frag(V i):SV_Target{
                float streak=.5+.5*sin(i.uv.x*61+sin(i.uv.y*35-_Time.y*5)*.6);
                float flow=.5+.5*sin(i.uv.y*90-_Time.y*8);
                float edge=smoothstep(0,.13,i.uv.x)*smoothstep(0,.13,1-i.uv.x);
                float alpha=edge*(.32+streak*.5)*smoothstep(1,.87,i.uv.y);
                half3 col=lerp(_BaseColor.rgb,half3(1,1,1),streak*.5+flow*.15);
                return half4(MixFog(col,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
