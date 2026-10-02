Shader "Coop/Canyon Sky"
{
    Properties {
        _Zenith("Zenith",Color)=(.32,.51,.80,1)
        _Horizon("Horizon",Color)=(.94,.64,.52,1)
        _Cloud("Cloud blush",Color)=(1,.82,.72,1)
    }
    SubShader {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half4 _Zenith,_Horizon,_Cloud;
            struct A{float4 vertex:POSITION;};struct V{float4 pos:SV_POSITION;float3 direction:TEXCOORD0;};
            V Vert(A i){V o;o.pos=TransformObjectToHClip(i.vertex.xyz);o.direction=i.vertex.xyz;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 f=frac(p);float2 a=floor(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
            half4 Frag(V i):SV_Target {
                float3 d=normalize(i.direction);half3 c=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),.55));
                float2 p=d.xz/max(d.y+.25,.1)*2;
                float clouds=noise(p)*.5+noise(p*2.1)*.3+noise(p*4.2)*.2;
                float veil=smoothstep(.53,.75,clouds)*smoothstep(.02,.12,d.y)*(1-smoothstep(.55,.9,d.y));
                return half4(lerp(c,_Cloud.rgb,veil*.7),1);
            }
            ENDHLSL
        }
    }
}
