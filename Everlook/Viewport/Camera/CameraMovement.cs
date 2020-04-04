//
//  CameraMovement.cs
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
using System.Numerics;
using Everlook.Configuration;
using Everlook.Utility;

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
        /// The default movement speed of the observer within the viewport, in world units per second.
        /// </summary>
        private const float DefaultMovementSpeed = 50.0f;

        /// <summary>
        /// The default turning speed of the observer within the viewport, in degrees per second.
        /// </summary>
        private const float DefaultTurningSpeed = 5.0f;

        private readonly ViewportCamera _camera;

        /// <summary>
        /// Gets or sets the current orientation of the bound camera.
        /// </summary>
        public Vector3 Orientation
        {
            get
            {
                return new Vector3
                (
                    (float)MathHelper.RadiansToDegrees(_camera.VerticalViewAngle),
                    (float)MathHelper.RadiansToDegrees(_camera.HorizontalViewAngle),
                    0
                );
            }

            set
            {
                _camera.VerticalViewAngle = MathHelper.DegreesToRadians(value.X);
                _camera.HorizontalViewAngle = MathHelper.DegreesToRadians(value.Y);
            }
        }

        /// <summary>
        /// Gets or sets the current position of the bound camera.
        /// </summary>
        public Vector3 Position
        {
            get => _camera.Position;
            set => _camera.Position = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to constrain the vertical view angle to -/+ 90 degrees. This
        /// prevents the viewer from going upside down.
        /// </summary>
        public bool ConstrainVerticalView
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the user wants to move left.
        /// </summary>
        public bool WantsToMoveLeft { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user wants to move right.
        /// </summary>
        public bool WantsToMoveRight { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user wants to move forward.
        /// </summary>
        public bool WantsToMoveForward { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user wants to move backward.
        /// </summary>
        public bool WantsToMoveBackward { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user wants to move up.
        /// </summary>
        public bool WantsToMoveUp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user wants to move down.
        /// </summary>
        public bool WantsToMoveDown { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user wants to move faster.
        /// </summary>
        public bool WantsToSprint { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CameraMovement"/> class, bound to the input camera.
        /// </summary>
        /// <param name="inCamera">The camera which the movement component should control.</param>
        public CameraMovement(ViewportCamera inCamera)
        {
            _camera = inCamera;
            this.ConstrainVerticalView = true;
        }

        /// <summary>
        /// Calculates and applies the position of the camera in screen space, using input
        /// relayed from the main interface. This function is used for 2D orthographic projection.
        /// </summary>
        /// <param name="deltaMouseX">The motion delta along the X axis of the mouse in the last frame.</param>
        /// <param name="deltaMouseY">The motion delta along the Y axis of the mouse in the last frame.</param>
        /// <param name="deltaTime">The time delta between this frame and the previous one.</param>
        public void Calculate2DMovement(double deltaMouseX, double deltaMouseY, float deltaTime)
        {
            const float speedMultiplier = 6.0f;
            if (deltaMouseX < 0)
            {
                MoveLeft(deltaMouseX * deltaTime * DefaultMovementSpeed * speedMultiplier);
            }
            else
            {
                MoveRight(deltaMouseX * deltaTime * DefaultMovementSpeed * speedMultiplier);
            }

            if (deltaMouseY < 0)
            {
                MoveUp(deltaMouseY * deltaTime * DefaultMovementSpeed * speedMultiplier);
            }
            else
            {
                MoveDown(deltaMouseY * deltaTime * DefaultMovementSpeed * speedMultiplier);
            }
        }

        /// <summary>
        /// Calculates the relative position of the observer in world space, using
        /// input relayed from the main interface. This function is used for 3D projection.
        /// </summary>
        /// <param name="deltaMouseX">The motion delta along the X axis of the mouse in the last frame.</param>
        /// <param name="deltaMouseY">The motion delta along the Y axis of the mouse in the last frame.</param>
        /// <param name="deltaTime">The time delta between this frame and the previous one.</param>
        public void Calculate3DMovement(double deltaMouseX, double deltaMouseY, float deltaTime)
        {
            // Perform radial movement
            RotateHorizontal
            (
                deltaMouseX * DefaultTurningSpeed * (float)EverlookConfiguration.Instance.RotationSpeed * deltaTime
            );

            RotateVertical
            (
                deltaMouseY * DefaultTurningSpeed * (float)EverlookConfiguration.Instance.RotationSpeed * deltaTime
            );

            // Constrain the viewing angles to no more than 90 degrees in any direction
            if (this.ConstrainVerticalView)
            {
                if (_camera.VerticalViewAngle > MathHelper.DegreesToRadians(90.0f))
                {
                    _camera.VerticalViewAngle = MathHelper.DegreesToRadians(90.0);
                }
                else if (_camera.VerticalViewAngle < MathHelper.DegreesToRadians(-90.0))
                {
                    _camera.VerticalViewAngle = MathHelper.DegreesToRadians(-90.0);
                }
            }

            var speedMultiplier = (float)(DefaultMovementSpeed * EverlookConfiguration.Instance.CameraSpeed);

            if (this.WantsToSprint)
            {
                speedMultiplier *= (float)EverlookConfiguration.Instance.SprintMultiplier;
            }

            var moveDistance = deltaTime * speedMultiplier;

            // Perform axial movement
            if (this.WantsToMoveForward)
            {
                MoveForward(moveDistance);
            }

            if (this.WantsToMoveBackward)
            {
                MoveBackward(moveDistance);
            }

            if (this.WantsToMoveLeft)
            {
                MoveLeft(moveDistance);
            }

            if (this.WantsToMoveRight)
            {
                MoveRight(moveDistance);
            }

            if (this.WantsToMoveUp)
            {
                MoveUp(moveDistance);
            }

            if (this.WantsToMoveDown)
            {
                MoveDown(moveDistance);
            }
        }

        /// <summary>
        /// Rotates the camera on the horizontal axis by the provided amount of degrees.
        /// </summary>
        /// <param name="degrees">The number of degrees to rotate.</param>
        public void RotateHorizontal(double degrees)
        {
            _camera.HorizontalViewAngle += degrees;
        }

        /// <summary>
        /// Rotates the camera on the vertical axis by the provided amount of degrees.
        /// </summary>
        /// <param name="degrees">The number of degrees to rotate.</param>
        public void RotateVertical(double degrees)
        {
            _camera.VerticalViewAngle += degrees;
        }

        /// <summary>
        /// Moves the camera up along its local Y axis by <paramref name="distance"/> units.
        /// </summary>
        /// <param name="distance">The distance to move.</param>
        public void MoveUp(double distance)
        {
            _camera.Position += _camera.UpVector * (float)Math.Abs(distance);
        }

        /// <summary>
        /// Moves the camera down along its local Y axis by <paramref name="distance"/> units.
        /// </summary>
        /// <param name="distance">The distance to move.</param>
        public void MoveDown(double distance)
        {
            _camera.Position -= _camera.UpVector * (float)Math.Abs(distance);
        }

        /// <summary>
        /// Moves the camera forward along its local Z axis by <paramref name="distance"/> units.
        /// </summary>
        /// <param name="distance">The distance to move.</param>
        public void MoveForward(double distance)
        {
            _camera.Position += _camera.LookDirectionVector * (float)Math.Abs(distance);
        }

        /// <summary>
        /// Moves the camera backwards along its local Z axis by <paramref name="distance"/> units.
        /// </summary>
        /// <param name="distance">The distance to move.</param>
        public void MoveBackward(double distance)
        {
            _camera.Position -= _camera.LookDirectionVector * (float)Math.Abs(distance);
        }

        /// <summary>
        /// Moves the camera left along its local X axis by <paramref name="distance"/> units.
        /// </summary>
        /// <param name="distance">The distance to move.</param>
        public void MoveLeft(double distance)
        {
            _camera.Position -= _camera.RightVector * (float)Math.Abs(distance);
        }

        /// <summary>
        /// Moves the camera right along its local X axis by <paramref name="distance"/> units.
        /// </summary>
        /// <param name="distance">The distance to move.</param>
        public void MoveRight(double distance)
        {
            _camera.Position += _camera.RightVector * (float)Math.Abs(distance);
        }
    }
}
