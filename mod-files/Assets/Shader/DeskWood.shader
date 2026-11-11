// The desks' wood (Jen's son's request): the school's own wood shader with a
// little less colour, so the orange wood reads brown, and more contrast.
Shader "BaldiVR/DeskWood" {
	Properties {
		[NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
		[NoScaleOffset] _LightMap ("LightMap", 2D) = "white" {}
		_TextureOffset ("TextureOffset", Vector) = (0,0,0,0)
		_Tiling ("Tiling", Vector) = (1,1,0,0)
		_TextureColor ("Color", Vector) = (1,1,1,0)
		_ColorMultiplier ("ColorMultiplier", Float) = 1
		_Contrast ("Contrast (1 = as the game made it)", Float) = 1
		_Saturation ("Colour (1 = as the game made it, 0 = grey)", Float) = 1
		_Whiten ("Towards white (0 = as the game made it)", Float) = 0
		_Offset ("Offset", Float) = 0
		_LightingOffset ("LightingOffset", Float) = 0.01
		_VRSat ("VR Saturation", Float) = 1
		_VRCull ("Cull (0 = both sides, for fences)", Float) = 2
	}
	SubShader {
		Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
		Pass {
			Cull [_VRCull]
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
			float4 _TextureOffset; float4 _Tiling; float4 _TextureColor; float _ColorMultiplier; float _VRSat; float _Contrast; float _Saturation; float _Whiten;
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
				fixed4 c = tex2D(_MainTex, i.uv * _Tiling.xy + _TextureOffset.xy);
				clip(c.a - 0.5);
				c.rgb = saturate((c.rgb - 0.5) * _Contrast + 0.5);
				c.rgb = lerp(dot(c.rgb, fixed3(0.299, 0.587, 0.114)).xxx, c.rgb, _Saturation);
				c.rgb = lerp(c.rgb, 1, _Whiten);
				c.rgb *= _TextureColor.rgb * _ColorMultiplier * BaldiLight(i.wpos, i.wnormal);
				return fixed4(BaldiSaturate(c.rgb, _VRSat), 1);
			}
			ENDCG
		}
	}
	Fallback Off
}
