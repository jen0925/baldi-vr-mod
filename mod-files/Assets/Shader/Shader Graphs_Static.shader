Shader "Shader Graphs/Static" {
	Properties {
		[NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
	}
	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
		Cull Off
		Lighting Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			sampler2D _MainTex;
			float4 _MainTex_ST;

			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
			v2f vert (appdata v) {
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.color = v.color;
				return o;
			}
			fixed4 frag (v2f i) : SV_Target {
				return tex2D(_MainTex, i.uv) * i.color;
			}
			ENDCG
		}
	}
}
