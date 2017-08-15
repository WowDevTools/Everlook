//
//  Plane.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2016 Jarl Gullberg
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

using OpenTK;

namespace Everlook.Viewport.Rendering.Core
{
	/// <summary>
	/// This class represents a plane in the world, stretching infinitely in all directions.
	/// The orientation and shearpoint of the plane are determined by a normal, and a point which
	/// is known to be on the plane. These two values can be arbitrarily chosen as neccesary when
	/// the plane is constructed.
	/// </summary>
	public struct Plane
	{
		/// <summary>
		/// The normal of the plane.
		/// </summary>
		public Vector3 Normal;

		/// <summary>
		/// A point on the plane.
		/// </summary>
		public Vector3 PointOnPlane;

		/// <summary>
		/// Initializes a new instance of the <see cref="Plane"/> struct.
		/// </summary>
		/// <param name="inNormal">The normal of the plane.</param>
		/// <param name="inPointOnPlane">A point on the plane.</param>
		public Plane(Vector3 inNormal, Vector3 inPointOnPlane)
		{
			this.Normal = inNormal;
			this.PointOnPlane = inPointOnPlane;
		}

		/// <summary>
		/// Calculates the distance from the input point to the plane.
		/// </summary>
		/// <param name="point">The point from which to measure the distance.</param>
		/// <returns>The distance between the point and the plane.</returns>
		public float Distance(Vector3 point)
		{
			return Vector3.Dot(this.Normal, point - this.PointOnPlane);
		}
	}
}
