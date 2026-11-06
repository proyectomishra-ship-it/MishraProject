// Interior de portal "cielo estrellado" con aspecto realista:
// nebulosa con detalle y vetas de polvo, miles de estrellas diminutas de brillo variable
// (pocas brillantes con halo y destellos finos), borde de luz irregular, bruma volumétrica
// en el piso y PARALLAX: el fondo se desplaza detrás del marco al mover la cámara.
// Procedural (sin texturas). URP, doble cara. Va en un Quad DENTRO del marco de piedra.
Shader "Custom/FantasyPortalStars"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _DeepColor("Deep Sky", Color) = (0.004, 0.008, 0.05, 1)
        [HDR] _NebulaColorA("Nebula A", Color) = (0.035, 0.09, 0.36, 1)
        [HDR] _NebulaColorB("Nebula B (matiz secundario)", Color) = (0.22, 0.08, 0.38, 1)
        [HDR] _GlowColor("Edge / Floor Glow", Color) = (0.22, 0.5, 1.1, 1)
        [HDR] _MistColor("Mist", Color) = (0.3, 0.6, 1.0, 1)
        [HDR] _StarCold("Star Cold (azuladas)", Color) = (0.7, 0.85, 1.5, 1)
        [HDR] _StarWarm("Star Warm (cálidas)", Color) = (1.5, 1.05, 0.75, 1)
        _Opacity("Opacity", Range(0, 1)) = 1

        [Header(Shape)]
        _Aspect("Aspect (ancho / alto del quad)", Float) = 0.77
        _ArchSpring("Arch Spring (donde empieza la curva)", Range(0, 0.95)) = 0.55
        _EdgeSoftness("Edge Softness", Range(0.005, 0.3)) = 0.035
        _EdgeGlow("Edge Glow", Range(0, 3)) = 0.7

        [Header(Depth)]
        _ParallaxDepth("Parallax Depth", Range(0, 0.6)) = 0.18

        [Header(Stars)]
        _StarAmount("Star Amount", Range(0, 1)) = 0.5
        _StarBrightness("Star Brightness", Range(0, 4)) = 2
        _DustAmount("Fine Star Dust", Range(0, 1)) = 0.8
        _Twinkle("Twinkle Speed", Range(0, 8)) = 1.5

        [Header(Movement)]
        _Drift("Drift Speed", Range(-0.5, 0.5)) = 0.015
        _NebulaScale("Nebula Scale", Range(0.5, 6)) = 2
        _NebulaAmount("Nebula Amount", Range(0, 2)) = 1
        _MistAmount("Mist Amount", Range(0, 2)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "PortalStars"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _NebulaColorA;
                half4 _NebulaColorB;
                half4 _GlowColor;
                half4 _MistColor;
                half4 _StarCold;
                half4 _StarWarm;
                half _Opacity;
                half _Aspect;
                half _ArchSpring;
                half _EdgeSoftness;
                half _EdgeGlow;
                half _ParallaxDepth;
                half _StarAmount;
                half _StarBrightness;
                half _DustAmount;
                half _Twinkle;
                half _Drift;
                half _NebulaScale;
                half _NebulaAmount;
                half _MistAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewOS : TEXCOORD1; // de la superficie hacia la cámara, en espacio objeto
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                float3 camOS = TransformWorldToObject(GetCameraPositionWS());
                OUT.viewOS = camOS - IN.positionOS.xyz;
                return OUT;
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

            // 0..1 aprox. Con más octavas hay más detalle fino.
            float Fbm5(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    v += ValueNoise(p) * amp;
                    p = p * 2.03 + 17.0;
                    amp *= 0.5;
                }
                return v / 0.96875;
            }

            float Fbm3(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += ValueNoise(p) * amp;
                    p = p * 2.03 + 17.0;
                    amp *= 0.5;
                }
                return v / 0.875;
            }

            // Capa de estrellas. maxBright=1: incluye brillantes con halo y destellos;
            // maxBright=0: solo puntos finos (polvo estelar).
            float3 StarLayer(float2 uv, float density, float speed, float seed, float amount, float maxBright)
            {
                float t = _Time.y;
                float2 g = float2(uv.x * _Aspect, uv.y) * density;
                g.y -= t * speed * density;

                float2 id = floor(g) + seed;
                float2 f = frac(g) - 0.5;

                float h1 = Hash21(id);
                float h2 = Hash21(id + 7.7);
                float h3 = Hash21(id + 13.1);
                float h4 = Hash21(id + 29.3);

                float2 q = f - (float2(h2, h3) - 0.5) * 0.2;

                // La mayoría son tenues; muy pocas son brillantes
                float mag = pow(h4, 5.0);
                float bright = lerp(0.12, 1.0, mag) * lerp(0.55, 1.0, maxBright);
                float R = lerp(0.03, 0.16, mag) * lerp(0.5, 1.0, maxBright);

                float r2 = dot(q, q);
                float core = exp(-r2 / (R * R * 0.12));
                // El halo se apaga antes del borde de la celda: sin esto se ven recortes cuadrados
                float cellFade = 1.0 - smoothstep(0.22, 0.46, max(abs(q.x), abs(q.y)));
                float halo = exp(-sqrt(r2) / (R * 1.4)) * 0.16 * step(0.55, mag) * maxBright * cellFade;

                // Destellos finos solo en las más brillantes
                float spikes = 0.0;
                if (mag > 0.6 && maxBright > 0.5)
                {
                    float sx = exp(-abs(q.y) * 90.0) * smoothstep(R * 2.6, 0.0, abs(q.x));
                    float sy = exp(-abs(q.x) * 90.0) * smoothstep(R * 2.6, 0.0, abs(q.y));
                    spikes = (sx + sy) * 0.45 * cellFade;
                }

                float present = step(1.0 - amount, h1);
                float tw = 0.75 + 0.25 * sin(t * _Twinkle * (0.5 + h3) + h1 * 60.0);

                float3 tint = lerp(_StarCold.rgb, _StarWarm.rgb, step(0.78, h3));
                return tint * present * (core + halo + spikes) * bright * tw;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float2 uv = IN.uv;

                // x: -1..1 ; y: 0 (piso) .. 1 (tope del arco)
                float x = (uv.x - 0.5) * 2.0;
                float y = uv.y;

                float yArch = max(y - _ArchSpring, 0.0) / max(1.0 - _ArchSpring, 0.001);
                float d = sqrt(x * x + yArch * yArch);

                float mask = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, d);
                if (mask <= 0.001) return half4(0, 0, 0, 0);

                // Parallax: lo más lejano se desplaza más respecto del marco
                float3 v = IN.viewOS;
                float2 shift = -v.xy / max(abs(v.z), 0.25) * _ParallaxDepth;
                float2 uvNeb = uv + shift * 0.9;

                // ---------- Fondo ----------
                float3 col = lerp(_GlowColor.rgb * 0.12, _DeepColor.rgb, pow(saturate(y), 0.45));

                // ---------- Nebulosa con domain warping ----------
                float2 np = float2((uvNeb.x - 0.5) * 1.6, uvNeb.y * 1.2) * _NebulaScale;
                float2 drift = float2(0.0, -t * _Drift);
                float2 warp = float2(Fbm3(np + drift), Fbm3(np + float2(5.2, 1.3) + drift));
                float neb = Fbm5(np + warp * 2.0);
                float dust = smoothstep(0.55, 0.78, Fbm3(np * 1.7 + 9.0 + warp));   // vetas oscuras
                float nebMask = smoothstep(0.35, 0.85, neb) * (1.0 - 0.6 * dust);
                float3 nebColor = lerp(_NebulaColorA.rgb, _NebulaColorB.rgb, smoothstep(0.3, 0.7, Fbm3(np * 0.6 + 3.0)));
                col += nebColor * nebMask * _NebulaAmount;

                // ---------- Estrellas ----------
                float3 stars = StarLayer(uv + shift * 0.5, 5.0, _Drift * 1.0, 0.0, _StarAmount * 0.5, 1.0)
                             + StarLayer(uv + shift * 0.7, 10.0, _Drift * 0.7, 31.0, _StarAmount * 0.7, 1.0) * 0.8
                             + StarLayer(uv + shift * 0.9, 20.0, _Drift * 0.4, 67.0, _StarAmount, 1.0) * 0.6;
                // Polvo estelar fino: más denso donde hay nebulosa
                float3 fine = StarLayer(uv + shift * 1.0, 48.0, _Drift * 0.2, 101.0, _DustAmount * 0.35, 0.0)
                            * (0.4 + nebMask * 1.4);
                stars = (stars + fine) * smoothstep(1.0, 0.85, d); // no pegadas al marco
                col += stars * _StarBrightness;

                // ---------- Borde: luz irregular, no un contorno parejo ----------
                float edgeNoise = Fbm3(float2(x * 3.0, y * 3.0 - t * 0.12));
                float edge = pow(smoothstep(0.78, 1.0, d), 2.5) * (0.35 + edgeNoise * 1.3);
                col += _GlowColor.rgb * edge * _EdgeGlow;
                col *= 1.0 - 0.25 * smoothstep(0.5, 0.95, d); // el interior se oscurece hacia el marco

                // ---------- Bruma en el piso (volumétrica, capas) ----------
                float2 mp = float2(x * 1.3 + sin(t * 0.2) * 0.25, y * 2.4 - t * 0.04);
                float m1 = Fbm5(mp + Fbm3(mp * 1.3 - t * 0.03) * 1.5);
                float m2 = Fbm3(mp * 2.1 + float2(3.7, -t * 0.07));
                float floorFade = smoothstep(0.38, 0.0, y);
                float mist = floorFade * (0.15 + m1 * 0.8 + m2 * 0.4) * _MistAmount;
                col += _MistColor.rgb * mist;
                col += _GlowColor.rgb * exp(-y * 7.0) * 0.25; // luz que sale del piso

                // Dither para evitar bandas en degradados oscuros
                col += (Hash21(uv * 997.0 + frac(t)) - 0.5) / 255.0;

                return half4(max(col, 0.0), mask * _Opacity);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
