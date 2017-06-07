#version 330 core

in vec2 UV1;
in vec2 UV2;
in vec3 Normal;

out vec3 color;

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
	//color = texture(texture0, UV);
	color = vec3(0.18, 0.204, 0.212);
}