#version 330 core

#include "Components/SolidWireframe/SolidWireframeFragment.glsl"

in GeometryVertexDataOut
{
	vec2 UV;
    vec3 Normal;
} gvIn;

layout(location = 0) out vec4 FragColour;

/*
	Base shader inputs
*/

uniform float alphaThreshold;

uniform sampler2D Diffuse0;
uniform sampler2D Specular0;
uniform sampler2D Diffuse1;
uniform sampler2D Specular1;
uniform sampler2D EnvMap;

uniform vec4 colour0;
uniform vec4 colour1;
uniform vec4 baseDiffuseColour;


void main()
{
	vec4 texCol = texture(Diffuse0, gvIn.UV);

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
