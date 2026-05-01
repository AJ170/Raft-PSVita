// Optimized Water Shader for PS Vita
Shader "Water/VitaWaterOptimized" {
    Properties{
        _Color("Water Base Color", Color) = (0,0,0,1)
        _MainTex("Underwater Texture", 2D) = "white" {}
        _floorDistort("Underwater Distort", Range(0.0, 1.0)) = 0.6
        _floorDepth("Underwater Depth", Range(0.0, 1.0)) = 0.6
        _floorPos("Underwater Position", Vector) = (0.005, 0.005, 0.0, 0.0)
        _fresnelTex("Fresnel Tex", 2D) = "white" {}

        _FoamTex("WaterFoam", 2D) = "white" {}
        _Offsetmap("WaveOffsetAlphas", 2D) = "white" {}
        _WaveHeight("WaveHeight", Range(0.0, 5.0)) = 2.0
        _BumpMap("Normalmap", 2D) = "white" {}

        _BumpTiling("Bump Tiling", Vector) = (1.0, 1.0, 1.0, 1.0)
        _RippleTiling("Ripple Tiling", Vector) = (0.001, 0.001, 0.001, 0.001)
        _FoamTiling("Foam Tiling", Vector) = (0.001, 0.001, 0.001, 0.001)
        _WaterDistort("Wave/Surface Distortion", Float) = 1.0
        _WaterCube("Water Cube Map", CUBE) = "" {}
        _LightingTex("Flat Lighting Tex", 2D) = "white" {}
        _WaterDistantCol("Water Distant Color", Color) = (0.0, 0.0, 1.0, 1.0)
        _PeakDirection("Peak Direction", Vector) = (0.0, 1.0, 0.0, 0.0)
        _warpFactor("Mesh Clustering Warp Factor", Float) = 0.5
        _fallOff("Mesh Clustering Falloff", Float) = 7.6
    }

        SubShader{
            Tags { "RenderType" = "Opaque" "Queue" = "Geometry-10"}
            LOD 400
            Offset 1, 10

            Pass {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 2.0  // Changed from 3.0 - Vita doesn't support SM3.0 features
                // Removed: #pragma exclude_renderers - let Unity handle this

                #include "UnityCG.cginc"

                // Samplers
                samplerCUBE _WaterCube;
                sampler2D _MainTex;
                sampler2D _BumpMap;
                sampler2D _Offsetmap;
                sampler2D _FoamTex;
                sampler2D _LightingTex;
                sampler2D _fresnelTex;

                // Properties
                fixed4 _Color;
                half _floorDistort;
                half _floorDepth;
                half _WaterDistort;
                half4 _WaterDistantCol;
                half _WaveHeight;
                half4 _floorPos;
                half4 _BumpTiling;
                half4 _RippleTiling;
                half4 _FoamTiling;
                half4 _PeakDirection;
                half _warpFactor;
                half _fallOff;

                // Uniforms passed from script
                uniform float3 cornerUL;
                uniform float3 cornerUR;
                uniform float3 cornerBL;
                uniform float3 cornerBR;
                uniform float3 camPos;
                uniform float2 viewCenter;
                uniform half4 _bumpTransOffset;
                uniform half4 _waveTransOffset;
                uniform half4 _foamTransOffset;

                struct vertexInput {
                    float4 vertex : POSITION;
                    float3 normal : NORMAL;
                    float2 texcoord0 : TEXCOORD0;  // Changed from float4 to float2
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct fragmentInput {
                    float4 position : SV_POSITION;
                    half4 bumpcoord : TEXCOORD0;
                    half4 floorUV : TEXCOORD1;
                    half3 viewDir : TEXCOORD2;      // Reduced from half4 to half3
                    half4 peakUV : TEXCOORD3;
                    half2 peakColor : TEXCOORD4;    // Reduced from fixed4 to half2 (only using .x anyway)
                    half4 rippleCoords : TEXCOORD5;
                    half dist : TEXCOORD6;          // Separated out for precision
                };

                // Warp UV coordinates toward view center
                float2 warpUV(float2 uv) {
                    half skeinFactor = 1.0 - saturate(length(uv - viewCenter) * _fallOff);
                    return lerp(uv, viewCenter, _warpFactor * skeinFactor);
                }

                // Calculate world position using bilinear interpolation
                float3 bilinearDirectionPosition(float2 position, out half dist, out float3 viewDir) {
                    float3 lerpA = lerp(cornerUR, cornerUL, position.x);
                    float3 lerpB = lerp(cornerBR, cornerBL, position.x);
                    float3 lerpC = lerp(lerpB, lerpA, position.y);

                    viewDir = lerpC;

                    // Avoid division by very small numbers
                    half divisor = max(abs(lerpC.y), 0.01);
                    lerpC = lerpC * camPos.y / divisor;

                    dist = length(lerpC);
                    return camPos - lerpC;
                }

                fragmentInput vert(vertexInput v) {
                    fragmentInput o;
                    UNITY_SETUP_INSTANCE_ID(v);

                    half dist;
                    float3 viewDir;

                    // Calculate bilinear world position
                    v.vertex.xyz = bilinearDirectionPosition(warpUV(v.texcoord0.xy), dist, viewDir);

                    // World coordinates
                    float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                    o.floorUV = worldPos;

                    // Calculate ripple coordinates
                    o.rippleCoords = worldPos.xzxz * _RippleTiling.xyzw + _waveTransOffset.xyzw;

                    // Sample wave height with LOD
                    // CRITICAL FIX: tex2Dlod in vertex shader is expensive on Vita
                    // Consider pre-baking or simplifying
                    half lod = dist * 5.5 / 400.0;
                    half height = tex2Dlod(_Offsetmap, half4(o.rippleCoords.xy, 0, lod)).a;
                    height += tex2Dlod(_Offsetmap, half4(o.rippleCoords.zw, 0, lod)).a;

                    // Peak color for foam
                    o.peakColor.x = saturate(height * 4.0 - 4.5);
                    o.peakColor.x = lerp(o.peakColor.x, 0.0, saturate(dist / 600.0));
                    o.peakColor.y = 0.0; // Unused

                    // Apply wave displacement
                    v.vertex.xyz += _PeakDirection.xyz * height * _WaveHeight;

                    // Transform to clip space
                    o.position = UnityObjectToClipPos(v.vertex);

                    // Store view direction
                    o.viewDir = normalize(viewDir);

                    // Calculate texture coordinates
                    o.peakUV = worldPos.xzxz * _FoamTiling.xyzw + _foamTransOffset.xyzw;
                    o.bumpcoord = worldPos.xzxz * _BumpTiling.xyzw + _bumpTransOffset.xyzw;
                    o.floorUV = worldPos * _floorPos.xxyy + _floorPos.zzww + half4(o.viewDir.xyzz) * _floorDepth;
                    o.dist = dist;

                    return o;
                }

                // Sample and combine two bump map samples
                half3 UnpackGetBump(sampler2D bumpTexture, half4 uv) {
                    half3 bump1 = tex2D(bumpTexture, uv.xy).xyz;
                    half3 bump2 = tex2D(bumpTexture, uv.zw).xyz;
                    half3 bump = (bump1 + bump2) * 2.0 - 2.0;
                    return bump * 0.5;
                }

                half4 frag(fragmentInput i) : SV_Target {
                    // Sample bump maps at two scales
                    half3 bump = UnpackGetBump(_BumpMap, i.bumpcoord);
                    half3 bumpFar = UnpackGetBump(_BumpMap, i.rippleCoords);

                    // Combine normals
                    half3 worldNormal = (bump + bumpFar) * 0.5;

                    // Calculate reflection vector
                    // FIX: Normalize worldNormal before reflection
                    worldNormal = normalize(worldNormal);
                    half3 reflectVector = reflect(-i.viewDir, worldNormal.xzy);

                    // Sample cubemap reflection
                    half4 waterCube = texCUBE(_WaterCube, reflectVector);

                    // Calculate Fresnel
                    // FIX: Dot product needs normalized vectors
                    half fresnel = saturate(tex2D(_fresnelTex, half2(dot(-i.viewDir, worldNormal.xzy), 0.5)).a);

                    // Blend underwater texture with reflection based on Fresnel
                    half4 underwaterColor = tex2D(_MainTex, i.floorUV.xz + worldNormal.xz * _floorDistort);
                    waterCube = lerp(underwaterColor, waterCube, fresnel);

                    // Add foam at wave peaks
                    half4 foam1 = tex2D(_FoamTex, i.peakUV.xy);
                    half4 foam2 = tex2D(_FoamTex, i.peakUV.zw);
                    waterCube += (foam1 * foam2) * i.peakColor.x;

                    // Apply lighting
                    half4 lighting = tex2D(_LightingTex, worldNormal.xy * 0.5 + 0.5);

                    return waterCube * lighting;
                }

                ENDCG
            }
        }

            Fallback "Mobile/Diffuse"
}