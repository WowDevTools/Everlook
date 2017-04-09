#version 330 core

layout(location = 0) in vec3 vertexPosition;
layout(location = 3) in vec3 vertexNormal;
layout(location = 4) in vec2 vertexUV1;
layout(location = 5) in vec2 vertexUV2;

uniform mat4 ModelViewProjection;

out vec2 UV1;
out vec2 UV2;
out vec3 Normal;

void main()
{
	gl_Position = ModelViewProjection * vec4(vertexPosition.xyz, 1);

	UV1 = vertexUV1;
	UV2 = vertexUV2;
	Normal = vertexNormal;
}