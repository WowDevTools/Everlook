#version 330 core

in vec2 UV;
out vec4 color;

uniform sampler2D imageTextureSampler;

void main()
{
	color = texture(imageTextureSampler, UV);
}