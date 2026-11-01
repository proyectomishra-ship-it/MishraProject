Shader "Custom/WaterShader"
{
    Properties
    {
        [Header(Color)]
        _DeepColor("Deep Water Color", Color) = (0.03, 0.13, 0.15, 1)
        _ShallowColor("Shallow Water Color", Color) = (0.22, 0.4, 0.33, 1)
        _FresnelColor("Sky Reflection Tint", Color) = (0.6, 0.72, 0.8, 1)
        _FresnelPower("Fresnel Power", Range(0.1, 8.0)) = 4.0
        _FresnelStrength("Reflection Strength", Range(0, 1)) = 0.55
        _SkyBlend("Real Sky Reflection (0 = tint only)", Range(0, 1)) = 0.8
        _Transparency("Max Opacity", Range(0, 1)) = 0.92
        _Density("Water Density (1/m)", Range(0.05, 5)) = 0.7
        _LightInfluence("Scene Light Influence", Range(0, 1)) = 0.85

        [Header(River Flow)]
        _FlowDirection("Flow Direction (X,Z)", Vector) = (0, 1, 0, 0)
        [Toggle] _FlowUseObjectZ("Flow Along Object Z (blue arrow)", Float) = 0
        _FlowSpeed("Flow Speed", Range(0, 5)) = 0.8
        _RippleScale("Ripple Scale", Range(0.05, 4)) = 0.6
        _RippleStretch("Ripple Stretch Along Flow", Range(1, 8)) = 3
        _NormalStrength("Ripple Strength", Range(0, 4)) = 1.2

        [Header(Swell optional)]
        _Amplitude1("Amplitude1", Range(0, 1)) = 0.03
        _Frequency1("Frequency1", Range(0, 10)) = 0.5
        _Speed1("Speed1", Range(0, 10)) = 1
        _Direction1("Direction1 (X,Z)", Vector) = (1, 0, 0, 0)
        _Amplitude2("Amplitude2", Range(0, 1)) = 0.02
        _Frequency2("Frequency2", Range(0, 10)) = 0.9
        _Speed2("Speed2", Range(0, 10)) = 1.3
        _Direction2("Direction2 (X,Z)", Vector) = (0, 1, 0, 0)
        _Amplitude3("Amplitude3", Range(0, 1)) = 0
        _Frequency3("Frequency3", Range(0, 10)) = 1.5
        _Speed3("Speed3", Range(0, 10)) = 1.8
        _Direction3("Direction3 (X,Z)", Vector) = (0.7, 0.7, 0, 0)
        _VertexWaves("Vertex Displacement (0 = off)", Range(0, 1)) = 0

        [Header(Sun Highlight)]
        _SunDirection("Fallback Sun Direction (X,Y,Z)", Vector) = (0.4, 0.7, 0.3, 0)
        _SpecPower("Highlight Sharpness", Range(8, 512)) = 160
        _SpecIntensity("Highlight Intensity", Range(0, 3)) = 0.6

        [Header(Depth and Shore)]
        [Toggle] _UseDepth("Use Depth Texture", Float) = 1
        _DepthFade("Depth Fade (m)", Range(0.1, 30)) = 3
        _ShoreFoamWidth("Shore Foam Width (m)", Range(0.05, 5)) = 0.5

        [Header(Foam)]
        _FoamColor("Foam Color", Color) = (0.9, 0.95, 0.95, 1)
        _FoamTexture("Foam Texture (R, optional)", 2D) = "black" {}
        _FoamScale("Foam Scale", Range(0, 10)) = 3.0
        _FoamIntensity("Foam Intensity", Range(0, 2)) = 0.6
        _FoamThreshold("Foam Rapids Threshold", Range(0, 1)) = 0.78
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
            float _SkyBlend;
            float _Transparency;
            float _Density;
            float _LightInfluence;

            float4 _FlowDirection;
            float _FlowUseObjectZ;
            float _FlowSpeed;
            float _RippleScale;
            float _RippleStretch;
            float _NormalStrength;

            float _Amplitude1, _Frequency1, _Speed1; float4 _Direction1;
            float _Amplitude2, _Frequency2, _Speed2; float4 _Direction2;
            float _Amplitude3, _Frequency3, _Speed3; float4 _Direction3;
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

            // Luz principal que URP publica como variables globales.
            float4 _MainLightPosition;
            half4 _MainLightColor;

            sampler2D_float _CameraDepthTexture;

            // Dirección normalizada; si es (0,0) no devuelve NaN.
            float2 SafeDir(float4 d)
            {
                float2 v = d.xy;
                float l = dot(v, v);
                return l > 1e-6 ? v * rsqrt(l) : float2(1, 0);
            }

            // Sentido de la corriente en el plano XZ del mundo.
            float2 FlowDir()
            {
                float2 f = SafeDir(_FlowDirection);
                if (_FlowUseObjectZ > 0.5)
                {
                    float2 o = float2(unity_ObjectToWorld._m02, unity_ObjectToWorld._m22);
                    float l = dot(o, o);
                    if (l > 1e-6) f = o * rsqrt(l);
                }
                return f;
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

            // Relieve del río: ruido estirado a lo largo de la corriente y desplazándose con ella.
            float RiverHeight(float2 p, float2 flow)
            {
                float2 side = float2(-flow.y, flow.x);
                float along = dot(p, flow);
                float across = dot(p, side);
                float tt = _Time.y * _FlowSpeed * _RippleScale / _RippleStretch;

                float2 q = float2(across, along / _RippleStretch) * _RippleScale;

                float h = ValueNoise(q + float2(0.0, -tt)) * 0.55;
                h += ValueNoise(q * 2.1 + float2(5.2, -tt * 1.4)) * 0.30;
                h += ValueNoise(q * 4.3 + float2(9.1, -tt * 2.0)) * 0.15;
                return h;
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
                float2 p = i.worldPos.xz;
                float2 flow = FlowDir();

                // Normal por pixel: ondas de río + oleaje suave opcional.
                float e = 0.25 / max(_RippleScale, 0.05);
                float h0 = RiverHeight(p, flow);
                float hx = RiverHeight(p + float2(e, 0), flow);
                float hz = RiverHeight(p + float2(0, e), flow);
                float2 grad = float2(hx - h0, hz - h0) / e;

                float3 sw = AllWaves(p);
                float3 N = normalize(float3(-(grad.x * _NormalStrength + sw.y),
                                            1.0,
                                            -(grad.y * _NormalStrength + sw.z)));
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);

                // Profundidad respecto del suelo (Depth Texture activado en el URP asset).
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                float surfZ = i.screenPos.w;
                float depthDiff = max(sceneZ - surfZ, 0.0);

                // 0 = poco profundo, 1 = profundo.
                float deep = lerp(0.6, saturate(depthDiff / _DepthFade), _UseDepth);
                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, deep);

                // Reflejo del cielo real (skybox / reflection probe) mezclado con un tinte.
                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float3 R = reflect(-V, N);
                half4 skyData = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, R, 1.0);
                float3 sky = DecodeHDR(skyData, unity_SpecCube0_HDR);
                float3 reflCol = lerp(_FresnelColor.rgb, sky, _SkyBlend);
                float3 col = lerp(waterColor, reflCol, saturate(fres * _FresnelStrength));

                // Brillo del sol: usa la luz principal de la escena (o la dirección de respaldo).
                float3 L = normalize(_SunDirection.xyz);
                if (dot(_MainLightPosition.xyz, _MainLightPosition.xyz) > 1e-4)
                    L = normalize(_MainLightPosition.xyz);

                float3 sunCol = float3(1, 1, 1);
                if (dot(_MainLightColor.rgb, _MainLightColor.rgb) > 1e-6)
                    sunCol = _MainLightColor.rgb;

                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), _SpecPower) * _SpecIntensity;
                col += spec * sunCol;

                // El agua se oscurece de noche y en escenas oscuras.
                float3 ambient = ShadeSH9(float4(0, 1, 0, 1));
                float3 lightTint = saturate(ambient + sunCol * 0.55) * 1.25;
                col *= lerp(float3(1, 1, 1), lightTint, _LightInfluence);

                // Espuma: orilla y zonas rápidas, con ruido en coordenadas de MUNDO.
                float2 fp = p * _FoamScale * 0.4;
                float noise = ValueNoise(fp + _Time.y * 0.15) * 0.6
                            + ValueNoise(fp * 2.3 - _Time.y * 0.11) * 0.4;
                float texFoam = tex2D(_FoamTexture, p * _FoamScale * 0.05 + _Time.y * 0.01).r;
                float pattern = saturate(noise + texFoam);

                float rapids = smoothstep(_FoamThreshold, 1.0, h0);
                float shore = (1.0 - saturate(depthDiff / _ShoreFoamWidth)) * _UseDepth;
                float foam = saturate((rapids + shore) * pattern * _FoamIntensity);

                col = lerp(col, _FoamColor.rgb * lerp(float3(1, 1, 1), lightTint, _LightInfluence), foam);

                // Opacidad según la profundidad: transparente en la orilla, más opaco al fondo.
                float opacityDepth = _Transparency * (1.0 - exp(-depthDiff * _Density));
                float opacity = lerp(_Transparency * 0.8, opacityDepth, _UseDepth);
                opacity = saturate(opacity + fres * _FresnelStrength * 0.4);
                opacity = max(opacity, foam * 0.85);

                return fixed4(col, opacity);
            }
            ENDCG
        }
    }
    FallBack Off
}
