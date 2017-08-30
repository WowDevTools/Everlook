#version 330 core

#include "Components/SolidWireframe/SolidWireframeFragment.glsl"
#include "GameModel/GameModelShaderPaths.glsl"

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

uniform sampler2D Diffuse0;
uniform sampler2D Specular0;
uniform sampler2D Diffuse1;
uniform sampler2D Specular1;
uniform sampler2D EnvMap;

uniform vec4 colour;
uniform vec4 baseDiffuseColour;

void main()
{
	vec4 texCol = texture(Diffuse0, gvIn.UV1);

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
