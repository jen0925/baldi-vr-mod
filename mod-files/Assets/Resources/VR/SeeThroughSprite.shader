// The BSODA spray in VR: its own picture in its own blue, turned to face the
// player like the game's sprites, but see-through (VR/SodaSplash.cs).
// Kept in Resources so the build always includes it.
Shader "BaldiVR/SeeThroughSprite" {
	Properties {
		[NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
		[ToggleUI] _Billboard ("Billboard", Float) = 1
		_Tint ("Tint", Color) = (0.62, 0.85, 1, 1)
		_See ("How solid", Range(0, 1)) = 0.55
	}
	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
		Pass {
			Cull Off
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
			sampler2D _MainTex; float _Billboard; float _BaldiFaceEyes; fixed4 _Tint; float _See;
			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
			v2f vert (appdata v) {
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				float3 centre = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
				float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
				if (_Billboard > 0.5) {
					float sx = length(unity_ObjectToWorld._m00_m10_m20);
					float sy = length(unity_ObjectToWorld._m01_m11_m21);
					float3 right = normalize(float3(UNITY_MATRIX_V[0].x, 0, UNITY_MATRIX_V[0].z));
					float3 toEye = _WorldSpaceCameraPos - centre;
					if (_BaldiFaceEyes > 0.5 && dot(toEye.xz, toEye.xz) > 0.0001) right = normalize(float3(-toEye.z, 0, toEye.x));
					world = centre + right * v.vertex.x * sx + float3(0, 1, 0) * v.vertex.y * sy;
				}
				o.pos = mul(UNITY_MATRIX_VP, float4(world, 1));
				o.uv = v.uv;
				o.color = v.color;
				return o;
			}
			fixed4 frag (v2f i) : SV_Target {
				fixed4 c = tex2D(_MainTex, i.uv) * i.color;
				clip(c.a - 0.1);
				return fixed4(c.rgb, c.a * _See);
			}
			ENDCG
		}
	}
	Fallback Off
}
