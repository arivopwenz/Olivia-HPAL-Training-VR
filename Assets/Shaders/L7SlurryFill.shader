Shader "Olivia/L7SlurryFill"
{
    Properties
    {
        _BaseColor ("Slurry Color", Color) = (0.55, 0.18, 0.65, 0.65)
        _EmissionColor ("Emission Color", Color) = (0.65, 0.25, 0.75, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 0.5
        _FillY ("World Fill Y (top of slurry)", Float) = -1000
        _SurfaceGlow ("Surface Glow", Range(0, 5)) = 1.8
        _SurfaceWidth ("Surface Width", Range(0.01, 2)) = 0.3
        _Alpha ("Alpha (Transparency)", Range(0, 1)) = 0.65
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
                float4 _EmissionColor;
                float _EmissionIntensity;
                float _FillY;
                float _SurfaceGlow;
                float _SurfaceWidth;
                float _Alpha;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Clip semua pixel di atas garis fill (= di atas permukaan slurry).
                clip(_FillY - IN.worldPos.y);

                // Lighting basic
                Light mainLight = GetMainLight();
                float3 N = normalize(IN.worldNormal);
                float3 L = normalize(mainLight.direction);
                float ndotl = saturate(dot(N, L));

                half3 diffuse = _BaseColor.rgb * (ndotl * 0.6 + 0.4);

                // Surface glow: bagian dekat permukaan air glowing lebih terang.
                float distFromSurface = _FillY - IN.worldPos.y;
                float surfaceProx = saturate(1.0 - distFromSurface / _SurfaceWidth);
                half3 surfaceGlow = _EmissionColor.rgb * surfaceProx * _SurfaceGlow;

                half3 emission = _EmissionColor.rgb * _EmissionIntensity;
                half3 finalColor = diffuse + emission + surfaceGlow;

                // Fresnel rim untuk efek translucent (lebih opaque di tepi, transparent di tengah).
                float fresnel = 1.0 - saturate(dot(N, normalize(_WorldSpaceCameraPos - IN.worldPos)));
                float alpha = lerp(_Alpha, min(1.0, _Alpha + 0.25), fresnel);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
