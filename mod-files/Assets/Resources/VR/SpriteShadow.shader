// The shadow of a character or item in VR (VR/Shadows.cs): the thing's own
// picture laid flat on the floor, stretched away from the light, with a sharp
// edge, a little lighter towards its far end as real shadows are. Kept in
// Resources so the build always includes it.
Shader "BaldiVR/SpriteShadow" {
	Properties {
		_MainTex ("Picture", 2D) = "white" {}
		_Darkness ("Darkness", Range(0, 1)) = 0.6
	}
	SubShader {
		Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
		Pass {
			ZWrite Off
			Cull Off
			Blend SrcAlpha OneMinusSrcAlpha
			Offset -1, -1
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			sampler2D _MainTex;
			float4 _MainTex_ST;
			float _Darkness;
			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float fade : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
			v2f vert (appdata v) {
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.fade = v.uv.y;
				return o;
			}
			fixed4 frag (v2f i) : SV_Target {
				// The picture's outline, sharp: in or out, no blur.
				clip(tex2D(_MainTex, i.uv).a - 0.5);
				return fixed4(0, 0, 0, _Darkness * lerp(1, 0.55, i.fade));
			}
			ENDCG
		}
	}
	Fallback Off
}
