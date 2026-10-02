Shader "Coop/Stratified Sandstone"
{
    Properties {
        _BaseColor("Sandstone", Color) = (.68,.30,.13,1)
        _BandColor("Sediment", Color) = (.9,.56,.31,1)
        _BandScale("Layer frequency", Float) = .7
    }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _BandColor;
            float _BandScale;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;half fog:TEXCOORD2;};
            V Vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionCS=p.positionCS;o.world=p.positionWS;o.normal=TransformObjectToWorldNormal(i.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
            half4 Frag(V i):SV_Target {
                float ripple=sin(i.world.x*.08)+sin(i.world.z*.11)*.6;
                float layer=.5+.5*sin((i.world.y+ripple)*_BandScale);
                layer=smoothstep(.42,.85,layer)*.48;
                float grain=frac(sin(dot(floor(i.world*9),float3(12.99,78.23,34.1)))*43758.54);
                half3 albedo=lerp(_BaseColor.rgb,_BandColor.rgb,layer)*( .95+grain*.10);
                Light light=GetMainLight();half3 n=normalize(i.normal);
                half3 lit=albedo*(SampleSH(n)+light.color*(saturate(dot(n,light.direction))*.8+.12));
                return half4(MixFog(lit,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
