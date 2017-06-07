#version 330 core

layout(location = 0) in vec2 vertexPosition;
layout(location = 1) in vec2 vertexUV;

uniform mat4 ModelViewProjection;
out vec2 UV;

void main()
{
	gl_Position = ModelViewProjection * vec4(vertexPosition.x, vertexPosition.y, 0, 1.0);

	UV = vertexUV;
}