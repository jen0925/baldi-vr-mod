// The colour all round the player in the menus, eaten away by the game's own
// pixel fade (GlobalCam's 4-by-4 pattern, step 0 to 16) to show the new
// colour behind it (Jen's son's request): the fade fills the whole view, not
// only the flat screen.
Shader "VR/DitherAround"
{
	Properties
	{
		_Color ("Colour", Color) = (0, 0, 0, 1)
		_Step ("How far the game's pixel fade has got (0 to 16)", Float) = 0
	}
	SubShader
	{
		Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
		Cull Off
		ZWrite On
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			fixed4 _Color; float _Step;

			struct v2f { float4 pos : SV_POSITION; float4 screen : TEXCOORD0; };

			v2f vert (float4 vertex : POSITION)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(vertex);
				o.screen = ComputeScreenPos(o.pos);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				// big screen pixels, each 4-by-4 block cleared one spot at a time
				// in the game's own order (the same as TiledAround's)
				float2 spot = floor(i.screen.xy / i.screen.w * _ScreenParams.xy / 6.0);
				spot = spot - 4.0 * floor(spot / 4.0);
				float at = spot.x + spot.y * 4.0;
				static const float order[16] = { 5, 15, 13, 7, 2, 8, 10, 0, 9, 12, 11, 1, 14, 4, 6, 12 };
				float when = 16;
				[unroll] for (int n = 0; n < 16; n++) if (order[n] == at) when = min(when, n);
				clip(when < _Step ? -1 : 1);
				return _Color;
			}
			ENDCG
		}
	}
	Fallback Off
}
