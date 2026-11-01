Shader "Custom/WaterShader"
{
    Properties
    {
        [Header(Color)]
        _DeepColor("Deep Water Color", Color) = (0.02, 0.2, 0.45, 1)
        _ShallowColor("Shallow Water Color", Color) = (0.15, 0.65, 0.75, 1)
        _FresnelColor("Sky Reflection Color", Color) = (0.55, 0.75, 0.95, 1)
        _FresnelPower("Fresnel Power", Range(0.1, 8.0)) = 3.0
        _FresnelStrength("Fresnel Strength", Range(0, 1)) = 0.6
        _Transparency("Transparency", Range(0, 1)) = 0.85

        [Header(Waves)]
        _Amplitude1("Amplitude1", Range(0, 1)) = 0.10
        _Frequency1("Frequency1", Range(0, 10)) = 0.9
        _Speed1("Speed1", Range(0, 10)) = 1
        _Direction1("Direction1 (X,Z)", Vector) = (1, 0, 0, 0)
        _Amplitude2("Amplitude2", Range(0, 1)) = 0.07
        _Frequency2("Frequency2", Range(0, 10)) = 1.6
        _Speed2("Speed2", Range(0, 10)) = 1.3
        _Direction2("Direction2 (X,Z)", Vector) = (0, 1, 0, 0)
        _Amplitude3("Amplitude3", Range(0, 1)) = 0.05
        _Frequency3("Frequency3", Range(0, 10)) = 2.7
        _Speed3("Speed3", Range(0, 10)) = 1.8
        _Direction3("Direction3 (X,Z)", Vector) = (0.7, 0.7, 0, 0)
        _NormalStrength("Ripple Strength", Range(0, 4)) = 1.5
        _VertexWaves("Vertex Displacement (0 = off)", Range(0, 1)) = 0

        [Header(Sun Highlight)]
        _SunDirection("Sun Direction (X,Y,Z)", Vector) = (0.4, 0.7, 0.3, 0)
        _SpecPower("Highlight Sharpness", Range(8, 512)) = 160
        _SpecIntensity("Highlight Intensity", Range(0, 3)) = 0.8

        [Header(Depth and Shore)]
        [Toggle] _UseDepth("Use Depth Texture", Float) = 1
        _DepthFade("Depth Fade (m)", Range(0.1, 30)) = 4
        _ShoreFoamWidth("Shore Foam Width (m)", Range(0.05, 5)) = 0.8

        [Header(Foam)]
        _FoamColor("Foam Color", Color) = (1, 1, 1, 1)
        _FoamTexture("Foam Texture (R, optional)", 2D) = "black" {}
        _FoamScale("Foam Scale", Range(0, 10)) = 3.0
        _FoamIntensity("Foam Intensity", Range(0, 2)) = 1.0
        _FoamThreshold("Foam Crest Threshold", Range(0, 1)) = 0.78
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 clipPos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            float4 _DeepColor;
            float4 _ShallowColor;
            float4 _FresnelColor;
            float _FresnelPower;
            float _FresnelStrength;
            float _Transparency;

            float _Amplitude1, _Frequency1, _Speed1; float4 _Direction1;
            float _Amplitude2, _Frequency2, _Speed2; float4 _Direction2;
            float _Amplitude3, _Frequency3, _Speed3; float4 _Direction3;
            float _NormalStrength;
            float _VertexWaves;

            float4 _SunDirection;
            float _SpecPower;
            float _SpecIntensity;

            float _UseDepth;
            float _DepthFade;
            float _ShoreFoamWidth;

            float4 _FoamColor;
            sampler2D _FoamTexture;
            float _FoamScale;
            float _FoamIntensity;
            float _FoamThreshold;

            sampler2D_float _CameraDepthTexture;

            // Dirección normalizada; si es (0,0) no devuelve NaN.
            float2 SafeDir(float4 d)
            {
                float2 v = d.xy;
                float l = dot(v, v);
                return l > 1e-6 ? v * rsqrt(l) : float2(1, 0);
            }

            // x = altura de la ola, yz = pendiente (dh/dx, dh/dz)
            float3 Wave(float2 p, float amp, float freq, float speed, float2 dir)
            {
                float theta = dot(dir, p) * freq + _Time.y * speed;
                float s = sin(theta);
                float c = cos(theta);
                return float3(amp * s, amp * freq * c * dir.x, amp * freq * c * dir.y);
            }

            float3 AllWaves(float2 p)
            {
                return Wave(p, _Amplitude1, _Frequency1, _Speed1, SafeDir(_Direction1))
                     + Wave(p, _Amplitude2, _Frequency2, _Speed2, SafeDir(_Direction2))
                     + Wave(p, _Amplitude3, _Frequency3, _Speed3, SafeDir(_Direction3));
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;

                // Desplazamiento opcional: solo tiene sentido con una malla muy subdividida.
                wp.y += AllWaves(wp.xz).x * _VertexWaves;

                o.worldPos = wp;
                o.clipPos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                o.screenPos = ComputeScreenPos(o.clipPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Olas y normal calculadas POR PIXEL: no dependen de cuántos vértices tenga el plano.
                float3 w = AllWaves(i.worldPos.xz);
                float totalAmp = max(_Amplitude1 + _Amplitude2 + _Amplitude3, 1e-4);
                float h01 = saturate(w.x / totalAmp * 0.5 + 0.5);   // 0 = valle, 1 = cresta

                float3 N = normalize(float3(-w.y * _NormalStrength, 1.0, -w.z * _NormalStrength));
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);

                // Profundidad respecto del suelo (necesita Depth Texture activado en el URP asset).
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                float surfZ = i.screenPos.w;
                float depthDiff = max(sceneZ - surfZ, 0.0);

                // 0 = poco profundo, 1 = profundo. Sin depth texture, un leve gradiente según la ola.
                float deep = lerp(1.0 - h01, saturate(depthDiff / _DepthFade), _UseDepth);

                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, deep);

                // Reflejo del cielo según el ángulo (con la normal ondulada, ya no es uniforme).
                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float3 col = lerp(waterColor, _FresnelColor.rgb, saturate(fres * _FresnelStrength));

                // Brillo del sol.
                float3 L = normalize(_SunDirection.xyz);
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), _SpecPower) * _SpecIntensity;
                col += spec;

                // Espuma: solo en las crestas y en la orilla, con patrón de ruido en coordenadas de MUNDO.
                float2 fp = i.worldPos.xz * _FoamScale * 0.4;
                float noise = ValueNoise(fp + _Time.y * 0.15) * 0.6
                            + ValueNoise(fp * 2.3 - _Time.y * 0.11) * 0.4;
                float texFoam = tex2D(_FoamTexture, i.worldPos.xz * _FoamScale * 0.05 + _Time.y * 0.01).r;
                float pattern = saturate(noise + texFoam);

                float crest = smoothstep(_FoamThreshold, 1.0, h01);
                float shore = (1.0 - saturate(depthDiff / _ShoreFoamWidth)) * _UseDepth;
                float foam = saturate((crest + shore) * pattern * _FoamIntensity);

                col = lerp(col, _FoamColor.rgb, foam);

                // Más transparente donde es poco profundo.
                float alpha = saturate(lerp(_Transparency * 0.35, _Transparency, deep) + foam * 0.4);

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    FallBack Off
}
