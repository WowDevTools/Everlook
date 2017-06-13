//
//  DirectionalLight.cs
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
using OpenTK.Graphics;

namespace Everlook.Viewport.Rendering.Core.Lights
{
	public class DirectionalLight
	{
		public float HorizontalAngle { get; set; }
		public float VerticalAngle { get; set; }

		public Vector3 LightVector => new Vector3
		(
			(float)Math.Cos(this.HorizontalAngle) * (float)Math.Cos(this.VerticalAngle),
			(float)Math.Sin(this.VerticalAngle),
			(float)Math.Sin(this.HorizontalAngle) * (float)Math.Cos(this.VerticalAngle)
		).Normalized();

		public Color4 LightColour { get; set; }
		public float Intensity { get; set; }
	}
}