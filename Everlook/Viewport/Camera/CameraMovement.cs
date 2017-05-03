//
//  CameraMovement.cs
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
using GLib;
using log4net;
using OpenTK;
using OpenTK.Input;

namespace Everlook.Viewport.Camera
{
	/// <summary>
	/// Camera movement component. This class is bound to a single camera instance, and handles
	/// relative movement inside the world for it. A number of simple movement methods are exposed
	/// which makes handling it from the outside easier.
	/// </summary>
	public class CameraMovement
	{
		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(CameraMovement));

		private readonly ViewportCamera Camera;

		/// <summary>
		/// The current orientation of the bound camera.
		/// </summary>
		public Vector3 Orientation
		{
			get
			{
				return new Vector3(MathHelper.RadiansToDegrees(this.Camera.VerticalViewAngle),
					MathHelper.RadiansToDegrees(this.Camera.HorizontalViewAngle), 0);
			}
			set
			{
				this.Camera.VerticalViewAngle = MathHelper.DegreesToRadians(value.X);
				this.Camera.HorizontalViewAngle = MathHelper.DegreesToRadians(value.Y);
			}
		}

		/// <summary>
		/// The current position of the bound camera.
		/// </summary>
		public Vector3 Position
		{
			get
			{
				return this.Camera.Position;
			}
			set
			{
				this.Camera.Position = value;
			}
		}

		/// <summary>
		/// Whether or not to constrain the vertical view angle to -/+ 90 degrees. This
		/// prevents the viewer from going upside down.
		/// </summary>
		public bool ConstrainVerticalView
		{
			get;
			set;
		}

		/// <summary>
		/// The default movement speed of the observer within the viewport.
		/// </summary>
		private const float DefaultMovementSpeed = 10.0f;

		/// <summary>
		/// The default turning speed of the observer within the viewport.
		/// </summary>
		private const float DefaultTurningSpeed = 0.5f;

		/// <summary>
		/// Creates a new <see cref="CameraMovement"/> instance, bound to the input camera.
		/// </summary>
		public CameraMovement(ViewportCamera inCamera)
		{
			this.Camera = inCamera;
			this.ConstrainVerticalView = true;
		}

		/// <summary>
		/// Calculates the relative position of the observer in world space, using
		/// input relayed from the main interface.
		/// </summary>
		public void CalculateMovement(float deltaMouseX, float deltaMouseY, float deltaTime)
		{
			Log.Debug($"Moving: DeltaTime is {deltaTime}");

			// Perform radial movement
			RotateHorizontal(deltaMouseX * DefaultTurningSpeed * deltaTime);
			RotateVertical(deltaMouseY * DefaultTurningSpeed * deltaTime);

			// Constrain the viewing angles to no more than 90 degrees in any direction
			if (this.ConstrainVerticalView)
			{
				if (this.Camera.VerticalViewAngle > MathHelper.DegreesToRadians(90.0f))
				{
					this.Camera.VerticalViewAngle = MathHelper.DegreesToRadians(90.0f);
				}
				else if (this.Camera.VerticalViewAngle < MathHelper.DegreesToRadians(-90.0f))
				{
					this.Camera.VerticalViewAngle = MathHelper.DegreesToRadians(-90.0f);
				}
			}

			// Perform axial movement
			if (Keyboard.GetState().IsKeyDown(Key.W))
			{
				MoveForward(deltaTime * DefaultMovementSpeed);
			}

			if (Keyboard.GetState().IsKeyDown(Key.S))
			{
				MoveBackward(deltaTime * DefaultMovementSpeed);
			}

			if (Keyboard.GetState().IsKeyDown(Key.A))
			{
				MoveLeft(deltaTime * DefaultMovementSpeed);
			}
			if (Keyboard.GetState().IsKeyDown(Key.D))
			{
				MoveRight(deltaTime * DefaultMovementSpeed);
			}


			if (Keyboard.GetState().IsKeyDown(Key.Q))
			{
				MoveUp(deltaTime * DefaultMovementSpeed);
			}

			if (Keyboard.GetState().IsKeyDown(Key.E))
			{
				MoveDown(deltaTime * DefaultMovementSpeed);
			}
		}

		/// <summary>
		/// Rotates the camera on the horizontal axis by the provided amount of degrees.
		/// </summary>
		public void RotateHorizontal(float degrees)
		{
			this.Camera.HorizontalViewAngle += degrees;
		}

		/// <summary>
		/// Rotates the camera on the vertical axis by the provided amount of degrees.
		/// </summary>
		public void RotateVertical(float degrees)
		{
			this.Camera.VerticalViewAngle += degrees;
		}

		/// <summary>
		/// Moves the camera up along its local Y axis by <paramref name="distance"/> units.
		/// </summary>
		public void MoveUp(float distance)
		{
			this.Camera.Position += this.Camera.UpVector * Math.Abs(distance);
		}

		/// <summary>
		/// Moves the camera down along its local Y axis by <paramref name="distance"/> units.
		/// </summary>
		public void MoveDown(float distance)
		{
			this.Camera.Position -= this.Camera.UpVector * Math.Abs(distance);
		}

		/// <summary>
		/// Moves the camera forward along its local Z axis by <paramref name="distance"/> units.
		/// </summary>
		public void MoveForward(float distance)
		{
			this.Camera.Position += this.Camera.LookDirectionVector * Math.Abs(distance);
		}

		/// <summary>
		/// Moves the camera backwards along its local Z axis by <paramref name="distance"/> units.
		/// </summary>
		public void MoveBackward(float distance)
		{
			this.Camera.Position -= this.Camera.LookDirectionVector * Math.Abs(distance);
		}

		/// <summary>
		/// Moves the camera left along its local X axis by <paramref name="distance"/> units.
		/// </summary>
		public void MoveLeft(float distance)
		{
			this.Camera.Position -= this.Camera.RightVector * Math.Abs(distance);
		}

		/// <summary>
		/// Moves the camera right along its local X axis by <paramref name="distance"/> units.
		/// </summary>
		public void MoveRight(float distance)
		{
			this.Camera.Position += this.Camera.RightVector * Math.Abs(distance);
		}
	}
}