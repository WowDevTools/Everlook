//
//  Transform.cs
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
	/// A structure representing a set of transformation data in world space.
	/// This is mainly used by OpenGL to render objects in different points in the world.
	/// </summary>
	public struct Transform
	{
		/// <summary>
		/// The translation of the object in world space. One unit is arbitrary, but
		/// can usually be considered one meter.
		/// </summary>
		public Vector3 Translation
		{
			get;
			set;
		}

		/// <summary>
		/// The orientation of the object, using a quaternion.
		/// </summary>
		public Quaternion Orientation
		{
			get;
			set;
		}

		/// <summary>
		/// The scale of the object on three axes. A value of <value>1.0f</value> equates to a
		/// 1:1 correspondence of vertex position to actual position. Increasing or decreasing this value
		/// will increase or decrease the scale of the object on that axis.
		/// </summary>
		public Vector3 Scale
		{
			get;
			set;
		}

		/// <summary>
		/// Creates a new <see cref="Transform"/> object from a single translation vector. The rotation and scale
		/// are assumed to be 0,0,0 and 1,1,1, respectively.
		/// </summary>
		public Transform(Vector3 translation)
			: this(translation, Quaternion.FromAxisAngle(Vector3.UnitX, 0.0f), Vector3.One)
		{
		}

		/// <summary>
		/// Creates a new <see cref="Transform"/> object from a translation vector, a quaternion and a scale vector.
		/// </summary>
		public Transform(Vector3 translation, Quaternion orientation, Vector3 scale)
		{
			this.Translation = translation;
			this.Orientation = orientation;
			this.Scale = scale;
		}

		/// <summary>
		/// Gets the <see cref="Matrix4"/> object representing the model matrix of this transform.
		/// </summary>
		public Matrix4 GetModelMatrix()
		{
			Matrix4 modelScale = Matrix4.Scale(this.Scale);
			Matrix4 modelOrientation = Matrix4.Rotate(this.Orientation);
			Matrix4 modelTranslation = Matrix4.CreateTranslation(this.Translation);

			return modelScale * modelOrientation * modelTranslation;
		}
	}
}