Shader "Masked/Mask" {
	SubShader {
		Tags { "Queue"="Geometry-10" }
		Pass { ColorMask 0 ZWrite Off }
	}
}
