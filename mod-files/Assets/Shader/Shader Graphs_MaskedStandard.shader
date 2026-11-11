Shader "Shader Graphs/MaskedStandard" {
	Properties {
		_TextureColor ("Color", Vector) = (1,1,1,0)
		[NoScaleOffset] _LightMap ("LightMap", 2D) = "white" {}
		_Offset ("Offset", Float) = 0
		_VRSat ("VR Saturation", Float) = 1
		_LightOffset ("LightOffset", Float) = 0.01
		[NoScaleOffset] _Mask ("Mask", 2D) = "white" {}
		[NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
	}
	SubShader {
		Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
		Pass {
			Cull Back
			// Doors, window and fence walls, posters and buttons sit flat on a
			// school wall; this keeps them drawn in front of it without flicker.
			Offset -1, -1
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "BaldiCommon.cginc"
			sampler2D _MainTex;
			float _Offset;
			float _VRSat;
			float4 _TextureColor; sampler2D _Mask;
			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float3 wnormal : TEXCOORD2; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
			v2f vert (appdata v) {
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
				// The material's Offset nudges the surface toward the viewer, so a
				// decal never fights the surface under it.
				float3 shifted = o.wpos + normalize(_WorldSpaceCameraPos - o.wpos) * _Offset;
				o.pos = mul(UNITY_MATRIX_VP, float4(shifted, 1));
				o.uv = v.uv;
				o.wnormal = UnityObjectToWorldNormal(v.normal);
				o.color = v.color;
				return o;
			}
			fixed4 frag (v2f i) : SV_Target {
				// The wall's own picture (the room's wall texture, set by the game),
				// with the mask cutting the hole a door or window sits in.
				fixed4 m = tex2D(_Mask, i.uv);
				clip(m.a - 0.5);
				fixed4 c = tex2D(_MainTex, i.uv);
				return fixed4(BaldiSaturate(c.rgb * _TextureColor.rgb * BaldiLight(i.wpos, i.wnormal), _VRSat), 1);
			}
			ENDCG
		}
	}
	Fallback Off
}
