// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Water/CubeLit" {
	Properties {
		_WaterCube ("Water Cube Map", Cube) = "" {}
	}
	SubShader {
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			
			samplerCUBE _WaterCube;

			struct v2f {
				float4 pos : SV_POSITION;
				fixed3 norm : TEXCOORD0;
			};

			v2f vert (appdata_base v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos (v.vertex);
				o.norm = v.normal;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target { 
				return texCUBE(_WaterCube, normalize(i.norm));
			}
			ENDCG
		}
	} 
}