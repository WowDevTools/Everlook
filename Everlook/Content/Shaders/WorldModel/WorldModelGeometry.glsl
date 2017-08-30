#version 330 core

#include "Components/SolidWireframe/SolidWireframeGeometry.glsl"

layout(triangles) in;
layout(triangle_strip, max_vertices = 3) out;

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
		SetWireframePrimitiveData(i);

        gvOut.UV = vIn[i].UV;
        gvOut.Normal = vIn[i].Normal;

        EmitVertex();
	}

    EndPrimitive();
}
