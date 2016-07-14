//
//  ViewportRenderer.cs
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

namespace Everlook.Viewport.Rendering
{
	public class ViewportCamera
	{
		/// <summary>
		/// The current position of the observer in world space.
		/// </summary>
		private Vector3 cameraPosition;

		/// <summary>
		/// A vector pointing in the direction the observer is currently looking.
		/// </summary>
		private Vector3 cameraLookDirection;

		/// <summary>
		/// The current vector that points directly to the right, relative to the
		/// current orientation of the observer.
		/// </summary>
		private Vector3 cameraRightVector;

		/// <summary>
		/// The current vector that points directly upwards, relative to the current
		/// orientation of the observer.
		/// </summary>
		private Vector3 cameraUpVector;

		/// <summary>
		/// The current horizontal view angle of the observer.
		/// </summary>
		private float horizontalViewAngle;

		/// <summary>
		/// The current vertical view angle of the observer. This angle is
		/// limited to -90 and 90 (straight down and straight up, respectively).
		/// </summary>
		private float verticalViewAngle;

		/*
			Default camera and movement speeds.
		*/

		/// <summary>
		/// The default field of view for perspective projections.
		/// </summary>
		public const float DefaultFieldOfView = 45.0f;

		/// <summary>
		/// The default movement speed of the observer within the viewport.
		/// </summary>
		public const float DefaultMovementSpeed = 10.0f;

		/// <summary>
		/// The default turning speed of the observer within the viewport.
		/// </summary>
		public const float DefaultTurningSpeed = 1.0f;

		/// <summary>
		/// The default near clipping distance.
		/// </summary>
		public const float DefaultNearClippingDistance = 0.01f;

		/// <summary>
		/// The default far clipping distance.
		/// </summary>
		public const float DefaultFarClippingDistance = 10000.0f;

		public ViewportCamera()
		{
			ResetPosition();
		}

		/// <summary>
		/// Resets the camera to the default position.
		/// </summary>
		public void ResetPosition()
		{
			this.cameraPosition = new Vector3(0.0f, 0.0f, 1.0f);
			this.horizontalViewAngle = MathHelper.DegreesToRadians(180.0f);
			this.verticalViewAngle = MathHelper.DegreesToRadians(0.0f);

			this.cameraLookDirection = new Vector3(
				(float) (Math.Cos(this.verticalViewAngle) * Math.Sin(this.horizontalViewAngle)),
				(float) Math.Sin(this.verticalViewAngle),
				(float) (Math.Cos(this.verticalViewAngle) * Math.Cos(this.horizontalViewAngle)));

			this.cameraRightVector = new Vector3(
				(float) Math.Sin(horizontalViewAngle - MathHelper.PiOver2),
				0,
				(float) Math.Cos(horizontalViewAngle - MathHelper.PiOver2));

			this.cameraUpVector = Vector3.Cross(cameraRightVector, cameraLookDirection);
		}

		/// <summary>
		/// Calculates the relative position of the observer in world space, using
		/// input relayed from the main interface.
		/// </summary>
		public void CalculateMovement(float deltaMouseX, float deltaMouseY, float deltaTime, float ForwardAxis, float RightAxis)
		{
			this.horizontalViewAngle += deltaMouseX * DefaultTurningSpeed * deltaTime;
			this.verticalViewAngle += deltaMouseY * DefaultTurningSpeed * deltaTime;

			if (verticalViewAngle > MathHelper.DegreesToRadians(90.0f))
			{
				verticalViewAngle = MathHelper.DegreesToRadians(90.0f);
			}
			else if (verticalViewAngle < MathHelper.DegreesToRadians(-90.0f))
			{
				verticalViewAngle = MathHelper.DegreesToRadians(-90.0f);
			}

			// Compute the look direction
			this.cameraLookDirection = new Vector3(
				(float) (Math.Cos(this.verticalViewAngle) * Math.Sin(this.horizontalViewAngle)),
				(float) Math.Sin(this.verticalViewAngle),
				(float) (Math.Cos(this.verticalViewAngle) * Math.Cos(this.horizontalViewAngle)));

			this.cameraRightVector = new Vector3(
				(float) Math.Sin(this.horizontalViewAngle - MathHelper.PiOver2),
				0,
				(float) Math.Cos(this.horizontalViewAngle - MathHelper.PiOver2));

			this.cameraUpVector = Vector3.Cross(this.cameraRightVector, this.cameraLookDirection);

			// Perform any movement
			if (ForwardAxis > 0)
			{
				this.cameraPosition += this.cameraLookDirection * (deltaTime * DefaultMovementSpeed);
			}

			if (ForwardAxis < 0)
			{
				this.cameraPosition -= this.cameraLookDirection * (deltaTime * DefaultMovementSpeed);
			}

			if (RightAxis > 0)
			{
				this.cameraPosition += this.cameraRightVector * (deltaTime * DefaultMovementSpeed);
			}

			if (RightAxis < 0)
			{
				this.cameraPosition -= this.cameraRightVector * (deltaTime * DefaultMovementSpeed);
			}
		}

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

		public Matrix4 GetViewMatrix()
		{
			return Matrix4.LookAt(
				cameraPosition,
				cameraPosition + cameraLookDirection,
				cameraUpVector
			);
		}
	}
}