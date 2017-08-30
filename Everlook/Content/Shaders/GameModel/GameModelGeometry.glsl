#version 330 core

#include "Components/SolidWireframe/SolidWireframeGeometry.glsl"

layout(triangles) in;
layout(triangle_strip, max_vertices = 3) out;

in VertexData
{
	vec2 UV1;
    vec2 UV2;
    vec3 Normal;
} vIn[3];

out GeometryVertexDataOut
{
	vec2 UV1;
    vec2 UV2;
    vec3 Normal;
} gvOut;

void main()
{
	ComputeEdgeDistanceData();

	// Pass through the primitive data to the fragment
	for (int i = 0; i < 3; ++i)
	{
		SetWireframePrimitiveData(i);

		gvOut.UV1 = vIn[i].UV1;
        gvOut.UV2 = vIn[i].UV2;
        gvOut.Normal = vIn[i].Normal;

        EmitVertex();
	}

    EndPrimitive();
}
