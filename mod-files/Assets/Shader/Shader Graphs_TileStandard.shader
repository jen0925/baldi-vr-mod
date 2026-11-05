Shader "Shader Graphs/TileStandard" {
	Properties {
		[NoScaleOffset] _MainTex ("MainTexture", 2D) = "white" {}
		_Offset ("Offset", Float) = 0
		[NoScaleOffset] _LightMap ("LightMap", 2D) = "white" {}
	}
	SubShader {
		Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
		Pass {
			Cull Back
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "BaldiCommon.cginc"
			sampler2D _MainTex;

			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float3 wnormal : TEXCOORD2; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
			v2f vert (appdata v) {
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.wnormal = UnityObjectToWorldNormal(v.normal);
				o.color = v.color;
				return o;
			}
			fixed4 frag (v2f i) : SV_Target {
				fixed4 c = tex2D(_MainTex, i.uv);
				clip(c.a - 0.5);
				return fixed4(c.rgb * BaldiLight(i.wpos, i.wnormal), 1);
			}
			ENDCG
		}
	}
	Fallback Off
}
