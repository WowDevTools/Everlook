#version 330 core

in vec2 UV;
in vec3 Normal;

out vec4 color;

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
	vec4 texCol = texture(Diffuse0, UV);
    if (texCol.a < alphaThreshold)
    {
        discard;
    }

	color = texCol;
}