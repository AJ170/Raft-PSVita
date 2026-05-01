// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'
// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Water/projectWaterVitaGerstner" {
    Properties{
        _Color("Water Base Color", Color) = (0,0,0,1)

        _MainTex("Underwater Texture", 2D) = "white" {}
        _floorDistort("Underwater Distort", Range(0.0, 1.0)) = 0.6
        _floorDepth("Underwater Depth", Range(0.0, 1.0)) = 0.6
        _floorPos("Underwater Position", Vector) = (0.005, 0.005, 0.0, 0.0)

        _FoamTex("WaterFoam", 2D) = "white" {}
        _WaveHeight("Wave Height", Range(0.0, 20.0)) = 2.0
        _BumpMap("Normalmap", 2D) = "white" {}

        _BumpTiling("Bump Tiling", Vector) = (1.0, 1.0, 1.0, 1.0)
        _FoamTiling("Foam Tiling", Vector) = (0.001, 0.001, 0.001, 0.001)
        _WaterDistort("Wave/Surface Distortion", Float) = 1.0
        _WaterCube("Water Cube Map", CUBE) = "" {}
        _LightingTex("Flat Lighting Tex", 2D) = "white" {}
        _WaterTexture("Water Surface Texture", 2D) = "white" {}
        _WaterDistantCol("Water Distant Color", Color) = (0.0, 0.0, 1.0, 1.0)

        // Gerstner Wave Parameters
        _WaveA("Wave A (dir.xy, steepness, wavelength)", Vector) = (1, 0, 0.5, 10)
        _WaveB("Wave B (dir.xy, steepness, wavelength)", Vector) = (0, 1, 0.25, 20)
        _WaveC("Wave C (dir.xy, steepness, wavelength)", Vector) = (1, 1, 0.15, 15)
        _WaveSpeed("Wave Speed", Float) = 1.0

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
                #pragma target 2.0  // Changed from 3.0 for Vita compatibility

                #include "UnityCG.cginc"

                // Samplers
                samplerCUBE _WaterCube;
                sampler2D _MainTex;
                sampler2D _BumpMap;
                sampler2D _FoamTex;
                sampler2D _LightingTex;
                sampler2D _WaterTexture;

                // Properties
                fixed4 _Color;
                half _floorDistort;
                half _floorDepth;
                half _WaterDistort;
                half4 _WaterDistantCol;
                half _WaveHeight;
                half4 _floorPos;
                half4 _BumpTiling;
                half4 _FoamTiling;
                half _warpFactor;
                half _fallOff;

                // Gerstner wave parameters
                half4 _WaveA;
                half4 _WaveB;
                half4 _WaveC;
                half _WaveSpeed;

                // Uniforms from script
                uniform float3 cornerUL;
                uniform float3 cornerUR;
                uniform float3 cornerBL;
                uniform float3 cornerBR;
                uniform float3 camPos;
                uniform float2 viewCenter;
                uniform half4 _bumpTransOffset;
                uniform half4 _foamTransOffset;

                struct vertexInput {
                    float4 vertex : POSITION;
                    float3 normal : NORMAL;
                    float4 texcoord0 : TEXCOORD0;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct fragmentInput {
                    float4 position : SV_POSITION;
                    float4 bumpcoord : TEXCOORD0;
                    float4 floorUV : TEXCOORD1;
                    float3 viewDir : TEXCOORD2;
                    float4 peakUV : TEXCOORD3;
                    float2 worldPosXZ : TEXCOORD4;  // Store world XZ for Gerstner normal calc
                    float dist : TEXCOORD5;
                };

                float2 warpUV(float2 uv) {
                    half skeinFactor = 1.0 - saturate(length(uv - viewCenter) * _fallOff);
                    return lerp(uv, viewCenter, _warpFactor * skeinFactor);
                }

                float3 bilinearDirectionPosition(fixed2 position, out fixed dist, out float3 viewDir) {
                    float3 lerpA = lerp(cornerUR, cornerUL, position.x);
                    float3 lerpB = lerp(cornerBR, cornerBL, position.x);
                    float3 lerpC = lerp(lerpB, lerpA, position.y);	//Never seems to get up to 1.0...
                    viewDir = lerpC;	//we get this for "free"
                    lerpC = lerpC * camPos.y / lerpC.y;		//calc our final position
                    dist = saturate(length(lerpC) / 300.0); //Has a much more asthetic falloff than the square magnitude
                    return camPos - lerpC;
                }

                // Gerstner Wave function
                // Returns offset (xyz) and derivative for normal calculation (w component unused here)
                float3 GerstnerWave(half4 wave, float3 p, inout float3 tangent, inout float3 binormal) {
                    half steepness = wave.z;
                    half wavelength = wave.w;
                    half k = 2.0 * UNITY_PI / wavelength;
                    half c = sqrt(9.8 / k);
                    half2 d = normalize(wave.xy);
                    half f = k * (dot(d, p.xz) - c * _Time.y * _WaveSpeed);
                    half a = steepness / k;

                    // Calculate tangent and binormal for normal derivation
                    tangent += float3(
                        -d.x * d.x * (steepness * sin(f)),
                        d.x * (steepness * cos(f)),
                        -d.x * d.y * (steepness * sin(f))
                    );
                    binormal += float3(
                        -d.x * d.y * (steepness * sin(f)),
                        d.y * (steepness * cos(f)),
                        -d.y * d.y * (steepness * sin(f))
                    );

                    return float3(
                        d.x * (a * cos(f)),
                        a * sin(f),
                        d.y * (a * cos(f))
                    );
                }

                fragmentInput vert(vertexInput v) {
                    fragmentInput o;
                    UNITY_SETUP_INSTANCE_ID(v);

                    half dist;
                    float3 viewDir;

                    // Calculate bilinear world position
                    v.vertex.xyz = bilinearDirectionPosition(v.texcoord0.xy, dist, viewDir);
                    float4 worldPos = mul(unity_ObjectToWorld, v.vertex);

                    // Store original world XZ for fragment shader Gerstner normal calculation
                    o.worldPosXZ = worldPos.xz;
                    o.dist = dist;

                    // Apply Gerstner waves in vertex shader (displacement only)
                    // We'll recalculate normals in fragment shader for per-pixel accuracy
                    float3 tangent = float3(1, 0, 0);
                    float3 binormal = float3(0, 0, 1);
                    float3 p = worldPos.xyz;

                    float3 waveOffset = GerstnerWave(_WaveA, p, tangent, binormal);
                    waveOffset += GerstnerWave(_WaveB, p, tangent, binormal);
                    waveOffset += GerstnerWave(_WaveC, p, tangent, binormal);

                    // Apply wave displacement with distance falloff
                    half distanceFalloff = 1.0 / clamp(dist * dist, 1.0, 100.0);
                    v.vertex.xyz += waveOffset * _WaveHeight * distanceFalloff;

                    // Update world position after wave displacement
                    worldPos = mul(unity_ObjectToWorld, v.vertex);
                    o.floorUV = worldPos;

                    // Transform to clip space
                    o.position = UnityObjectToClipPos(v.vertex);
                    o.viewDir = normalize(viewDir);

                    // Calculate texture coordinates
                    o.peakUV = worldPos.xzxz * _FoamTiling.xyzw + _foamTransOffset.xyzw;
                    o.bumpcoord = worldPos.xzxz * _BumpTiling.xyzw + _bumpTransOffset.xyzw;
                    o.floorUV = worldPos * _floorPos.xxyy + _floorPos.zzww + half4(o.viewDir.xyzz) * _floorDepth;

                    return o;
                }

                // Calculate Gerstner wave normal in fragment shader (per-pixel)
                float3 GerstnerNormal(half2 posXZ, half4 wave) {
                    half steepness = wave.z;
                    half wavelength = wave.w;
                    half k = 2.0 * UNITY_PI / wavelength;
                    half c = sqrt(9.8 / k);
                    half2 d = normalize(wave.xy);
                    half f = k * (dot(d, posXZ) - c * _Time.y * _WaveSpeed);
                    half a = steepness / k;

                    // Derivatives for normal calculation
                    half WA = k * a;
                    half dX = d.x * WA * cos(f);
                    half dZ = d.y * WA * cos(f);

                    return float3(-dX, 1.0, -dZ);
                }

                // Custom reflection (your vecReflect)
                float3 vecReflect(float3 incidentVec, float3 normal) {
                    return incidentVec - 2.0 * dot(incidentVec, normal) * normal;
                }

                half4 frag(fragmentInput i) : SV_Target {
                    // Calculate Gerstner wave normals per-pixel for accurate reflections
                    float3 gerstnerNormal = GerstnerNormal(i.worldPosXZ, _WaveA);
                    gerstnerNormal += GerstnerNormal(i.worldPosXZ, _WaveB);
                    gerstnerNormal += GerstnerNormal(i.worldPosXZ, _WaveC);
                    gerstnerNormal = normalize(gerstnerNormal);

                    // Sample bump map and combine with Gerstner normals
                    float3 bump = UnpackNormal(tex2D(_BumpMap, i.bumpcoord.xy));
                    bump = (bump + UnpackNormal(tex2D(_BumpMap, i.bumpcoord.zw + bump.xy * 0.05))) * 0.25;

                    // Combine Gerstner wave normals with detail bump map
                    // Weight Gerstner normals more heavily for large-scale wave shape
                    float3 worldNormal = normalize(gerstnerNormal * 0.7 + bump * 0.3);

                    // Sample water surface texture with normal-based distortion
                    float4 color = tex2D(_WaterTexture, i.bumpcoord.xy + bump.xy * 0.25);

                    // Calculate reflection
                    float3 reflectVector = vecReflect(-i.viewDir, worldNormal.xzy);
                    half4 waterCube = texCUBE(_WaterCube, reflectVector);

                    // Combine surface texture with reflection
                    color += waterCube;

                    // Apply lighting based on normal
                    //PROBLEM: This begs for a bit of optimization for Vita:
                    color *= tex2D(_LightingTex, (normalize(worldNormal).xz + 1.0) * 0.5);

                    //Get a fresnel here for handling view depths
                    half facing = 1.0 - max(dot(worldNormal, -i.viewDir), 0.0);

                    half fresnelTerm = pow(facing, 4.0f);

                    return lerp(_Color, color, fresnelTerm);
                }

                ENDCG
            }
        }

            Fallback "Mobile/Diffuse"
}