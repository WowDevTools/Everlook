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
using System.Diagnostics;
using Everlook.Configuration;
using Everlook.Viewport.Rendering.Interfaces;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace Everlook.Viewport
{
	/// <summary>
	/// Viewport renderer for the main Everlook UI. This class manages an OpenGL rendering thread, which
	/// uses rendering built into the different renderable objects
	/// </summary>
	public class ViewportRenderer : IDisposable
	{
		/// <summary>
		/// The viewport widget displayed to the user in the main interface.
		/// Used to get proper dimensions for the OpenGL viewport.
		/// </summary>
		private readonly GLWidget ViewportWidget;

		/*
			RenderTarget and related control flow data.
		*/

		/// <summary>
		/// A lock object used to enforce that the rendering target can finish its current
		/// frame before a new one is assigned.
		/// </summary>
		private readonly object RenderTargetLock = new object();

		/// <summary>
		/// The current rendering target. This is an object capable of being shown in an
		/// OpenGL viewport.
		/// </summary>
		private IRenderable RenderTarget;

		/*
			Runtime positional data for the observer.
		*/

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

		/// <summary>
		/// Whether or not the user wants to move in world space. If set to true, the
		/// rendering loop will recalculate the view and projection matrices every frame.
		/// </summary>
		public bool WantsToMove = false;

		/// <summary>
		/// The time taken to render the previous frame.
		/// </summary>
		private float DeltaTime;

		/// <summary>
		/// The X position of the mouse during the last frame, relative to the <see cref="ViewportWidget"/>.
		/// </summary>
		public int MouseXLastFrame;

		/// <summary>
		/// The Y position of the mouse during the last frame, relative to the <see cref="ViewportWidget"/>.
		/// </summary>
		public int MouseYLastFrame;

		/// <summary>
		/// The current desired movement direction of the right axis.
		///
		/// A positive value represents movement to the right at a speed matching <see cref="DefaultMovementSpeed"/>
		/// multiplied by the axis value.
		///
		/// A negative value represents movement to the left at a speed matching <see cref="DefaultMovementSpeed"/>
		/// multiplied by the axis value.
		///
		/// A value of <value>0</value> represents no movement.
		/// </summary>
		public float RightAxis;

		/// <summary>
		/// The current desired movement direction of the right axis.
		///
		/// A positive value represents forwards movement at a speed matching <see cref="DefaultMovementSpeed"/>
		/// multiplied by the axis value.
		///
		/// A negative value represents backwards movement at a speed matching <see cref="DefaultMovementSpeed"/>
		/// multiplied by the axis value.
		///
		/// A value of <value>0</value> represents no movement.
		/// </summary>
		public float ForwardAxis;


		/*
			Default camera and movement speeds.
		*/

		/// <summary>
		/// The default field of view for perspective projections.
		/// </summary>
		private const float DefaultFieldOfView = 45.0f;

		/// <summary>
		/// The default movement speed of the observer within the viewport.
		/// </summary>
		private const float DefaultMovementSpeed = 5.0f;

		/// <summary>
		/// The default turning speed of the observer within the viewport.
		/// </summary>
		private const float DefaultTurningSpeed = 0.05f;


		/*
			Runtime transitional OpenGL data.
		*/

		/// <summary>
		/// The OpenGL ID of the vertex array valid for the current context.
		/// </summary>
		private int VertexArrayID;

		/// <summary>
		/// Whether or not this instance has been initialized and is ready
		/// to render objects.
		/// </summary>
		public bool IsInitialized
		{
			get;
			set;
		}

		/*
			Everlook caching and static data accessors.
		*/

		/// <summary>
		/// Static reference to the configuration handler.
		/// </summary>
		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Viewport.ViewportRenderer"/> class.
		/// </summary>
		public ViewportRenderer(GLWidget viewportWidget)
		{
			this.ViewportWidget = viewportWidget;
			this.IsInitialized = false;
		}

		/// <summary>
		/// Initializes
		/// </summary>
		public void Initialize()
		{
			// Generate the vertex array
			GL.GenVertexArrays(1, out this.VertexArrayID);
			GL.BindVertexArray(this.VertexArrayID);

			// Make sure we use the depth buffer when drawing
			GL.Enable(EnableCap.DepthTest);
			GL.DepthFunc(DepthFunction.Less);

			// Enable backface culling for performance reasons
			GL.Enable(EnableCap.CullFace);
			GL.Enable(EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

			// Initialize the viewport
			int widgetWidth = this.ViewportWidget.AllocatedWidth;
			int widgetHeight = this.ViewportWidget.AllocatedHeight;
			GL.Viewport(0, 0, widgetWidth, widgetHeight);
			GL.ClearColor(
				(float)Config.GetViewportBackgroundColour().Red,
				(float)Config.GetViewportBackgroundColour().Green,
				(float)Config.GetViewportBackgroundColour().Blue,
				(float)Config.GetViewportBackgroundColour().Alpha);

			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			ResetCamera();

			this.IsInitialized = true;
		}

		/// <summary>
		/// The primary rendering logic. Here, the current object is rendered using OpenGL.
		/// </summary>
		public void RenderFrame()
		{
			lock (RenderTargetLock)
			{
				// Make sure the viewport is accurate for the current widget size on screen
				int widgetWidth = this.ViewportWidget.AllocatedWidth;
				int widgetHeight = this.ViewportWidget.AllocatedHeight;
				GL.Viewport(0, 0, widgetWidth, widgetHeight);
				GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

				if (RenderTarget != null)
				{
					// Calculate the current relative movement of the camera
					if (WantsToMove)
					{
						CalulateRelativeMovementVectors();
					}

					// Calculate the relative viewpoint
					Matrix4 projection;
					if (RenderTarget.Projection == ProjectionType.Orthographic)
					{
						projection = Matrix4.CreateOrthographic(widgetWidth, widgetHeight, 0.01f, 1000.0f);
					}
					else
					{
						float aspectRatio = (float)widgetWidth / (float)widgetHeight;
						projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(DefaultFieldOfView), aspectRatio, 0.01f, 1000.0f);
					}

					Matrix4 view = Matrix4.LookAt(
						cameraPosition,
						cameraPosition + cameraLookDirection,
						cameraUpVector
					);

					// Render the current object
					Stopwatch sw = Stopwatch.StartNew();
					{
						// Tick the actor, advancing any time-dependent behaviour
						ITickingActor tickingRenderable = RenderTarget as ITickingActor;
						if (tickingRenderable != null)
						{
							tickingRenderable.Tick(DeltaTime);
						}

						// Then render the visual component
						RenderTarget.Render(view, projection);
					}
					sw.Stop();
					DeltaTime = (float)sw.Elapsed.TotalMilliseconds;

					GraphicsContext.CurrentContext.SwapBuffers();
				}
			}
		}

		/// <summary>
		/// Determines whether or not movement is currently disabled for the rendered object.
		/// </summary>
		public bool IsMovementDisabled()
		{
			return this.RenderTarget == null ||
			       this.RenderTarget.IsStatic ||
			       !this.RenderTarget.IsInitialized;
		}

		/// <summary>
		/// Sets the render target that is currently being rendered by the viewport renderer.
		/// </summary>
		/// <param name="inRenderable">inRenderable.</param>
		public void SetRenderTarget(IRenderable inRenderable)
		{
			lock (RenderTargetLock)
			{
				// Dispose of the old render target
				if (this.RenderTarget != null)
				{
					this.RenderTarget.Dispose();
				}

				// Assign the new one
				this.RenderTarget = inRenderable;
			}

			ResetCamera();
		}

		/// <summary>
		/// Resets the camera to the default position.
		/// </summary>
		public void ResetCamera()
		{
			this.cameraPosition = new Vector3(0.0f, 0.0f, 1.0f);
			this.horizontalViewAngle = MathHelper.DegreesToRadians(180.0f);
			this.verticalViewAngle = MathHelper.DegreesToRadians(0.0f);

			this.cameraLookDirection = new Vector3(
				(float)(Math.Cos(this.verticalViewAngle) * Math.Sin(this.horizontalViewAngle)),
				(float)Math.Sin(this.verticalViewAngle),
				(float)(Math.Cos(this.verticalViewAngle) * Math.Cos(this.horizontalViewAngle)));

			this.cameraRightVector = new Vector3(
				(float)Math.Sin(horizontalViewAngle - MathHelper.PiOver2),
				0,
				(float)Math.Cos(horizontalViewAngle - MathHelper.PiOver2));

			this.cameraUpVector = Vector3.Cross(cameraRightVector, cameraLookDirection);
		}

		/// <summary>
		/// Calculates the relative position of the observer in world space, using
		/// input relayed from the main interface.
		/// </summary>
		private void CalulateRelativeMovementVectors()
		{
			int mouseX;
			int mouseY;
			this.ViewportWidget.GetPointer(out mouseX, out mouseY);

			this.horizontalViewAngle += DefaultTurningSpeed * this.DeltaTime * (MouseXLastFrame - mouseX);
			this.verticalViewAngle += DefaultTurningSpeed * this.DeltaTime * (MouseYLastFrame - mouseY);

			if (verticalViewAngle > MathHelper.DegreesToRadians(90.0f))
			{
				verticalViewAngle = MathHelper.DegreesToRadians(90.0f);
			}
			else if (verticalViewAngle < MathHelper.DegreesToRadians(-90.0f))
			{
				verticalViewAngle = MathHelper.DegreesToRadians(-90.0f);
			}

			MouseXLastFrame = mouseX;
			MouseYLastFrame = mouseY;

			// Compute the look direction
			this.cameraLookDirection = new Vector3(
				(float)(Math.Cos(this.verticalViewAngle) * Math.Sin(this.horizontalViewAngle)),
				(float)Math.Sin(this.verticalViewAngle),
				(float)(Math.Cos(this.verticalViewAngle) * Math.Cos(this.horizontalViewAngle)));

			this.cameraRightVector = new Vector3(
				(float)Math.Sin(this.horizontalViewAngle - MathHelper.PiOver2),
				0,
				(float)Math.Cos(this.horizontalViewAngle - MathHelper.PiOver2));

			this.cameraUpVector = Vector3.Cross(this.cameraRightVector, this.cameraLookDirection);

			// Perform any movement
			if (ForwardAxis > 0)
			{
				this.cameraPosition += this.cameraLookDirection * DeltaTime * DefaultMovementSpeed;
			}

			if (ForwardAxis < 0)
			{
				this.cameraPosition -= this.cameraLookDirection * DeltaTime * DefaultMovementSpeed;
			}

			if (RightAxis > 0)
			{
				this.cameraPosition += this.cameraRightVector * DeltaTime * DefaultMovementSpeed;
			}

			if (RightAxis < 0)
			{
				this.cameraPosition -= this.cameraRightVector * DeltaTime * DefaultMovementSpeed;
			}
		}

		/// <summary>
		/// Disposes the viewport renderer, releasing the current rendering target and current
		/// OpenGL arrays and buffers.
		/// </summary>
		public void Dispose()
		{
			if (this.RenderTarget != null)
			{
				this.RenderTarget.Dispose();
			}

			GL.DeleteVertexArrays(1, ref this.VertexArrayID);
		}
	}
}

