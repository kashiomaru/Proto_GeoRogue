Shader "Custom/EnemyBullet"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
        [Header(Facing Camera Additive)]
        [HDR] _RimColor("Rim Color", Color) = (0.5, 0.5, 1, 1)
        _RimPower("Rim Power", Range(0.1, 20)) = 2
        _RimStrength("Rim Strength", Range(0, 1)) = 0.3
        _RimMaxAdd("Rim Max Add", Range(0, 2)) = 1
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
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half _RimMaxAdd;
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

                // 法線がカメラ側を向いているほど加算する色（RimPower で範囲、RimStrength で強さ、RimMaxAdd で上限）
                half NdotV = saturate(dot(normalWS, viewDirWS));
                half rimFactor = pow(NdotV, _RimPower);
                half3 rimAdd = min(_RimColor.rgb * rimFactor * _RimStrength, _RimMaxAdd);

                // 拡散はメタルでは弱く、スペキュラはメタルで強く。エミッション・リムは加算。
                half3 color = _BaseColor.rgb * lighting * (1.0 - _Metallic) + specular + _EmissionColor.rgb + rimAdd;
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
