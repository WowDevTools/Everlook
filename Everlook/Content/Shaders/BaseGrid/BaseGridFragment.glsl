#version 330 core

out vec4 color;

uniform vec4 lineColour;

void main()
{
	color = lineColour;
}