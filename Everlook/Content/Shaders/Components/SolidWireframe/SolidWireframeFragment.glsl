//
//  SolidWireframeFragment.glsl
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

#ifndef SolidWireframeFragment_I
#define SolidWireframeFragment_I

#include "Mathemathics/LineMath.glsl"

/// <summary>
/// Input interface block for data coming in from the geometry shader.
/// </summary>
in GeometryOut
{
	/// <summary>
	/// Gets whether or not this is the simple case where all three vertices are visible.
	/// </summary>
	bool IsSimpleWireframeCase;

	/// <summary>
    /// Gets whether or not a single vertex is visible.
    /// </summary>
    bool IsSingleVertexVisible;

    /// <summary>
    /// Gets the interpolated distances to the three polygon edges from this fragment.
    /// </summary>
    noperspective vec3 EdgeDistances;

	/// <summary>
	/// Gets the screen-space position of the "A" vertex.
	/// </summary>
    flat vec2 A;

    /// <summary>
    /// Gets the screen-space direction of the vector which forms a line along with the "A" vertex.
    /// </summary>
    flat vec2 ADir;

    /// <summary>
    /// Gets the screen-space position of the "B" vertex.
    /// </summary>
    flat vec2 B;

    /// <summary>
    /// Gets the screen-space direction of the vector which forms a line along with the "B" vertex.
    /// </summary>
    flat vec2 BDir;

    /// <summary>
    /// Gets the screen-space direction of the vector which forms a line between the "A" and "B" vertices.
    /// </summary>
    flat vec2 ABDir;
} gIn;

/// <summary>
/// Gets a uniform value indicating whether the wireframe is enabled.
/// </summary>
uniform bool IsWireframeEnabled;

/// <summary>
/// Gets the uniform colour of the wireframe lines.
/// </summary>
uniform vec4 WireframeColour;

/// <summary>
/// Gets the uniform line width of the wireframe lines.
/// </summary>
uniform int WireframeLineWidth;

/// <summary>
/// Gets the uniform fade width of the wireframe lines.
/// </summary>
uniform int WireframeFadeWidth;

/// <summary>
/// Calculates a smooth step multiplier from an edge, based on the distance from the edge.
/// </summary>
/// <param name="distance">The distance from the edge.</param>
/// <returns>A smooth step multiplier.</returns>
float SmoothStepEdge(float distance)
{
	return exp2(-2.0f * pow(distance, 2));
}

/// <summary>
/// Overlays a wireframe onto a base colour. The strength of the wireframe (if it should be present at all) is calculated
/// using the fragment's distance from its closest polygon edge.
/// </summary>
/// <param name="baseColour">The base colour to overlay onto.</param>
/// <param name="discardThreshold">The normal discard threshold of the fragment.</param>
/// <returns>The final colour when overlayed with the wireframe.</returns>
vec4 OverlayWireframe(vec4 baseColour, float discardThreshold)
{
	float distance = 0.0f;
    if (gIn.IsSimpleWireframeCase)
    {
        distance = min(min(gIn.EdgeDistances.x, gIn.EdgeDistances.y), gIn.EdgeDistances.z);
    }
    else
    {
        if (gIn.IsSingleVertexVisible)
        {
            float dist1 = DistanceToLine(gl_FragCoord.xy, gIn.A, gIn.ADir);
            float dist2 = DistanceToLine(gl_FragCoord.xy, gIn.B, gIn.BDir);

            distance = min(dist1, dist2);
        }
        else
        {
            float dist1 = DistanceToLine(gl_FragCoord.xy, gIn.A, gIn.ADir);
            float dist2 = DistanceToLine(gl_FragCoord.xy, gIn.B, gIn.BDir);
            float dist3 = DistanceToLine(gl_FragCoord.xy, gIn.A, gIn.ABDir);

            distance = min(min(dist1, dist2), dist3);
        }
    }

    if (distance < (WireframeLineWidth - WireframeFadeWidth))
    {
        return WireframeColour;
    }
    else if (distance < WireframeLineWidth)
    {
        float relativeDistance = distance - (WireframeLineWidth - WireframeFadeWidth);

        // If the pixel should ordinarily be discarded, fade with a fully transparent pixel instead
        if (baseColour.a < discardThreshold)
        {
            baseColour = vec4(0, 0, 0, 0);
        }

        return mix(baseColour, WireframeColour, SmoothStepEdge(relativeDistance));
    }

	// Fallback: return unmodified
    return baseColour;
}

#endif