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

uniform sampler2D Texture0;

uniform vec4 colour0;
uniform vec4 colour1;
uniform vec4 baseDiffuseColour;

void main()
{
	vec4 texCol = texture(Texture0, gvIn.UV);

	if (texCol.a < alphaThreshold)
    {
        discard;
    }

	if (IsWireframeEnabled)
	{
		//FragColour = OverlayWireframe(texCol, alphaThreshold);
		if (gIn.EdgeDistances[0] > 0)
		{
			FragColour = vec4(1);
		}
		else
		{
			FragColour = vec4(0);
		}
        return;
	}

    FragColour = texCol;
}
