//
//  ViewportCamera.cs
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
using Everlook.Viewport.Rendering.Interfaces;
using OpenTK;
using SlimTK;

namespace Everlook.Viewport.Camera
{
	/// <summary>
	/// A camera in the world, represented by a position and a set of viewing angles.
	///</summary>
	public class ViewportCamera
	{
		/// <summary>
		/// The current position of the observer in world space.
		/// </summary>
		public Vector3 Position;

		/// <summary>
		/// A vector pointing in the direction the observer is currently looking.
		/// </summary>
		public Vector3 LookDirectionVector
		{
			get;
			private set;
		}

		/// <summary>
		/// The current vector that points directly to the right, relative to the
		/// current orientation of the observer.
		/// </summary>
		public Vector3 RightVector
		{
			get;
			private set;
		}

		/// <summary>
		/// The current vector that points directly upwards, relative to the current
		/// orientation of the observer.
		/// </summary>
		public Vector3 UpVector
		{
			get;
			private set;
		}

		/// <summary>
		/// The current horizontal view angle of the observer.
		/// </summary>
		public float HorizontalViewAngle
		{
			get
			{
				return this.horizontalViewAngle;
			}
			set
			{
				this.horizontalViewAngle = value;
				RecalculateVectors();
			}
		}

		private float horizontalViewAngle;

		/// <summary>
		/// The current vertical view angle of the observer. This angle is
		/// limited to -90 and 90 (straight down and straight up, respectively).
		/// </summary>
		public float VerticalViewAngle
		{
			get
			{
				return this.verticalViewAngle;
			}
			set
			{
				this.verticalViewAngle = value;
				RecalculateVectors();
			}
		}

		private float verticalViewAngle;

		/// <summary>
		/// The bounding frustum of the camera.
		/// </summary>
		private BoundingFrustum Frustum;

		/*
			Default camera and movement speeds.
		*/

		/// <summary>
		/// The default field of view for perspective projections.
		/// </summary>
		public const float DefaultFieldOfView = 45.0f;

		/// <summary>
		/// The default near clipping distance.
		/// </summary>
		public const float DefaultNearClippingDistance = 0.1f;

		/// <summary>
		/// The default far clipping distance.
		/// </summary>
		public const float DefaultFarClippingDistance = 1000.0f;

		/// <summary>
		/// Creates a new instance of the <see cref="ViewportCamera"/> class, and sets its position
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
			this.HorizontalViewAngle = MathHelper.DegreesToRadians(180.0f);
			this.VerticalViewAngle = MathHelper.DegreesToRadians(0.0f);

			this.LookDirectionVector = new Vector3(
				(float) (Math.Cos(this.VerticalViewAngle) * Math.Sin(this.HorizontalViewAngle)),
				(float) Math.Sin(this.VerticalViewAngle),
				(float) (Math.Cos(this.VerticalViewAngle) * Math.Cos(this.HorizontalViewAngle)));

			this.RightVector = new Vector3(
				(float) Math.Sin(this.HorizontalViewAngle - MathHelper.PiOver2),
				0,
				(float) Math.Cos(this.HorizontalViewAngle - MathHelper.PiOver2));

			this.UpVector = Vector3.Cross(this.RightVector, this.LookDirectionVector);
		}

		private void RecalculateVectors()
		{
			// Compute the look direction
			this.LookDirectionVector = new Vector3(
				(float) (Math.Cos(this.VerticalViewAngle) * Math.Sin(this.HorizontalViewAngle)),
				(float) Math.Sin(this.VerticalViewAngle),
				(float) (Math.Cos(this.VerticalViewAngle) * Math.Cos(this.HorizontalViewAngle)));

			this.RightVector = new Vector3(
				(float) Math.Sin(this.HorizontalViewAngle - MathHelper.PiOver2),
				0,
				(float) Math.Cos(this.HorizontalViewAngle - MathHelper.PiOver2));

			this.UpVector = Vector3.Cross(this.RightVector, this.LookDirectionVector);
		}

		/// <summary>
		/// Recalculates the bounding frustum of the camera.
		/// </summary>
		/// <param name="projectionMatrix">The matrix to recalulcate from.</param>
		public void RecalculateFrustum(Matrix4 projectionMatrix)
		{
			Matrix4 tempMatrix = projectionMatrix;
			this.Frustum.Matrix = tempMatrix;
		}

		/// <summary>
		/// Gets the calculated projection matrix for this camera, using the values contained inside it.
		/// </summary>
		/// <param name="projectionType">The type of project to calculate for.</param>
		/// <param name="viewportWidth">The current width of the viewport.</param>
		/// <param name="viewportHeight">The current height of the viewport.</param>
		/// <returns>A <see cref="Matrix4"/> projection matrix.</returns>
		public Matrix4 GetProjectionMatrix(ProjectionType projectionType, int viewportWidth, int viewportHeight)
		{
			Matrix4 projection;
			if (projectionType == ProjectionType.Orthographic)
			{
				projection = Matrix4.CreateOrthographic(viewportWidth, viewportHeight,
					DefaultNearClippingDistance, DefaultFarClippingDistance);
			}
			else
			{
				float aspectRatio = (float) viewportWidth / (float) viewportHeight;
				projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(DefaultFieldOfView), aspectRatio,
					DefaultNearClippingDistance, DefaultFarClippingDistance);
			}

			return projection;
		}

		/// <summary>
		/// Gets the view matrix of this camera (i.e, where it is looking)
		/// </summary>
		/// <returns>A <see cref="Matrix4"/> view matrix.</returns>
		public Matrix4 GetViewMatrix()
		{
			return Matrix4.LookAt(this.Position, this.Position + this.LookDirectionVector, this.UpVector
			);
		}

		/// <summary>
		/// Determines whether or not the camera can see the provided bounding box (that is, it is within
		/// the view frustum).
		/// </summary>
		/// <param name="groupBoundingBox">The box to check.</param>
		/// <returns><value>true</value> if the camera can see the box; Otherwise, <value>false</value>.</returns>
		public bool CanSee(BoundingBox groupBoundingBox)
		{
			return this.Frustum.Contains(groupBoundingBox) != ContainmentType.Disjoint;
		}
	}
}