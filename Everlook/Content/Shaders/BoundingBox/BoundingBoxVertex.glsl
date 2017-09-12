#version 330 core

#include "Components/Instancing/ObjectInstancing.glsl"

layout(location = 0) in vec3 vertexPosition;

uniform mat4 ModelViewProjection;
uniform mat4 ViewMatrix;
uniform mat4 ProjectionMatrix;

void main()
{
	mat4 mvp = ModelViewProjection;
	if (IsInstance)
	{
		mvp = ProjectionMatrix * ViewMatrix * InstanceModelMatrix;
	}

	gl_Position = mvp * vec4(vertexPosition.xyz, 1);
}