Shader "Olivia/L7SlurryFill"
{
    Properties
    {
        _BaseColor ("Shallow Color", Color) = (0.15, 0.55, 0.65, 0.55)
        _DeepColor ("Deep Color", Color) = (0.02, 0.18, 0.28, 0.92)
        _EmissionColor ("Emission Color", Color) = (0.1, 0.4, 0.5, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 0.25
        _FillY ("World Fill Y (top of liquid)", Float) = -1000
        _SurfaceGlow ("Surface Glow", Range(0, 8)) = 2.5
        _SurfaceWidth ("Surface Band Width", Range(0.01, 3)) = 0.45
        _DepthRange ("Depth Fade Range", Range(0.1, 30)) = 8.0
        _Alpha ("Base Alpha", Range(0, 1)) = 0.7
        _FresnelPower ("Fresnel Power", Range(0.2, 8)) = 3.0
        _SpecPower ("Specular Sharpness", Range(4, 256)) = 64
        _SpecIntensity ("Specular Intensity", Range(0, 4)) = 1.4
        _RippleScale ("Ripple Scale", Range(0.1, 20)) = 6.0
        _RippleSpeed ("Ripple Speed", Range(0, 4)) = 0.8
        _RippleStrength ("Ripple Strength", Range(0, 0.5)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200
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
                float _EmissionIntensity;
                float _FillY;
                float _SurfaceGlow;
                float _SurfaceWidth;
                float _DepthRange;
                float _Alpha;
                float _FresnelPower;
                float _SpecPower;
                float _SpecIntensity;
                float _RippleScale;
                float _RippleSpeed;
                float _RippleStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            // Cheap animated surface ripple normal perturbation.
            float3 RippleNormal(float3 baseN, float3 wp)
            {
                float t = _Time.y * _RippleSpeed;
                float r1 = sin(wp.x * _RippleScale + t) * cos(wp.z * _RippleScale * 0.9 - t * 1.1);
                float r2 = sin(wp.z * _RippleScale * 1.3 + t * 0.7);
                float3 perturb = float3(r1, 0, r2) * _RippleStrength;
                return normalize(baseN + perturb);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Clip everything above the liquid surface line.
                clip(_FillY - IN.worldPos.y);

                float3 V = normalize(_WorldSpaceCameraPos - IN.worldPos);
                float3 N = normalize(IN.worldNormal);
                // Make sure normal faces the camera (double-sided volume).
                if (dot(N, V) < 0) N = -N;
                N = RippleNormal(N, IN.worldPos);

                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float ndotl = saturate(dot(N, L));

                // Depth below surface -> color gradient shallow to deep.
                float depth = saturate((_FillY - IN.worldPos.y) / _DepthRange);
                half3 waterCol = lerp(_BaseColor.rgb, _DeepColor.rgb, depth);

                // Diffuse term (soft, water keeps ambient even in shadow).
                half3 diffuse = waterCol * (ndotl * 0.5 + 0.5);

                // Blinn-Phong specular highlight for a wet glossy surface.
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), _SpecPower) * _SpecIntensity;
                half3 specular = mainLight.color.rgb * spec;

                // Bright glowing band right at the liquid surface.
                float distFromSurface = _FillY - IN.worldPos.y;
                float surfaceProx = saturate(1.0 - distFromSurface / _SurfaceWidth);
                surfaceProx = surfaceProx * surfaceProx;
                half3 surfaceGlow = _EmissionColor.rgb * surfaceProx * _SurfaceGlow;

                half3 emission = _EmissionColor.rgb * _EmissionIntensity;
                half3 finalColor = diffuse + emission + surfaceGlow + specular;

                // Fresnel: edges/grazing angles more opaque (typical of water volumes).
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float alpha = lerp(_Alpha, 1.0, fresnel);
                // Deeper liquid reads more opaque; surface band slightly boosts alpha.
                alpha = saturate(lerp(alpha, 1.0, depth * 0.5) + surfaceProx * 0.25);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
