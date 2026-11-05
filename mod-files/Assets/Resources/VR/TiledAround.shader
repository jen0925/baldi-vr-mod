// The loading screen's Baldi heads all the way round the player in VR
// (VR/MenuSurround.cs): on the inside of a cylinder with a smooth dome above
// and below. Round the side the heads are the game's own pattern; up each
// dome they go in rows, each row with as many heads as fit its width (fewer
// near the top), so none is ever squashed or on top of another, and one
// head sits right at the top and one right at the bottom.
// Kept in Resources so the build always includes it.
Shader "BaldiVR/TiledAround" {
	Properties {
		_MainTex ("Picture", 2D) = "white" {}
		_Rect ("Picture's place on the texture (x, y, w, h)", Vector) = (0, 0, 1, 1)
		_Profile ("Dome's curve, side height, distance round, length end to end", Vector) = (1056, 1260, 4224, 3372)
		_Offset ("Where the game has moved the faces (pixels)", Vector) = (0, 0, 0, 0)
		_Fill ("White between the heads (the start-up fade)", Float) = 0
		_Step ("How far the game's pixel fade has got (0 to 16, -1 none)", Float) = -1
		_ZTest ("Depth test", Float) = 4
	}
	SubShader {
		Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
		Pass {
			Cull Off
			ZTest [_ZTest]
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"
			sampler2D _MainTex; float4 _Rect; float4 _Profile; float4 _Offset; float _Fill; float _Step;
			#define FACE 128.0
			#define SPACING 192.0
			#define SIDE_ROW 96.0    // the game's own: rows half a space apart
			#define DOME_ROW 128.0   // up the domes rows never touch
			#define TIP 150.0        // rows stop this far from the top; one head sits there
			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
			// uv: x is the way round (0 to 1), y the distance in pixels from the
			// bottom, up the bottom dome, the side and the top dome.
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
			v2f vert (appdata v) {
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.screen = ComputeScreenPos(o.pos);
				return o;
			}
			// How far from the nearer end (top or bottom), and the distance round there.
			float from_end (float t) { return min(t, _Profile.w - t); }
			float round_at (float t) {
				float bend = _Profile.z / 6.2831853;
				return _Profile.z * sin(min(from_end(t), _Profile.x) / bend);
			}
			static float2 gx, gy;
			fixed4 picture (float2 cell) {
				return tex2Dgrad(_MainTex, _Rect.xy + cell * _Rect.zw, gx, gy);
			}
			// One row of heads, starting this far from the bottom.
			fixed4 row (float way, float t, float here, float start, float shifted, float bob) {
				float wide = round_at(start);
				float count = round(wide / SPACING);
				float up = t - (start + _Offset.y * bob);
				float slot = way * count - _Offset.x / SPACING - 0.5 * shifted;
				float across = (slot - round(slot)) * here / count;
				if (abs(up) >= FACE * 0.5 || abs(across) >= FACE * 0.5) return 0;
				return picture(float2(across, up) / FACE + 0.5);
			}
			fixed4 frag (v2f i) : SV_Target {
				float way = i.uv.x;
				float t = i.uv.y;
				float here = round_at(t);
				float dome = _Profile.x, side = _Profile.y, ends = _Profile.w;
				// How fast the picture changes across the screen, without the jump
				// where the way round goes from 1 back to 0, so the heads stay sharp.
				float2 dWay = float2(ddx(way), ddy(way));
				dWay -= round(dWay);
				gx = float2(dWay.x * here, ddx(t)) / FACE * _Rect.zw;
				gy = float2(dWay.y * here, ddy(t)) / FACE * _Rect.zw;
				fixed4 c = 0;
				// Round the side: the game's pattern, bobbing as the game bobs it.
				float last = floor(side / SIDE_ROW);
				float k0 = round((t - dome) / SIDE_ROW);
				[unroll] for (int a = -2; a <= 2; a++) {
					float k = k0 + a;
					if (c.a < 0.5 && k >= 0 && k <= last) c = row(way, t, here, dome + k * SIDE_ROW, fmod(k, 2), 1);
				}
				// Up the domes, rows that bob less the nearer the top they are.
				float top = dome + last * SIDE_ROW;
				float m0 = round((t - top) / DOME_ROW);
				float n0 = round((dome - t) / DOME_ROW);
				[unroll] for (int b = -2; b <= 2; b++) {
					float m = m0 + b;
					float start = top + m * DOME_ROW;
					if (c.a < 0.5 && m >= 1 && ends - start >= TIP) c = row(way, t, here, start, fmod(last + m, 2), round_at(start) / _Profile.z);
					float n = n0 + b;
					start = dome - n * DOME_ROW;
					if (c.a < 0.5 && n >= 1 && start >= TIP) c = row(way, t, here, start, fmod(n, 2), round_at(start) / _Profile.z);
				}
				// One head right at the top and one right at the bottom.
				float tip = from_end(t);
				if (c.a < 0.5 && tip < FACE * 0.75) {
					float turn = way * 6.2831853;
					float2 flat = tip * float2(sin(turn), cos(turn));
					if (t > ends * 0.5) flat.x = -flat.x;
					if (all(abs(flat) < FACE * 0.5)) c = picture(flat / FACE + 0.5);
				}
				// The game's pixel fade (GlobalCam): big screen pixels, each 4-by-4
				// block cleared one spot at a time in the game's own order.
				if (_Step >= 0) {
					float2 spot = floor(i.screen.xy / i.screen.w * _ScreenParams.xy / 6.0);
					spot = spot - 4.0 * floor(spot / 4.0);
					float at = spot.x + spot.y * 4.0;
					// the game's 16 spots, by when each goes (x + 4y)
					static const float order[16] = { 5, 15, 13, 7, 2, 8, 10, 0, 9, 12, 11, 1, 14, 4, 6, 12 };
					float when = 16;
					[unroll] for (int n = 0; n < 16; n++) if (order[n] == at) when = min(when, n);
					clip(when < _Step ? -1 : 1);
				}
				if (_Fill > 0.5 && c.a < 0.5) return fixed4(1, 1, 1, 1);
				clip(c.a - 0.5);
				return c;
			}
			ENDCG
		}
	}
	Fallback Off
}
