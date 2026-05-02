Shader "Custom/CorruptionVeins"
{
    Properties
    {
        _GrowProgress ("Grow Progress", Range(0,1)) = 0
        _VeinColor ("Vein Color", Color) = (0.4, 0, 0.8, 1)
        _GlowColor ("Glow Color", Color) = (0.6, 0, 1, 1)

        _VeinDensityA ("Vein Density A", Float) = 5
        _VeinDensityB ("Vein Density B", Float) = 12
        _VeinBlend ("Vein Blend A/B", Range(0,1)) = 0.4

        _Zoom ("Zoom", Float) = 8
        _PulseSpeed ("Pulse Speed", Float) = 1.5
        _VeinThickness ("Vein Thickness", Range(0.05, 0.5)) = 0.25

        _CrawlRadius ("Crawl Radius", Float) = 0
        _MaxRadius ("Max Radius", Float) = 12

        // 🌱 NEW: soil influence
        _SoilCorruption ("Soil Corruption", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 worldPos     : TEXCOORD0;
                float3 objectOrigin : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float  _GrowProgress;
                float4 _VeinColor;
                float4 _GlowColor;

                float  _VeinDensityA;
                float  _VeinDensityB;
                float  _VeinBlend;

                float  _Zoom;
                float  _PulseSpeed;
                float  _VeinThickness;

                float  _CrawlRadius;
                float  _MaxRadius;

                float  _SoilCorruption; // 🌱 NEW
            CBUFFER_END

            // -----------------------------
            // VORONOI NOISE
            // -----------------------------
            float2 VoronoiRandom(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float Voronoi(float2 uv, float density, float angleOffset)
            {
                uv *= density;
                float2 cell  = floor(uv);
                float2 local = frac(uv);

                float minDist1 = 8.0;
                float minDist2 = 8.0;

                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 neighbor = float2(x, y);
                    float2 rnd = VoronoiRandom(cell + neighbor);
                    rnd = 0.5 + 0.5 * sin(angleOffset + 6.2831 * rnd);

                    float2 diff = neighbor + rnd - local;
                    float dist = dot(diff, diff);

                    if (dist < minDist1)
                    {
                        minDist2 = minDist1;
                        minDist1 = dist;
                    }
                    else if (dist < minDist2)
                    {
                        minDist2 = dist;
                    }
                }

                return sqrt(minDist2) - sqrt(minDist1);
            }

            // -----------------------------
            // VERTEX
            // -----------------------------
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS  = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos     = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.objectOrigin = TransformObjectToWorld(float3(0, 0, 0));
                return OUT;
            }

            // -----------------------------
            // FRAGMENT
            // -----------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.worldPos.xz / _Zoom;

                float2 centreXZ = IN.objectOrigin.xz;
                float distFromCentre = length(IN.worldPos.xz - centreXZ);

                // -----------------------------
                // SOIL INFLUENCE (NEW CORE FEATURE)
                // -----------------------------
                float soil = saturate(_SoilCorruption);

                // stronger soil corruption = stronger visible spread
                float maxRadius = lerp(_MaxRadius * 0.7, _MaxRadius, soil);

                // -----------------------------
                // CRAWL MASK
                // -----------------------------
                float crawlMask = smoothstep(_CrawlRadius, _CrawlRadius - 0.4, distFromCentre);

                if (crawlMask <= 0.001)
                    discard;

                // -----------------------------
                // EDGE ANIMATION
                // -----------------------------
                float edgeWeight  = saturate(distFromCentre / (_Zoom * 0.8));
                float timeOffsetA = _Time.y * 0.05 * edgeWeight;
                float timeOffsetB = _Time.y * 0.05 * edgeWeight * 1.3;

                float vA = Voronoi(uv, _VeinDensityA, timeOffsetA);
                float vB = Voronoi(uv, _VeinDensityB, timeOffsetB);

                float vein = lerp(vA, vB, _VeinBlend);

                // -----------------------------
                // VEIN MASK
                // -----------------------------
                float veinMask = smoothstep(0.0, _VeinThickness, vein);
                veinMask = 1.0 - veinMask;

                // -----------------------------
                // PULSE (SOIL AFFECTED)
                // -----------------------------
                float pulseSpeed = _PulseSpeed * (1.0 + soil);
                float pulse = 0.7 + 0.3 * sin(_Time.y * pulseSpeed);

                // -----------------------------
                // COLOR BLEND
                // -----------------------------
                half4 col = lerp(_VeinColor, _GlowColor, veinMask * pulse);

                // -----------------------------
                // RADIAL FADE (SOIL BOOSTED)
                // -----------------------------
                float radial = 1.0 - saturate(distFromCentre / (maxRadius * 0.95));
                radial = pow(radial, lerp(1.5, 1.0, soil));

                // -----------------------------
                // FINAL ALPHA (SOIL DRIVES INTENSITY)
                // -----------------------------
                col.a =
                    veinMask *
                    _GrowProgress *
                    radial *
                    crawlMask *
                    lerp(0.3, 1.0, soil); // 🌱 key linkage

                return col;
            }

            ENDHLSL
        }
    }
}