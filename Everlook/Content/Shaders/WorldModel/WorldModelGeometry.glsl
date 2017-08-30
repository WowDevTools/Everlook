#version 330 core

layout(triangles) in;
layout(triangle_strip, max_vertices = 3) out;

#include "Components/SolidWireframe/SolidWireframeGeometry.glsl"

in VertexOut
{
	vec2 UV;
	vec3 Normal;
} vIn[3];

out GeometryVertexDataOut
{
	vec2 UV;
    vec3 Normal;
} gvOut;

void main()
{
	ComputeEdgeDistanceData();

	// Pass through the primitive data to the fragment
	for (int i = 0; i < 3; ++i)
	{
		SetWireframeVertexData(i);

		gl_Position = gl_in[i].gl_Position;
        gvOut.UV = vIn[i].UV;
        gvOut.Normal = vIn[i].Normal;

        EmitVertex();
	}

    EndPrimitive();
}
