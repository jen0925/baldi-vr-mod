Shader "Shader Graphs/SpriteStandard" {
	Properties {
		[NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
		_Color ("Color", Vector) = (1,1,1,0)
		_ColorMultiplier ("ColorMultiplier", Float) = 1
		[NoScaleOffset] _LightMap ("LightMap", 2D) = "white" {}
		_Offset ("Offset", Float) = 0
		[ToggleUI] _Billboard ("Billboard", Float) = 0
		_VRThick ("VR Thickness", Float) = 1
		_VRLayers ("VR Thickness Layers", Float) = 6
		_VRSat ("VR Saturation", Float) = 1
	}
	SubShader {
		Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
		Pass {
			Name "Side"
			Cull Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma target 3.0
			#include "BaldiCommon.cginc"
			sampler2D _MainTex;
			float4 _Color; float _ColorMultiplier; float _Billboard; float _BaldiFaceEyes; float _VRThick;
			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float2 shift : TEXCOORD2; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
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
				o.wpos = world;
				// Depth (full VR only, set by VRRig): one solid side, made by
				// drawing the cutout once, pushed back and to one side, and filling
				// in everything between it and the front in the colour of the edge
				// it comes from (see frag), so it reads as a thick cut-out rather
				// than copies. Outside VR it collapses away.
				float3 view = mul(UNITY_MATRIX_V, float4(world, 1)).xyz;
				float4 front = mul(UNITY_MATRIX_P, float4(view, 1));
				float3 back = view + float3(0.035, -0.02, -0.05) * 1.39 * _VRThick;   // about 1.1 inches deep
				float4 backClip = mul(UNITY_MATRIX_P, float4(back, 1));
				// Far off, the side is thinner than a pixel, so it is not drawn at all.
				o.pos = _BaldiFaceEyes > 0.5 && length(view) < 50 ? backClip : float4(0, 0, 0, 0);
				// how far, in screen pixels, the front lies from here
				o.shift = (front.xy / front.w - backClip.xy / backClip.w) * 0.5 * _ScreenParams.xy;
				o.uv = v.uv;
				o.color = v.color;
				return o;
			}
			fixed4 frag (v2f i) : SV_Target {
				// From this point of the pushed-back cutout towards the front, the
				// first solid pixel gives the side its colour; nothing there, no side.
				// one look per screen pixel along the way (up to 32), so the side is
				// one continuous drag of the sprite, never separate copies
				float2 whole = ddx(i.uv) * i.shift.x + ddy(i.uv) * i.shift.y;
				// a look every two pixels at most (up to 16), half the work of one
				// per pixel, still one continuous side
				float looks = clamp(ceil(length(i.shift) * 0.5), 1.0, 16.0);
				float2 step = whole / looks;
				fixed4 c = fixed4(0, 0, 0, 0);
				[loop] for (int k = 0; k <= 16; k++) {
					if (k > looks || c.a >= 0.5) break;
					fixed4 s = tex2Dlod(_MainTex, float4(i.uv + step * k, 0, 0));
					if (s.a > 0.5) c = s;
				}
				clip(c.a - 0.5);
				c *= i.color;
				c.rgb *= _Color.rgb * _ColorMultiplier * BaldiLight(i.wpos, float3(0, 0, 0));
				return fixed4(c.rgb * 0.6, 1);
			}
			ENDCG
		}
		Pass {
			Cull Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "BaldiCommon.cginc"
			sampler2D _MainTex;
			float4 _Color; float _ColorMultiplier; float _Billboard; float _BaldiFaceEyes; float _VRThick; float _VRLayers; float _VRSat;
			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float3 wnormal : TEXCOORD2; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
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
					// In VR (set by VRRig) sprites face the player's eyes rather than
					// the way the head is turned, so turning the head never swings
					// them into tables and walls.
					float3 toEye = _WorldSpaceCameraPos - centre;
					if (_BaldiFaceEyes > 0.5 && dot(toEye.xz, toEye.xz) > 0.0001) right = normalize(float3(-toEye.z, 0, toEye.x));
					world = centre + right * v.vertex.x * sx + float3(0, 1, 0) * v.vertex.y * sy;
				}
				o.wpos = world;
				o.pos = mul(UNITY_MATRIX_VP, float4(world, 1));
				o.uv = v.uv;
				o.wnormal = UnityObjectToWorldNormal(v.normal);
				o.color = v.color;
				return o;
			}
			fixed4 frag (v2f i) : SV_Target {
				fixed4 c = tex2D(_MainTex, i.uv) * i.color;
				clip(c.a - 0.5);
				c.rgb *= _Color.rgb * _ColorMultiplier * BaldiLight(i.wpos, float3(0, 0, 0));
				return fixed4(BaldiSaturate(c.rgb, _VRSat), 1);
			}
			ENDCG
		}
	}
	Fallback Off
}
