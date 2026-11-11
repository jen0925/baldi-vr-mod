// A splash of BSODA left on a wall in VR (VR/SodaSplash.cs): the spray's own
// picture laid flat on the wall in a lighter shade of its blue, fading away
// over time. Kept in Resources so the build always includes it.
Shader "BaldiVR/SodaSplash" {
	Properties {
		_MainTex ("Picture", 2D) = "white" {}
		_Fade ("Fade", Range(0, 1)) = 1
	}
	SubShader {
		Tags { "Queue"="Transparent-5" "RenderType"="Transparent" "IgnoreProjector"="True" }
		Pass {
			Cull Off
			ZWrite Off
			Offset -2, -2
			Blend SrcAlpha OneMinusSrcAlpha
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			sampler2D _MainTex; float4 _MainTex_ST; float _Fade;
			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
			v2f vert (appdata v) {
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			fixed4 frag (v2f i) : SV_Target {
				fixed4 c = tex2D(_MainTex, i.uv);
				clip(c.a - 0.1);
				// the same picture, a lighter shade of its blue
				c.rgb = lerp(c.rgb, fixed3(1, 1, 1), 0.35);
				return fixed4(c.rgb, c.a * 0.85 * _Fade);
			}
			ENDCG
		}
	}
	Fallback Off
}
