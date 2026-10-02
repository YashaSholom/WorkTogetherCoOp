Shader "Coop/Holo Document Display"
{
    Properties {
        _BaseColor("Holographic cyan",Color)=(.05,.48,.62,.72)
        _ScanStrength("Scan lines",Range(0,.3))=.06
    }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _ScanStrength;
            CBUFFER_END
            struct A{float4 pos:POSITION;float2 uv:TEXCOORD0;};struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vert(A i){V o;o.pos=TransformObjectToHClip(i.pos.xyz);o.uv=i.uv;return o;}
            half4 Frag(V i):SV_Target {
                float lines=.5+.5*sin(i.uv.y*460-_Time.y*2);
                float edge=pow(saturate(abs(i.uv.x-.5)*2),18)+pow(saturate(abs(i.uv.y-.5)*2),18);
                half3 color=_BaseColor.rgb*(.45+lines*_ScanStrength)+half3(.08,.6,.75)*saturate(edge)*.4;
                return half4(color,_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
