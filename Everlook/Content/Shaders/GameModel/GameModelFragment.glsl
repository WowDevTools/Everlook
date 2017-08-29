#version 330 core

in GeometryOut
{
	bool IsSimpleWireframeCase;
    bool IsSingleVertexVisible;
    noperspective vec3 EdgeDistances;

    flat vec2 A;
    flat vec2 ADir;
    flat vec2 B;
    flat vec2 BDir;
    flat vec2 ABDir;

    vec2 UV1;
    vec2 UV2;
    vec3 Normal;
} gIn;

#include "Components/SolidWireframe/SolidWireframe.glsl"

layout(location = 0) out vec4 FragColour;

/*
	Shader path values
*/

const int Opaque = 0;
const int Mod = 1;
const int Opaque_Mod = 2;
const int Opaque_Mod2x = 3;
const int Opaque_Mod2xNA = 4;
const int Opaque_Opaque = 5;
const int Mod_Mod = 6;
const int Mod_Mod2x = 7;
const int Mod_Add = 8;
const int Mod_Mod2xNA = 9;
const int Mod_AddNA = 10;
const int Mod_Opaque = 11;
const int Opaque_Mod2xNA_Alpha = 12;
const int Opaque_AddAlpha = 13;
const int Opaque_AddAlpha_Alpha = 14;
const int Opaque_Mod2xNA_Alpha_Add = 15;
const int Mod_AddAlpha = 16;
const int Mod_AddAlpha_Alpha = 17;
const int Opaque_Alpha_Alpha = 18;
const int Opaque_Mod2xNA_Alpha_3s = 19;
const int Opaque_AddAlpha_Wgt = 20;
const int Mod_Add_Alpha = 21;
const int Opaque_ModNA_Alpha = 22;
const int Mod_AddAlpha_Wgt = 23;
const int Opaque_Mod_Add_Wgt = 24;
const int Opaque_Mod2xNA_Alpha_UnshAlpha = 25;
const int Mod_Dual_Crossfade = 26;
const int Opaque_Mod2xNA_Alpha_Alpha = 27;
const int Mod_Masked_Dual_Crossfade = 28;
const int Opaque_Alpha = 29;
const int Guild = 30;
const int Guild_NoBorder = 31;
const int Guild_Opaque = 32;
const int Mod_Depth = 33;
const int Illum = 34;

// The shading path to use. Maps to MDXFragmentShader and the above values
uniform int ShaderPath;

/*
	Base shader inputs
*/

uniform float alphaThreshold;

uniform sampler2D Diffuse0;
uniform sampler2D Specular0;
uniform sampler2D Diffuse1;
uniform sampler2D Specular1;
uniform sampler2D EnvMap;

uniform vec4 colour;
uniform vec4 colour1;
uniform vec4 baseDiffuseColour;

void main()
{
	vec4 texCol = texture(Diffuse0, gIn.UV1);

	if (texCol.a < alphaThreshold)
    {
        discard;
    }

    if (IsWireframeEnabled)
    {
		FragColour = OverlayWireframe(texCol, alphaThreshold);
		return;
    }

    FragColour = texCol;
}
