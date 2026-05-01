// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Water/projectWaterSimpleSimple" {
	Properties {	
		_Color("Water Base Color (base additive)", Color) = (0,0,0,1)
		_MainTex ("Underwater Texture", 2D) = "white" {}
		_floorDistort("Underwater Distort", Range(0.0, 1.0)) = 0.6
		_floorDepth("Underwater Depth", Range(0.0, 1.0)) = 0.6
		_floorPos ("Underwater Position (SS,PP)", Vector) = (0.005, 0.005, 0.0, 0.0)
		_fresnelTex ("Fresnel Tex", 2D) = "white"
		
		_FoamTex ("WaterFoam", 2D) = "white" {}
		_Offsetmap ("WaveOffsetAlphas", 2D) = "white" {}
		_WaveHeight ("WaveHeight", Range(0.0, 5.0)) = 2.0
     	_BumpMap ("Normalmap ", 2D) = "white" {}
     	
     	_BumpTiling ("Bump Tiling", vector) = (1.0, 1.0, 1.0, 1.0)
     	_RippleTiling ("Ripple Tiling", vector) = (0.001, 0.001, 0.001, 0.001)
     	_FoamTiling ("Foam Tiling", vector) = (0.001, 0.001, 0.001, 0.001)
     	_WaterDistort ("Wave/Surface Distortion", float) = 1.0
     	_WaterCube ("Water Cube Map", Cube) = "" {}
     	_LightingTex ("Flat Lighting Tex", 2D) = "white" {}	//Given that water is flat we really only need one face of the cubemap
     	_WaterDistantCol("Water Distant Color", Color) = (0.0, 0.0, 1.0, 1.0)
     	_PeakDirection("Peak Direction", vector) = (0.0, 1.0, 0.0, 0.0)
     	//public float warpFactor=0.2f, falloff=2f;
     	_warpFactor ("Mesh Clustering Warp Factor", float) = 0.5
     	_fallOff("Mesh Clustering Falloff", float) = 7.6
	}
	SubShader {
		Tags { "RenderType"="Opaque" "Queue"="Geometry-10"}
		LOD 400
		Offset 1, 10
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			
			//#pragma exclude_renderers d3d11 d3d9
			#include "UnityCG.cginc"
			
			fixed4 _Color;
			samplerCUBE _WaterCube; //, _LightCube;
			sampler2D _MainTex, _BumpMap, _Offsetmap, _FoamTex, _LightingTex;
			fixed _floorDistort, _floorDepth, _WaterDistort;
			
			float4 _WaterDistantCol; //may still have to be put back into the system...
			float _WaveHeight;
			float4 _floorPos;
			float4 _BumpTiling, _RippleTiling, _FoamTiling;
			half _warpFactor, _fallOff;
			
			uniform float3 cornerUL, cornerUR, cornerBL, cornerBR, camPos, centreU, centreB;
			uniform float2 viewCenter;
			uniform float4 _bumpTransOffset;
			uniform float4 _waveTransOffset;
			uniform float4 _foamTransOffset;

			struct vertexInput {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 texcoord0 : TEXCOORD0;
			};

			struct fragmentInput{
				float4 position : SV_POSITION;
				float4 bumpcoord : TEXCOORD0;
				float4 floorUV : TEXCOORD1;
				half4 viewDir : TEXCOORD2;
				half4 vtxNormalWorld : TEXCOORD3;
				float4 peakUV : TEXCOORD4;
				fixed4 peakColor : TEXCOORD5;
			};
			
			
			float2 warpUV(float2 uv) {
				half skeinFactor = 1-saturate((length(uv-viewCenter)*_fallOff)); //0 to about 2...although the edges still need to remain intact, so "warpability" should be defined by X and Y being close to 0.5
				return lerp(uv, viewCenter, _warpFactor*skeinFactor);
			}

			float3 bilinearDirectionPosition(fixed2 position, out fixed dist, out float3 viewDir) {
				float3 lerpA = lerp(cornerUR, cornerUL, position.x);
				float3 lerpB = lerp(cornerBR, cornerBL, position.x);
				float3 lerpC = lerp(lerpB, lerpA, position.y);	//Never seems to get up to 1.0...
				viewDir = lerpC;	//we get this for "free"
				lerpC = lerpC*camPos.y/lerpC.y;		//calc our final position
				dist = saturate(length(lerpC)/300.0); //Has a much more asthetic falloff than the square magnitude
				return camPos-lerpC;
			}
			
			inline fixed3 combineNormalMaps(fixed3 base, fixed3 detail) {
				base += fixed3(0,0,1);
				detail *= fixed3(-1, -1, -1);
				return (base * dot(base, detail)/base.z - detail);			
			}
						

			fragmentInput vert(vertexInput v) {
				fragmentInput o;
				fixed dist, dist2;
				float3 viewDir;			
				//v.vertex.xyz = bilinearDirectionPosition(warpUV(v.texcoord0.xy), dist, viewDir);	//projected world position of this vertex
				v.vertex.xyz = bilinearDirectionPosition(v.texcoord0.xy, dist, viewDir);	//projected world position of this vertex			
				o.floorUV = mul(unity_ObjectToWorld, v.vertex);	//World coordinates of transformed verticies					
				
				//calculating our displacements
				float4 rippleCoords = o.floorUV.xzxz * _RippleTiling.xyzw + _waveTransOffset.xyzw;
				//half lod=dist*5.5; //5.5 seems to be the magic number...
				//float height = tex2Dlod(_Offsetmap, float4(rippleCoords.xy, 0, lod)).r;
				//height = (height + tex2Dlod(_Offsetmap, float4(rippleCoords.wz, 0, lod)).r)*0.5;
				
				//o.peakColor = saturate(height*4-2);	//used for figuring out where to put foam peaks down. Hard coded might not be the best way to do this
				v.vertex.y += _WaveHeight*0.5;
				
				o.position = UnityObjectToClipPos (v.vertex);
				o.viewDir.xyz = normalize(viewDir.xyz); //Get this for "free" off the extend calcs //normalize(inlineWorldSpaceViewDir(v.vertex));	//This is correct for the reflection maps
				
				//Calculate normals from the associated normal
				float3 nrml = float3(0,1,0);
				//nrml = (tex2D(_BumpMap, rippleCoords.x).xyz+tex2D(_BumpMap, rippleCoords.zw).xyz)-1;
		        //nrml = combineNormalMaps(float3(0,-1,0), nrml.xyz);
		        
				//o.vtxNormalWorld.xyz = nrml;
				o.floorUV.xyzw += nrml.xzxz*_WaterDistort*(1-dist);
				o.peakUV = o.floorUV.xzxz * _FoamTiling.xyzw;	// + _foamTransOffset.xyzw;	//Used for our foam wave peaks
				o.bumpcoord = o.floorUV.xzxz * _BumpTiling.xyzw + _bumpTransOffset.xyzw; //used for the bumpmap				
				o.floorUV = o.floorUV *_floorPos.xxyy + _floorPos.zzww + o.viewDir.xyzz*_floorDepth;	//A rather messy and wasteful use of an entire field to wrap this texture
				return o;
			}
			
			half4 GetBump(sampler2D bumpTexture, half4 uv) {
				half4 bump = (tex2D(bumpTexture, uv.xy) + tex2D(bumpTexture, uv.zw))-1;
				bump.y *=-1;
				return bump;
			}

			half3 UnpackGetBump(sampler2D bumpTexture, half4 uv) {
				half3 bump = tex2D(bumpTexture, uv.xy) + tex2D(bumpTexture, uv.zw);
				return (bump*2-2)*0.5;
			}	
		
			float4 frag(fragmentInput i) : COLOR {
				float4 color=_Color;
								
				half3 bump = UnpackGetBump(_BumpMap, i.bumpcoord.xyzw);
				//half3 worldNormal = combineNormalMaps(i.vtxNormalWorld.xyz, bump);
          		fixed3 worldNormal = bump*0.5; //(combineNormalMaps(bumpFar, bump));
          		worldNormal.y = 0;
          		//fixed4 reflectVector = reflect(-i.viewDir, worldNormal.xzyw); //worldNormal));
		        fixed4 waterCube = texCUBE(_WaterCube, -i.viewDir-worldNormal); //create and shade our surface
				//half fresnel = 1-(saturate(dot(i.viewDir, worldNormal)));
				
				//waterCube = lerp(tex2D(_MainTex, i.floorUV.xz+worldNormal.xyz*_floorDistort), waterCube, fresnel);	//Include a brutally simple non-paralax approach to the distortion
				
				//color += waterCube*texCUBE(_LightCube, worldNormal);				
				//waterCube += (tex2D(_FoamTex, i.peakUV.xy)+tex2D(_FoamTex, i.peakUV.zw))*i.peakColor;	//Wave peaks. not strictly necessary
				
				//Could do with some sort of "fade to distant color function here
				
				color += waterCube*tex2D(_LightingTex, worldNormal.xz); //Shading pass
				//color = i.peakColor;			
				waterCube=tex2D(_LightingTex, worldNormal.xz);
				return color;
			}
			

			ENDCG
		}
	}
	//Fallback "Water/projectWaterTexBasic"
}