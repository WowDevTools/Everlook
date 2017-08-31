#version 330 core

#include "GameModel/GameModelVertexShaderPaths.glsl"
#include "Mathemathics/EnvironmentMapping.glsl"

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

uniform mat4 ModelViewMatrix;
uniform mat4 ProjectionMatrix;

uniform int VertexShaderPath;

void main()
{
	gl_Position = ModelViewProjection * vec4(vertexPosition.xyz, 1);

	switch (VertexShaderPath)
	{
		case Diffuse_T1:
		{
			vOut.UV1 = vertexUV1;
			break;
		}
		case Diffuse_Env:
        {
            vOut.UV1 = SphereMap((inverse(ProjectionMatrix) * gl_Position).xyz, (ModelViewMatrix * vec4(vertexNormal, 1)).xyz);
            break;
        }
        case Diffuse_T1_T2:
        {
            vOut.UV1 = vertexUV1;
            vOut.UV2 = vertexUV2;
            break;
        }
        case Diffuse_T1_Env:
	    {
	        vOut.UV1 = vertexUV1;
	        vOut.UV2 = SphereMap((inverse(ProjectionMatrix) * gl_Position).xyz, (ModelViewMatrix * vec4(vertexNormal, 1)).xyz);
	        break;
	    }
	    case Diffuse_Env_T1:
        {
            vOut.UV1 = SphereMap((inverse(ProjectionMatrix) * gl_Position).xyz, (ModelViewMatrix * vec4(vertexNormal, 1)).xyz);
            vOut.UV2 = vertexUV1;
            break;
        }
        case Diffuse_Env_Env:
        {
            vec2 envMap = SphereMap((inverse(ProjectionMatrix) * gl_Position).xyz, (ModelViewMatrix * vec4(vertexNormal, 1)).xyz);
            vOut.UV1 = envMap;
            vOut.UV2 = envMap;
            break;
        }
        case Diffuse_T1_T1:
	    {
	        vOut.UV1 = vertexUV1;
	        vOut.UV2 = vertexUV1;
	        break;
	    }
	    case Diffuse_T2:
	    {
	        vOut.UV1 = vertexUV2;
	        break;
	    }
	    // TODO: > 2 textures
	    case Diffuse_T1_Env_T1:
	    case Diffuse_T1_T1_T1:
	    case Diffuse_EdgeFade_T1:
	    case Diffuse_T1_Env_T2:
	    case Diffuse_EdgeFade_T1_T2:
	    case Diffuse_T1_T1_T1_T2:
	    case Diffuse_EdgeFade_Env:
	    case Diffuse_T1_T2_T1:
	    default:
	    {
	        vOut.UV1 = vertexUV1;
            vOut.UV2 = vertexUV2;
            break;
	    }
	}

	vOut.Normal = vertexNormal;
}