#version 330 core

layout(triangles) in;
layout(triangle_strip, max_vertices = 3) out;

in VertexOut
{
	vec2 UV;
	vec3 Normal;
} vIn[3];

out GeometryOut
{
	bool IsSimpleWireframeCase;
	bool IsSingleVertexVisible;
	noperspective vec3 EdgeDistances;

	flat vec2 A;
	flat vec2 ADir;
	flat vec2 B;
	flat vec2 BDir;
	flat vec2 ABDir;

	vec2 UV;
    vec3 Normal;
} gOut;

uniform mat4 ViewportMatrix;
uniform bool IsWireframeEnabled;

struct TriangleProjectionIndices
{
	int A;
	int B;
	int APrim;
	int BPrim;
};

const TriangleProjectionIndices ProjectionLookup[8] = TriangleProjectionIndices[]
(
	TriangleProjectionIndices(-1, -1, -1, -1), // All visible, never accessed
	TriangleProjectionIndices(0, 1, 2, 2), // TTF
	TriangleProjectionIndices(0, 2, 1, 1), // TFT
	TriangleProjectionIndices(0, 0, 1, 2), // TFF

	TriangleProjectionIndices(1, 2, 0, 0), // FTT
	TriangleProjectionIndices(1, 1, 0, 2), // FTF
	TriangleProjectionIndices(2, 2, 0, 1), // FFT
	TriangleProjectionIndices(-1, -1, -1, -1) // None visible, never accessed
);

vec4 ProjectToScreen(vec4 clipSpaceCoordinate)
{
	vec4 NDC = clipSpaceCoordinate / clipSpaceCoordinate.w;
	return ViewportMatrix * NDC;
}

int DetermineCase(float P0z, float P1z, float P2z)
{
	bool P0Visible = P0z > 0;
	bool P1Visible = P1z > 0;
	bool P2Visible = P2z > 0;

	gOut.IsSingleVertexVisible = false;

	if (P0Visible && P1Visible && P2Visible)
	{
		gOut.IsSimpleWireframeCase = true;
		return 0;
	}

	if (P0Visible && P1Visible && !P2Visible)
	{
		return 1;
	}
	if (P0Visible && !P1Visible && P2Visible)
	{
		return 2;
	}
	if (P0Visible && !P1Visible && !P2Visible)
	{
		gOut.IsSingleVertexVisible = true;
		return 3;
	}

	if (!P0Visible && P1Visible && P2Visible)
	{
		return 4;
	}
	if (!P0Visible && P1Visible && !P2Visible)
	{
		gOut.IsSingleVertexVisible = true;
		return 5;
	}
	if (!P0Visible && !P1Visible && P2Visible)
	{
		gOut.IsSingleVertexVisible = true;
		return 6;
	}

	// No vertices are visible and the triangle will be clipped
	return 7;
}

float DistanceToLine(vec2 F, vec2 Q, vec2 QDir)
{
	return sqrt(dot((Q - F), (Q - F)) - dot(QDir, (Q - F)));
}

vec2 ComputeLineDirection(int Q, int QPrim, vec2 P[3])
{
	return normalize
	(
		P[Q] -
		ProjectToScreen
		(
			gl_in[Q].gl_Position + (gl_in[QPrim].gl_Position - gl_in[Q].gl_Position)
		).xy
	);
}

vec3 ComputeVertexHeights(vec2 P[3])
{
	return vec3
    (
        DistanceToLine(P[2], P[0], ComputeLineDirection(0, 1, P)),
        DistanceToLine(P[0], P[2], ComputeLineDirection(2, 1, P)),
        DistanceToLine(P[1], P[0], ComputeLineDirection(0, 2, P))
    );
}

void SetComplexCasePoints(int complexCase, vec2 P[3])
{
	gOut.IsSimpleWireframeCase = false;
	TriangleProjectionIndices projectionIndices = ProjectionLookup[complexCase];

	gOut.A = P[projectionIndices.A];
	gOut.B = P[projectionIndices.B];

	gOut.ADir = ComputeLineDirection(projectionIndices.A, projectionIndices.APrim, P);
	gOut.BDir = ComputeLineDirection(projectionIndices.B, projectionIndices.BPrim, P);

	if (!gOut.IsSingleVertexVisible)
	{
		gOut.ABDir = ComputeLineDirection(projectionIndices.A, projectionIndices.B, P);
	}
}

void main()
{
	vec3 EdgeDistances = vec3(0);
	if (IsWireframeEnabled)
	{
		// Step 1: Project to screen space
        vec2 P[3];
        for (int i = 0; i < 3; ++i)
        {
            P[i] = ProjectToScreen(gl_in[i].gl_Position).xy;
        }

		// Step 2: Determine triangle case
		int projectionCase = DetermineCase
		(
			ProjectToScreen(gl_in[0].gl_Position).z,
			ProjectToScreen(gl_in[1].gl_Position).z,
			ProjectToScreen(gl_in[2].gl_Position).z
		);

		if (projectionCase == 0)
		{
			EdgeDistances = ComputeVertexHeights(P);
		}
		else if (projectionCase != 7)
		{
			SetComplexCasePoints(projectionCase, P);
		}
	}

	// Pass through the primitive data to the fragment
	for (int i = 0; i < 3; ++i)
	{
		gl_Position = gl_in[i].gl_Position;

		switch (i)
		{
			case 0:
			{
				gOut.EdgeDistances = vec3(0, EdgeDistances.y, 0);
				break;
			}
			case 1:
			{
				gOut.EdgeDistances = vec3(0, 0, EdgeDistances.z);
				break;
			}
			case 2:
			{
				gOut.EdgeDistances = vec3(EdgeDistances.x, 0, 0);
				break;
			}
		}
        gOut.UV = vIn[i].UV;
        gOut.Normal = vIn[i].Normal;

        EmitVertex();
	}

    EndPrimitive();
}
