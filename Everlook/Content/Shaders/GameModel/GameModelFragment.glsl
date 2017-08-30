#version 330 core

#include "Components/SolidWireframe/SolidWireframeFragment.glsl"
#include "GameModel/GameModelShaderPaths.glsl"
#include "Components/Combiners/Combiners.glsl"

in GeometryVertexDataOut
{
	vec2 UV1;
    vec2 UV2;
    vec3 Normal;
} gvIn;

layout(location = 0) out vec4 FragColour;

// The shading path to use. Maps to MDXFragmentShader and what's in GameModelShaderPaths.
uniform int ShaderPath;

/*
	Base shader inputs
*/

uniform float alphaThreshold;

uniform vec4 BaseColour;
uniform sampler2D Diffuse0;
uniform sampler2D Diffuse1;

void main()
{
	vec4 fragOutput = vec4(0);
	vec4 tex0 = texture(Diffuse0, gvIn.UV1);
	vec4 tex1 = texture(Diffuse1, gvIn.UV2);
	switch (ShaderPath)
	{
		case Opaque: fragOutput = CombinersOpaque(BaseColour, tex0); break;
		case Mod: fragOutput = CombinersMod(BaseColour, tex0); break;
		case Opaque_Mod: fragOutput = CombinersOpaqueMod(BaseColour, tex0, tex1); break;
		case Opaque_Mod2x: fragOutput = CombinersOpaqueMod2x(BaseColour, tex0, tex1); break;
		case Opaque_Mod2xNA: fragOutput = CombinersOpaqueMod2xNoAlpha(BaseColour, tex0, tex1); break;
		case Opaque_Opaque: fragOutput = CombinersOpaqueOpaque(BaseColour, tex0, tex1); break;
		case Mod_Mod: fragOutput = CombinersModMod(BaseColour, tex0, tex1); break;
		case Mod_Mod2x: fragOutput = CombinersModMod2x(BaseColour, tex0, tex1); break;
		case Mod_Add: fragOutput = CombinersModAdd(BaseColour, tex0, tex1); break;
		case Mod_Mod2xNA: fragOutput = CombinersModMod2xNoAlpha(BaseColour, tex0, tex1); break;
		case Mod_AddNA: fragOutput = CombinersModAddNoAlpha(BaseColour, tex0, tex1); break;
		case Mod_Opaque: fragOutput = CombinersModOpaque(BaseColour, tex0, tex1); break;
		case Opaque_Mod2xNA_Alpha: fragOutput = CombinersOpaqueMod2xNoAlphaAlpha(BaseColour, tex0, tex1); break;
		case Opaque_AddAlpha: fragOutput = CombinersOpaqueAddAlpha(BaseColour, tex0, tex1); break;
		case Opaque_AddAlpha_Alpha: fragOutput = CombinersOpaqueAddAlphaAlpha(BaseColour, tex0, tex1); break;
		//case Opaque_Mod2xNA_Alpha_Add: fragOutput = CombinersOpaqueMod2xNoAlphaAlphaAdd(BaseColour, tex0, tex1); break;
		default: fragOutput = vec4(1); break;
	}

	if (fragOutput.a < alphaThreshold)
    {
        discard;
    }

    if (IsWireframeEnabled)
    {
		FragColour = OverlayWireframe(fragOutput, alphaThreshold);
		return;
    }

    FragColour = fragOutput;
}
