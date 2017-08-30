//
//  SolidWireframeGeometry.glsl
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2017 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

#ifndef SolidWireframeGeometry_I
#define SolidWireframeGeometry_I

#include "Mathemathics/LineMath.glsl"
#include "Mathemathics/SpaceProjection.glsl"
#include "Components/SolidWireframe/TriangleProjectionIndexes.glsl"

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
} gOut;

uniform mat4 ViewportMatrix;
uniform bool IsWireframeEnabled;

const TriangleProjectionIndexes ProjectionLookup[8] = TriangleProjectionIndexes[]
(
	// These indices follow a truth table of the visible vertices. F means not visible, T means visible
	TriangleProjectionIndexes(-1, -1, -1, -1), // All visible, never accessed
	TriangleProjectionIndexes(0, 1, 2, 2), // TTF
	TriangleProjectionIndexes(0, 2, 1, 1), // TFT
	TriangleProjectionIndexes(0, 0, 1, 2), // TFF

	TriangleProjectionIndexes(1, 2, 0, 0), // FTT
	TriangleProjectionIndexes(1, 1, 0, 2), // FTF
	TriangleProjectionIndexes(2, 2, 0, 1), // FFT
	TriangleProjectionIndexes(-1, -1, -1, -1) // None visible, never accessed
);

vec3 EdgeDistances = vec3(0);

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

vec2 ComputeLineDirection(int Q, int QPrim, vec2 P[3])
{
	return normalize
	(
		P[Q] -
		ProjectToScreen
		(
			ViewportMatrix,
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
	TriangleProjectionIndexes projectionIndices = ProjectionLookup[complexCase];

	gOut.A = P[projectionIndices.A];
	gOut.B = P[projectionIndices.B];

	gOut.ADir = ComputeLineDirection(projectionIndices.A, projectionIndices.APrim, P);
	gOut.BDir = ComputeLineDirection(projectionIndices.B, projectionIndices.BPrim, P);

	if (!gOut.IsSingleVertexVisible)
	{
		gOut.ABDir = ComputeLineDirection(projectionIndices.A, projectionIndices.B, P);
	}
}

void ComputeEdgeDistanceData()
{
    if (IsWireframeEnabled)
    {
        // Step 1: Project to screen space
        vec2 P[3];
        for (int i = 0; i < 3; ++i)
        {
            P[i] = ProjectToScreen(ViewportMatrix, gl_in[i].gl_Position).xy;
        }

        // Step 2: Determine triangle case
        int projectionCase = DetermineCase
        (
            ProjectToScreen(ViewportMatrix, gl_in[0].gl_Position).z,
            ProjectToScreen(ViewportMatrix, gl_in[1].gl_Position).z,
            ProjectToScreen(ViewportMatrix, gl_in[2].gl_Position).z
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
}

void SetWireframePrimitiveData(int vertexIndex)
{
	gl_Position = gl_in[vertexIndex].gl_Position;

    switch (vertexIndex)
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
}

#endif
