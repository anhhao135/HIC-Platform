// Based on builtin Internal-DepthNormalsTexture.shader
// Encode Normal() is replaced with custom Output() function

Shader "Hidden/UberReplacement" {
Properties {
	_MainTex ("", 2D) = "white" {}
	_Cutoff ("", Float) = 0.5
	_Color ("", Color) = (1,1,1,1)


	_ObjectColor ("Object Color", Color) = (1,0,0,1)
	_CategoryColor ("Catergory Color", Color) = (0,1,0,1)
	_TagColor("Tag Color", Color) = (0,0,1,1)
	_OutputMode("Output Mode", int) = 0
	_CompressionFactor("Compression Factor", float) = 0.25

}

SubShader {
CGINCLUDE

fixed4 _ObjectColor;
fixed4 _CategoryColor;
fixed4 _TagColor;

float _CompressionFactor = 0.25;

int _OutputMode;

// remap depth: [0 @ eye .. 1 @ far] => [0 @ near .. 1 @ far]
inline float Linear01FromEyeToLinear01FromNear(float depth01)
{
	float near = _ProjectionParams.y;
	float far = _ProjectionParams.z;
	return (depth01 - near/far) * (1 + near/far);
}

float4 Output(float depth01, float3 normal)
{
	/* see ImageSynthesis.cs
	enum ReplacelementModes {
		ObjectId 			= 0,
		CatergoryId			= 1,
		DepthCompressed		= 2,
		DepthMultichannel	= 3,
		Normals				= 4
	};*/

	if (_OutputMode == 0) // ObjectId
	{
		return _ObjectColor;
	}
	else if (_OutputMode == 1) // CatergoryId			
	{
		return _CategoryColor;
	}
	else if (_OutputMode == 2) // DepthCompressed
	{
		//float linearZFromNear = Linear01FromEyeToLinear01FromNear(depth01); 

		float k = 1	; // compression factor
		return pow(depth01, k);
		//return fixed4((1 - linearZFromNear).xxx, 1);
	}
	else if (_OutputMode == 3) // DepthMultichannel
	{
		
		/*
		
		float lowBits = frac(depth01 * 256);
		float highBits = depth01 - lowBits / 256;
		return float4(lowBits, highBits, depth01, 1);
		//return float4(depth01, depth01, depth01, 1);
		*/

		

		//float trueDepth = _ProjectionParams.z * depth01;

		/*
		float R = frac(trueDepth) * 256;
		float G = trueDepth / 256 - frac(trueDepth);
		float B = trueDepth / 256 - R / (256 * 256) - G / 256;

		R = R / 255;
		G = G / 255;
		B = B / 255;

		return float4(R,G,B,1);

		*/

		//return float4(trueDepth / 255, 0, 0, 1);

		return float4(depth01, depth01, depth01, 1);
	}
	else if (_OutputMode == 4) // Normals
	{
		// [-1 .. 1] => [0 .. 1]
		float3 c = normal * 0.5 + 0.5;
		return float4(c, 1);
	}
	else if (_OutputMode == 5) // TagId
	{
		return _TagColor;
	}

	// unsupported _OutputMode
	return fixed4(1, 0.7, 0.7, 1);
}
ENDCG

// Support for different RenderTypes
// The following code is based on builtin Internal-DepthNormalsTexture.shader

	Tags { "RenderType"="Opaque" }
	Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct v2f {
	float4 pos : SV_POSITION;
	float4 nz : TEXCOORD0;
	UNITY_VERTEX_OUTPUT_STEREO
};
v2f vert( appdata_base v ) {
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	o.pos = UnityObjectToClipPos(v.vertex);
	o.nz.xyz = COMPUTE_VIEW_NORMAL;
	o.nz.w = COMPUTE_DEPTH_01;
	return o;
}
fixed4 frag(v2f i) : SV_Target{
	return Output (i.nz.w, i.nz.xyz);
	
}
ENDCG
	}
}



SubShader {

Tags{ "RenderType" = "Transparent" }
Pass{
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct v2f {
	float4 pos : SV_POSITION;
	float4 nz : TEXCOORD0;
	UNITY_VERTEX_OUTPUT_STEREO
};
v2f vert(appdata_base v) {
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	o.pos = UnityObjectToClipPos(v.vertex);
	o.nz.xyz = COMPUTE_VIEW_NORMAL;
	o.nz.w = COMPUTE_DEPTH_01;
	return o;
}
fixed4 frag(v2f i) : SV_Target{
	return Output(i.nz.w, i.nz.xyz);

}
ENDCG
}
}


SubShader {
	Tags { "RenderType"="TransparentCutout" }
	Pass {
	Cull Off
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct v2f {
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
	float4 nz : TEXCOORD1;
	UNITY_VERTEX_OUTPUT_STEREO
};
uniform float4 _MainTex_ST;
v2f vert( appdata_base v ) {
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	o.pos = UnityObjectToClipPos(v.vertex);
	o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
	o.nz.xyz = COMPUTE_VIEW_NORMAL;
	o.nz.w = COMPUTE_DEPTH_01;
	return o;
}
uniform sampler2D _MainTex;
uniform fixed _Cutoff;
uniform fixed4 _Color;
fixed4 frag(v2f i) : SV_Target {
	fixed4 texcol = tex2D( _MainTex, i.uv );
	clip( texcol.a*_Color.a - _Cutoff);
	return Output (i.nz.w, i.nz.xyz);
	//return fixed4(0.8, 0.8, 0.2, 1);
}
ENDCG
	}
}

SubShader {
	Tags { "RenderType"="TreeBark" }
	Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "UnityBuiltin3xTreeLibrary.cginc"
struct v2f {
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
	float4 nz : TEXCOORD1;
	UNITY_VERTEX_OUTPUT_STEREO
};
v2f vert( appdata_full v ) {
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	TreeVertBark(v);
	
	o.pos = UnityObjectToClipPos(v.vertex);
	o.uv = v.texcoord.xy;
	o.nz.xyz = COMPUTE_VIEW_NORMAL;
	o.nz.w = COMPUTE_DEPTH_01;
	return o;
}
fixed4 frag( v2f i ) : SV_Target {
	//return Output (i.nz.w, i.nz.xyz);
	return fixed4(0.5, 0.2, 0.8, 1);
}
ENDCG
	}
}

SubShader {
	Tags { "RenderType"="TreeLeaf" }
	Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "UnityBuiltin3xTreeLibrary.cginc"
struct v2f {
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
	float4 nz : TEXCOORD1;
	UNITY_VERTEX_OUTPUT_STEREO
};
v2f vert( appdata_full v ) {
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	TreeVertLeaf(v);
	
	o.pos = UnityObjectToClipPos(v.vertex);
	o.uv = v.texcoord.xy;
	o.nz.xyz = COMPUTE_VIEW_NORMAL;
	o.nz.w = COMPUTE_DEPTH_01;
	return o;
}
uniform sampler2D _MainTex;
uniform fixed _Cutoff;
fixed4 frag( v2f i ) : SV_Target {
	half alpha = tex2D(_MainTex, i.uv).a;

	clip (alpha - _Cutoff);
	//return Output (i.nz.w, i.nz.xyz);
	return fixed4(0.2, 0.2, 0.8, 1);

	
}
ENDCG
	}
}

SubShader {
	Tags { "RenderType"="TreeOpaque" "DisableBatching"="True" }
	Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
#include "TerrainEngine.cginc"
struct v2f {
	float4 pos : SV_POSITION;
	float4 nz : TEXCOORD0;
	UNITY_VERTEX_OUTPUT_STEREO
};
struct appdata {
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	fixed4 color : COLOR;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
v2f vert( appdata v ) {
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	TerrainAnimateTree(v.vertex, v.color.w);
	o.pos = UnityObjectToClipPos(v.vertex);
	o.nz.xyz = COMPUTE_VIEW_NORMAL;
	o.nz.w = COMPUTE_DEPTH_01;
	return o;
}
fixed4 frag(v2f i) : SV_Target {
	//return Output (i.nz.w, i.nz.xyz);
    return fixed4(0.2, 0.2, 0.8, 1);
}
ENDCG
	}
} 

SubShader {
	Tags { "RenderType"="TreeTransparentCutout" "DisableBatching"="True" }
	Pass {
		Cull Back
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
#include "TerrainEngine.cginc"

struct v2f {
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
	float4 nz : TEXCOORD1;
	UNITY_VERTEX_OUTPUT_STEREO
};
struct appdata {
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	fixed4 color : COLOR;
	float4 texcoord : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
v2f vert( appdata v ) {
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	TerrainAnimateTree(v.vertex, v.color.w);
	o.pos = UnityObjectToClipPos(v.vertex);
	o.uv = v.texcoord.xy;
	o.nz.xyz = COMPUTE_VIEW_NORMAL;
	o.nz.w = COMPUTE_DEPTH_01;
	return o;
}
uniform sampler2D _MainTex;
uniform fixed _Cutoff;
fixed4 frag(v2f i) : SV_Target {
	half alpha = tex2D(_MainTex, i.uv).a;

	clip (alpha - _Cutoff);
	//return Output (i.nz.w, i.nz.xyz);
	return fixed4(0.8, 0.8, 0.2, 1);
}
ENDCG
	}
	Pass {
		Cull Front
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
#include "TerrainEngine.cginc"

struct v2f {
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
	float4 nz : TEXCOORD1;
	UNITY_VERTEX_OUTPUT_STEREO
};
struct appdata {
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	fixed4 color : COLOR;
	float4 texcoord : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
v2f vert( appdata v ) {
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	TerrainAnimateTree(v.vertex, v.color.w);
	o.pos = UnityObjectToClipPos(v.vertex);
	o.uv = v.texcoord.xy;
	o.nz.xyz = -COMPUTE_VIEW_NORMAL;
	o.nz.w = COMPUTE_DEPTH_01;
	return o;
}
uniform sampler2D _MainTex;
uniform fixed _Cutoff;
fixed4 frag(v2f i) : SV_Target {
	fixed4 texcol = tex2D( _MainTex, i.uv );
	clip( texcol.a - _Cutoff );
	return Output (i.nz.w, i.nz.xyz);
}
ENDCG
	}

}

SubShader {
	Tags { "RenderType"="TreeBillboard" }
	Pass {
		Cull Off
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
#include "TerrainEngine.cginc"
struct v2f {
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
	float4 nz : TEXCOORD1;
	UNITY_VERTEX_OUTPUT_STEREO
};
v2f vert (appdata_tree_billboard v) {
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	TerrainBillboardTree(v.vertex, v.texcoord1.xy, v.texcoord.y);
	o.pos = UnityObjectToClipPos(v.vertex);
	o.uv.x = v.texcoord.x;
	o.uv.y = v.texcoord.y > 0;
	o.nz.xyz = float3(0,0,1);
	o.nz.w = COMPUTE_DEPTH_01;
	return o;
}
uniform sampler2D _MainTex;
fixed4 frag(v2f i) : SV_Target {
	fixed4 texcol = tex2D( _MainTex, i.uv );
	clip( texcol.a - 0.001 );
	//return Output (i.nz.w, i.nz.xyz);
	return fixed4(0.2, 0.2, 0.8, 1);
}
ENDCG
	}
}

SubShader {
	Tags { "RenderType"="GrassBillboard" }
	Pass {
		Cull Off		
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
#include "TerrainEngine.cginc"

struct v2f {
	float4 pos : SV_POSITION;
	fixed4 color : COLOR;
	float2 uv : TEXCOORD0;
	float4 nz : TEXCOORD1;
	UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert (appdata_full v) {
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	WavingGrassBillboardVert (v);
	o.color = v.color;
	o.pos = UnityObjectToClipPos(v.vertex);
	o.uv = v.texcoord.xy;
	o.nz.xyz = COMPUTE_VIEW_NORMAL;
	o.nz.w = COMPUTE_DEPTH_01;
	return o;
}
uniform sampler2D _MainTex;
uniform fixed _Cutoff;
fixed4 frag(v2f i) : SV_Target {
	fixed4 texcol = tex2D( _MainTex, i.uv );
	fixed alpha = texcol.a * i.color.a;
	clip( alpha - _Cutoff );
	return Output (i.nz.w, i.nz.xyz);
	//return fixed4(0.2, 0.2, 0.8, 1);
}
ENDCG
	}
}

SubShader {
	Tags { "RenderType"="Grass" }
	Pass {
		Cull Off
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
#include "TerrainEngine.cginc"
struct v2f {
	float4 pos : SV_POSITION;
	fixed4 color : COLOR;
	float2 uv : TEXCOORD0;
	float4 nz : TEXCOORD1;
	UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert (appdata_full v) {
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	WavingGrassVert (v);
	o.color = v.color;
	o.pos = UnityObjectToClipPos(v.vertex);
	o.uv = v.texcoord;
	o.nz.xyz = COMPUTE_VIEW_NORMAL;
	o.nz.w = COMPUTE_DEPTH_01;
	return o;
}
uniform sampler2D _MainTex;
uniform fixed _Cutoff;
fixed4 frag(v2f i) : SV_Target {
	fixed4 texcol = tex2D( _MainTex, i.uv );
	fixed alpha = texcol.a * i.color.a;
	clip( alpha - _Cutoff );
	return Output (i.nz.w, i.nz.xyz);
	//return fixed4(0.2, 0.2, 0.8, 1);
}
ENDCG
	}
}




SubShader{
	Tags{
		"RenderType" = "PedestrianCutout"
		"Queue" = "Transparent"
	}

	Blend SrcAlpha OneMinusSrcAlpha

	ZWrite off
	Cull off

	Pass{

		CGPROGRAM

		#include "UnityCG.cginc"

		#pragma vertex vert
		#pragma fragment frag

		sampler2D _MainTex;
		float4 _MainTex_ST;

		fixed4 _Color;

		struct appdata {
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
			fixed4 color : COLOR;
		};

		struct v2f {
			float4 position : SV_POSITION;
			float2 uv : TEXCOORD0;
			fixed4 color : COLOR;
		};

		v2f vert(appdata v) {
			v2f o;
			o.position = UnityObjectToClipPos(v.vertex);
			o.uv = TRANSFORM_TEX(v.uv, _MainTex);
			o.color = v.color;
			return o;
		}

		fixed4 frag(v2f i) : SV_TARGET{
			fixed4 col = tex2D(_MainTex, i.uv);
			//col *= _Color;
			//col *= i.color;

			//col.rgb = (1, 0, 0);

			col.rgb = _ObjectColor.rgb;
			//col.rgb = (1, 1, 1);
			return col;
		}

		ENDCG
	}
}









Fallback Off
}
