// A hard-edged dark shadow on the floor under characters and items in VR
// (VR/Shadows.cs). Kept in Resources so the build always includes it.
Shader "BaldiVR/HardShadow" {
	Properties {
		_Darkness ("Darkness", Range(0, 1)) = 0.6
	}
	SubShader {
		Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
		Pass {
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha
			Offset -1, -1
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			float _Darkness;
			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
			v2f vert (appdata v) {
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			fixed4 frag (v2f i) : SV_Target {
				// A solid ellipse with a sharp edge: no softening at all.
				float2 d = (i.uv - 0.5) * 2;
				clip(1 - dot(d, d));
				return fixed4(0, 0, 0, _Darkness);
			}
			ENDCG
		}
	}
	Fallback Off
}
