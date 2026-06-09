Shader "Olivia/TailingLiquid"
{
    Properties
    {
        _BaseColor ("Shallow Color", Color) = (0.42, 0.27, 0.12, 1)
        _DeepColor ("Deep Color", Color) = (0.16, 0.08, 0.03, 1)
        _EmissionColor ("Wet Highlight", Color) = (0.24, 0.12, 0.04, 1)
        _FillY ("World Liquid Surface", Float) = 0
        _DepthRange ("Depth Range", Range(0.1, 8)) = 2.6
        _Alpha ("Transparency", Range(0, 1)) = 0.72
        _FresnelPower ("Fresnel", Range(0.2, 8)) = 2.8
        _SpecPower ("Specular Sharpness", Range(4, 256)) = 72
        _SpecIntensity ("Specular Intensity", Range(0, 4)) = 1.35
        _RippleScale ("Ripple Scale", Range(0.1, 20)) = 5.2
        _RippleSpeed ("Ripple Speed", Range(0, 4)) = 0.75
        _RippleStrength ("Ripple Strength", Range(0, 0.5)) = 0.075
        _SwirlSpeed ("Agitator Swirl", Range(0, 6)) = 1
        _SwirlStrength ("Swirl Strength", Range(0, 1)) = 0.38
        _CenterX ("Tank Center X", Float) = 0
        _CenterZ ("Tank Center Z", Float) = 0
        _Reaction ("Neutralization Reaction", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DeepColor;
                float4 _EmissionColor;
                float _FillY;
                float _DepthRange;
                float _Alpha;
                float _FresnelPower;
                float _SpecPower;
                float _SpecIntensity;
                float _RippleScale;
                float _RippleSpeed;
                float _RippleStrength;
                float _SwirlSpeed;
                float _SwirlStrength;
                float _CenterX;
                float _CenterZ;
                float _Reaction;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.worldNormal = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float3 ProcessNormal(float3 normal, float3 worldPos)
            {
                float time = _Time.y * _RippleSpeed;
                float waveX = sin(worldPos.x * _RippleScale + time);
                float waveZ = cos(worldPos.z * (_RippleScale * 0.83) - time * 1.17);
                float2 delta = worldPos.xz - float2(_CenterX, _CenterZ);
                float radius = max(length(delta), 0.05);
                float angle = atan2(delta.y, delta.x);
                float swirl = sin(angle * 3.0 - _Time.y * _SwirlSpeed * 4.0 + radius * 2.2);
                float2 tangent = float2(-delta.y, delta.x) / radius;
                float2 reaction = float2(
                    sin(worldPos.z * 8.0 + _Time.y * 5.0),
                    cos(worldPos.x * 7.0 - _Time.y * 4.3)
                ) * _Reaction;
                float2 perturb = float2(waveX, waveZ) * _RippleStrength;
                perturb += tangent * swirl * _SwirlStrength * 0.08;
                perturb += reaction * 0.035;
                return normalize(normal + float3(perturb.x, 0, perturb.y));
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(_FillY - input.worldPos.y);

                float3 viewDir = normalize(_WorldSpaceCameraPos - input.worldPos);
                float3 normal = normalize(input.worldNormal);
                if (dot(normal, viewDir) < 0) normal = -normal;
                normal = ProcessNormal(normal, input.worldPos);

                Light light = GetMainLight();
                float3 lightDir = normalize(light.direction);
                float depth = saturate((_FillY - input.worldPos.y) / max(_DepthRange, 0.01));
                half3 liquidColor = lerp(_BaseColor.rgb, _DeepColor.rgb, depth);
                half3 diffuse = liquidColor * (0.48 + 0.52 * saturate(dot(normal, lightDir)));

                float3 halfDir = normalize(lightDir + viewDir);
                float specular = pow(saturate(dot(normal, halfDir)), _SpecPower) * _SpecIntensity;
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);
                float surfaceBand = saturate(1.0 - (_FillY - input.worldPos.y) / 0.18);
                half3 reactionGlow = lerp(_EmissionColor.rgb, half3(0.68, 0.76, 0.60), _Reaction);
                half3 color = diffuse + light.color * specular + reactionGlow * (0.12 + surfaceBand * 0.55);
                float alpha = saturate(lerp(_Alpha, 0.98, fresnel + depth * 0.42) + surfaceBand * 0.12);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
