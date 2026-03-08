Shader "Custom/Player"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half4 _EmissionColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);
                half3 viewDirWS = normalize(GetCameraPositionWS() - IN.positionWS);

                Light mainLight = GetMainLight();
                half atten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                // 拡散（ランバート）
                half3 diffuse = LightingLambert(mainLight.color, mainLight.direction, normalWS);
                half3 ambient = SampleSH(normalWS);
                half3 lighting = ambient + diffuse * atten;

                // スペキュラ（Blinn-Phong、スムースネスでハイライトの鋭さを制御）
                half shininess = 1.0 + _Smoothness * 127.0;
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half spec = pow(saturate(dot(normalWS, halfDir)), shininess);
                half3 specularF0 = lerp(0.04, _BaseColor.rgb, _Metallic);
                half3 specular = spec * specularF0 * mainLight.color * atten;

                // 拡散はメタルでは弱く、スペキュラはメタルで強く。エミッションは加算。
                half3 color = _BaseColor.rgb * lighting * (1.0 - _Metallic) + specular + _EmissionColor.rgb;
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
