//
//  ViewportCamera.cs
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

using System;
using System.Diagnostics.CodeAnalysis;
using Everlook.Configuration;
using Everlook.Viewport.Rendering.Core;
using OpenTK;
using OpenTK.Graphics.ES10;

namespace Everlook.Viewport.Camera
{
    /// <summary>
    /// A camera in the world, represented by a position and a set of viewing angles.
    /// </summary>
    public class ViewportCamera
    {
        /*
            Default camera and movement speeds.
        */

        /// <summary>
        /// The default near clipping distance.
        /// </summary>
        private const float DefaultNearClippingDistance = 0.1f;

        /// <summary>
        /// The default far clipping distance.
        /// </summary>
        private const float DefaultFarClippingDistance = 1000.0f;

        private ProjectionType _projectionInternal;

        /// <summary>
        /// Gets or sets the projection type of the camera.
        /// </summary>
        public ProjectionType Projection
        {
            get => this._projectionInternal;
            set
            {
                this._projectionInternal = value;
                RecalculateInternals();
            }
        }

        /// <summary>
        /// Gets or sets the width of the camera viewport in pixels.
        /// </summary>
        public int ViewportWidth { get; set; }

        /// <summary>
        /// Gets or sets the height of the camera viewport in pixels.
        /// </summary>
        public int ViewportHeight { get; set; }

        private Vector3 _positionInternal;

        /// <summary>
        /// Gets or sets the current position of the observer in world space.
        /// </summary>
        public Vector3 Position
        {
            get => this._positionInternal;
            set
            {
                this._positionInternal = value;
                RecalculateInternals();
            }
        }

        /// <summary>
        /// Gets a vector pointing in the direction the observer is currently looking.
        /// </summary>
        public Vector3 LookDirectionVector
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the current vector that points directly to the right, relative to the
        /// current orientation of the observer.
        /// </summary>
        public Vector3 RightVector
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the current vector that points directly upwards, relative to the current
        /// orientation of the observer.
        /// </summary>
        public Vector3 UpVector
        {
            get;
            private set;
        }

        private float _horizontalViewAngleInternal;

        /// <summary>
        /// Gets or sets the current horizontal view angle of the observer.
        /// </summary>
        public float HorizontalViewAngle
        {
            get => this._horizontalViewAngleInternal;
            set
            {
                this._horizontalViewAngleInternal = value;
                RecalculateInternals();
            }
        }

        private float _verticalViewAngleInternal;

        /// <summary>
        /// Gets or sets the current vertical view angle of the observer. This angle is
        /// limited to -90 and 90 (straight down and straight up, respectively).
        /// </summary>
        public float VerticalViewAngle
        {
            get => this._verticalViewAngleInternal;
            set
            {
                this._verticalViewAngleInternal = value;
                RecalculateInternals();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewportCamera"/> class, and sets its position
        /// to the default values.
        /// </summary>
        public ViewportCamera()
        {
            ResetPosition();
        }

        /// <summary>
        /// Resets the camera to the default position.
        /// </summary>
        public void ResetPosition()
        {
            this.Position = new Vector3(0.0f, 0.0f, 1.0f);
        }

        /// <summary>
        /// Resets the camera to its default rotation.
        /// </summary>
        public void ResetRotation()
        {
            this.HorizontalViewAngle = MathHelper.DegreesToRadians(180.0f);
            this.VerticalViewAngle = MathHelper.DegreesToRadians(0.0f);
        }

        private void RecalculateInternals()
        {
            // Recalculate the directional vectors
            this.LookDirectionVector = new Vector3
            (
                (float)(Math.Cos(this.VerticalViewAngle) * Math.Sin(this.HorizontalViewAngle)),
                (float)Math.Sin(this.VerticalViewAngle),
                (float)(Math.Cos(this.VerticalViewAngle) * Math.Cos(this.HorizontalViewAngle))
            );

            this.RightVector = new Vector3
            (
                (float)Math.Sin(this.HorizontalViewAngle - MathHelper.PiOver2),
                0,
                (float)Math.Cos(this.HorizontalViewAngle - MathHelper.PiOver2)
            );

            this.UpVector = Vector3.Cross(this.RightVector, this.LookDirectionVector);
        }

        /// <summary>
        /// Gets the calculated projection matrix for this camera, using the values contained inside it.
        /// </summary>
        /// <returns>A <see cref="Matrix4"/> projection matrix.</returns>
        public Matrix4 GetProjectionMatrix()
        {
            Matrix4 projectionMatrix;
            if (this.Projection == ProjectionType.Orthographic)
            {
                projectionMatrix = Matrix4.CreateOrthographic
                (
                    this.ViewportWidth,
                    this.ViewportHeight,
                    DefaultNearClippingDistance,
                    DefaultFarClippingDistance
                );
            }
            else
            {
                float aspectRatio = (float)this.ViewportWidth / this.ViewportHeight;
                projectionMatrix = Matrix4.CreatePerspectiveFieldOfView
                (
                    MathHelper.DegreesToRadians((float)EverlookConfiguration.Instance.CameraFOV),
                    aspectRatio,
                    DefaultNearClippingDistance,
                    DefaultFarClippingDistance
                );
            }

            return projectionMatrix;
        }

        /// <summary>
        /// Gets the view matrix of this camera (i.e, where it is looking).
        /// </summary>
        /// <returns>A <see cref="Matrix4"/> view matrix.</returns>
        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt
            (
                this.Position,
                this.Position + this.LookDirectionVector,
                this.UpVector
            );
        }

        /// <summary>
        /// Gets the NDC to screen transformation matrix.
        /// </summary>
        /// <returns>The viewport matrix.</returns>
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1117:ParametersMustBeOnSameLineOrSeparateLines", Justification = "Used for matrix parameter alignment.")]
        public Matrix3 GetViewportMatrix()
        {
            float widthOver2 = this.ViewportWidth / 2.0f;
            float heightOver2 = this.ViewportHeight / 2.0f;

            return new Matrix3
            (
                widthOver2, 0,           widthOver2,
                0,          heightOver2, heightOver2,
                0,          0,           1
            );
        }
    }
}
