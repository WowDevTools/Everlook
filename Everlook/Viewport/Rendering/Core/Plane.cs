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

using System;
using OpenTK;

namespace Everlook.Viewport.Rendering.Core
{
	public struct Plane
	{
		public Vector3 Normal;
		public Vector3 PointOnPlane;

		public Plane(Vector3 inNormal, Vector3 inPointOnPlane)
		{
			this.Normal = inNormal;
			this.PointOnPlane = inPointOnPlane;
		}

		public float Distance(Vector3 point)
		{
			return Vector3.Dot(this.Normal, point - this.PointOnPlane);
		}
	}
}