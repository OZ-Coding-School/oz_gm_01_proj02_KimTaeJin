Shader "PlayArea/PlayAreaFog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.45, 0.55, 0.65, 1)
        _FogWidth ("Fog Width", Float) = 3
        _FogEdgeOpacity ("Fog Edge Opacity", Range(0,1)) = 0.65
        _FogMaxOpacity ("Fog Max Opacity", Range(0,1)) = 1
        _FogRampPower ("Fog Ramp Power", Range(0.1,4)) = 1.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "PlayAreaFog"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment FullscreenFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float4 _FogColor;
            float _FogWidth;
            float _FogEdgeOpacity;
            float _FogMaxOpacity;
            float _FogRampPower;

            int _BoundaryCount;
            float4 _BoundaryPoints[32];
            float4x4 _BoundaryWorldToLocal;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_Position;
                float2 uv : TEXCOORD0;
            };

            Varyings FullscreenVert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float2 ClosestPointOnSegment(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float abSqr = dot(ab, ab);
                if (abSqr <= 1e-6) return a;
                float t = dot(p - a, ab) / abSqr;
                t = saturate(t);
                return a + ab * t;
            }

            bool IsPointInside(float2 p)
            {
                bool inside = false;
                int count = _BoundaryCount;
                if (count < 3) return false;
                for (int i = 0, j = count - 1; i < count; j = i++)
                {
                    float2 a = _BoundaryPoints[i].xy;
                    float2 b = _BoundaryPoints[j].xy;
                    bool intersect = ((a.y > p.y) != (b.y > p.y)) &&
                        (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y + 1e-6) + a.x);
                    if (intersect) inside = !inside;
                }
                return inside;
            }

            float DistanceToEdges(float2 p)
            {
                float best = 1e8;
                int count = _BoundaryCount;
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    float2 a = _BoundaryPoints[i].xy;
                    float2 b = _BoundaryPoints[next].xy;
                    float2 c = ClosestPointOnSegment(p, a, b);
                    float d = length(p - c);
                    best = min(best, d);
                }
                return best;
            }

            half4 FullscreenFrag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
                float depth = SampleSceneDepth(uv);
                if (depth >= 0.99999 || _BoundaryCount < 3)
                    return scene;

                float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 local = mul(_BoundaryWorldToLocal, float4(worldPos, 1.0)).xyz;
                float2 p = local.xz;

                bool inside = IsPointInside(p);
                if (inside)
                    return scene;

                float dist = DistanceToEdges(p);
                float t = smoothstep(0.0, max(0.001, _FogWidth), dist);
                t = pow(saturate(t), max(0.1, _FogRampPower));
                float fog = lerp(_FogEdgeOpacity, _FogMaxOpacity, t);
                fog = saturate(fog);

                return lerp(scene, _FogColor, fog);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
