#version 330 core

layout(location = 0) in vec3 vertexPosition;
layout(location = 1) in uvec4 boneIndexes;
layout(location = 2) in uvec4 boneWeights;
layout(location = 3) in vec3 vertexNormal;
layout(location = 4) in vec2 vertexUV1;
layout(location = 5) in vec2 vertexUV2;

uniform mat4 ModelViewProjection;

out VertexData
{
	vec2 UV1;
	vec2 UV2;
	vec3 Normal;
} vOut;

void main()
{
	gl_Position = ModelViewProjection * vec4(vertexPosition.xyz, 1);

	vOut.UV1 = vertexUV1;
	vOut.UV2 = vertexUV2;
	vOut.Normal = vertexNormal;
}