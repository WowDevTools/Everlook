//
//  EnvironmentMapping.glsl
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

/// <summary>
/// Calculates spherical environment mapping coordinates.
/// </summary>
/// <param name="vertex">The position of the vertex in view space.</param>
/// <param name="normal">The normal of the vertex in view space.</param>
vec2 SphereMap(vec3 vertex, vec3 normal)
{
	vec3 normalPos = -(normalize(vertex.xyz));
	vec3 temp = (normalPos - (normal * (2.0 * dot(normalPos, normal))));
	temp = vec3(temp.x, temp.y, temp.z + 1);

	return ((normalize(temp).xy * 0.5) + vec2(0.5));
}