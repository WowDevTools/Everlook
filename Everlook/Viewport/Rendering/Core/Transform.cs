//
//  Transform.cs
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

using System.Numerics;

namespace Everlook.Viewport.Rendering.Core
{
    /// <summary>
    /// A structure representing a set of transformation data in world space.
    /// This is mainly used by OpenGL to render objects in different points in the world.
    /// </summary>
    public class Transform
    {
        /// <summary>
        /// Gets or sets the translation of the object in world space. One unit is arbitrary, but
        /// can usually be considered one meter.
        /// </summary>
        public Vector3 Translation
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the orientation of the object.
        /// </summary>
        public Quaternion Orientation
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the scale of the object on three axes. A value of <value>1.0f</value> equates to a
        /// 1:1 correspondence of vertex position to actual position. Increasing or decreasing this value
        /// will increase or decrease the scale of the object on that axis.
        /// </summary>
        public Vector3 Scale
        {
            get;
            set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Transform"/> class. The translation, rotation and scale are
        /// assumed to be {0,0,0], [0,0,0] and [1,1,1], respectively.
        /// </summary>
        public Transform()
            : this(Vector3.Zero, Quaternion.Identity, Vector3.One)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Transform"/> class. This constructor creates the instance from
        /// a single translation vector. The rotation and scale are assumed to be [0,0,0] and [1,1,1], respectively.
        /// </summary>
        /// <param name="translation">The translation in world space.</param>
        public Transform(Vector3 translation)
            : this(translation, Quaternion.Identity, Vector3.One)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Transform"/> class. This constructor creates the instances from
        /// a translation vector, a quaternion and a scale vector.
        /// </summary>
        /// <param name="translation">The translation in world space.</param>
        /// <param name="orientation">The orientation of the transform.</param>
        /// <param name="scale">The scaling factor along the X, Y and Z axes.</param>
        public Transform(Vector3 translation, Quaternion orientation, Vector3 scale)
        {
            this.Translation = translation;
            this.Orientation = orientation;
            this.Scale = scale;
        }

        /// <summary>
        /// Gets the <see cref="Matrix4x4"/> object representing the model matrix of this transform.
        /// </summary>
        /// <returns>A matrix containing model-space transformation data.</returns>
        public Matrix4x4 GetModelMatrix()
        {
            var modelScale = Matrix4x4.CreateScale(this.Scale);
            var modelOrientation = Matrix4x4.CreateFromQuaternion(this.Orientation);
            var modelTranslation = Matrix4x4.CreateTranslation(this.Translation);

            return modelScale * modelOrientation * modelTranslation;
        }
    }
}
