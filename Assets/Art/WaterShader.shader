Shader "Custom/WaterShader"
{
    Properties
    {
        _DeepColor("Deep Water Color", Color) = (0,0.25,0.8,1)
        _ShallowColor("Shallow Water Color", Color) = (0.4,0.8,1,1)
        _FresnelColor("Fresnel Color", Color) = (1,1,1,1)
        _FresnelPower("Fresnel Power", Range(0.1,5.0)) = 2.0
        _Transparency("Transparency", Range(0,1)) = 0.8
        _Amplitude1("Amplitude1", Range(0,1)) = 0.1
        _Frequency1("Frequency1", Range(0,10)) = 3
        _Speed1("Speed1", Range(0,10)) = 1
        _Direction1("Direction1 (X,Z)", Vector) = (1,0,0,0)
        _Amplitude2("Amplitude2", Range(0,1)) = 0.06
        _Frequency2("Frequency2", Range(0,10)) = 2
        _Speed2("Speed2", Range(0,10)) = 1.3
        _Direction2("Direction2 (X,Z)", Vector) = (0,1,0,0)
        _Amplitude3("Amplitude3", Range(0,1)) = 0.08
        _Frequency3("Frequency3", Range(0,10)) = 4
        _Speed3("Speed3", Range(0,10)) = 1.8
        _Direction3("Direction3 (X,Z)", Vector) = (0.7,0.7,0,0)
        _FoamColor("Foam Color", Color) = (1,1,1,1)
        _FoamTexture("Foam Texture (R)", 2D) = "white" {}
        _FoamScale("Foam Texture Scale", Range(0,10)) = 3.0
        _FoamIntensity("Foam Intensity", Range(0,2)) = 1.0
        _SeaLevel("Base Sea Level", Float) = 0.0
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
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 clipPos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float waveHeight : TEXCOORD3; // altura de la ola
            };
            float4 _DeepColor;
            float4 _ShallowColor;
            float4 _FresnelColor;
            float _FresnelPower;
            float _Transparency;
            float _Amplitude1, _Frequency1, _Speed1; float4 _Direction1;
            float _Amplitude2, _Frequency2, _Speed2; float4 _Direction2;
            float _Amplitude3, _Frequency3, _Speed3; float4 _Direction3;
            float4 _FoamColor;
            sampler2D _FoamTexture;
            float _FoamScale;
            float _FoamIntensity;
            float _SeaLevel;
            float3 GerstnerWave(float3 pos, float amplitude, float frequency, float speed, float2 direction)
            {
                float theta = dot(direction, pos.xz) * frequency + (_Time.y * speed);
                float sinT = sin(theta);
                float cosT = cos(theta);
                float3 offset;
                offset.x = amplitude * cosT * direction.x;
                offset.z = amplitude * cosT * direction.y;
                offset.y = amplitude * sinT;
                return offset;
            }
            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float2 dir1 = normalize(_Direction1.xy);
                float2 dir2 = normalize(_Direction2.xy);
                float2 dir3 = normalize(_Direction3.xy);
                float3 waveOff1 = GerstnerWave(worldPos, _Amplitude1, _Frequency1, _Speed1, dir1);
                float3 waveOff2 = GerstnerWave(worldPos, _Amplitude2, _Frequency2, _Speed2, dir2);
                float3 waveOff3 = GerstnerWave(worldPos, _Amplitude3, _Frequency3, _Speed3, dir3);
                float3 totalOffset = waveOff1 + waveOff2 + waveOff3;
                worldPos += totalOffset;
                o.waveHeight = worldPos.y - _SeaLevel;
                o.clipPos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.worldPos = worldPos;
                o.normal = worldNormal;
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fresnelFactor = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float shallowFactor = saturate(i.worldPos.y * 0.5 + 0.5);
                float3 waterColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, shallowFactor);
                float3 finalColor = lerp(waterColor, _FresnelColor.rgb, fresnelFactor);
                float foamEdge0 = 0.02;  
                float foamEdge1 = 0.10;  
                float foamFactorHeight = smoothstep(foamEdge0, foamEdge1, i.waveHeight);
                float slopeFactor = 1.0 - saturate(dot(N, float3(0,1,0)));
                slopeFactor *= 0.5;
                float2 foamUV = i.uv * _FoamScale;
                float foamTex = tex2D(_FoamTexture, foamUV).r;
                float foamFactor = foamFactorHeight + slopeFactor;
                foamFactor *= foamTex;
                foamFactor = saturate(foamFactor * _FoamIntensity);
                float3 foamColor = _FoamColor.rgb;
                finalColor = lerp(finalColor, foamColor, foamFactor);
                return fixed4(finalColor, _Transparency);
            }
            ENDCG
        }
    }
    FallBack Off
}