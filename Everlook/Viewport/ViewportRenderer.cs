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
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Interfaces;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

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

		public bool HasRenderTarget
		{
			get { return this.RenderTarget != null; }
		}

		/// <summary>
		/// The current rendering target. This is an object capable of being shown in an
		/// OpenGL viewport.
		/// </summary>
		private IRenderable RenderTarget;

		/// <summary>
		/// The camera viewpoint of the observer.
		/// </summary>
		public ViewportCamera Camera;

		/// <summary>
		/// The movement component for the camera.
		/// </summary>
		private CameraMovement Movement;

		/// <summary>
		/// The time taken to render the previous frame.
		/// </summary>
		private float deltaTime;

		/// <summary>
		///
		/// </summary>
		public bool WantsToMove { get; set; }

		/// <summary>
		/// The X position of the mouse during the last frame, relative to the <see cref="ViewportWidget"/>.
		/// </summary>
		public int InitialMouseX;

		/// <summary>
		/// The Y position of the mouse during the last frame, relative to the <see cref="ViewportWidget"/>.
		/// </summary>
		public int InitialMouseY;

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
		/// Frame timing stopwatch, used to calculate deltaTime.
		/// </summary>
		private readonly Stopwatch frameWatch = new Stopwatch();

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
			this.Camera = new ViewportCamera();
			this.Movement = new CameraMovement(this.Camera);

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
			GL.DepthFunc(DepthFunction.Lequal);
			GL.DepthMask(true);

			// Enable backface culling for performance reasons
			GL.Enable(EnableCap.CullFace);

			// Set a simple default blending function
			GL.Enable(EnableCap.Blend);
			GL.BlendEquation(BlendEquationMode.FuncAdd);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

			// Initialize the viewport
			int widgetWidth = this.ViewportWidget.AllocatedWidth;
			int widgetHeight = this.ViewportWidget.AllocatedHeight;
			GL.Viewport(0, 0, widgetWidth, widgetHeight);
			GL.ClearColor(
				(float) this.Config.GetViewportBackgroundColour().Red,
				(float) this.Config.GetViewportBackgroundColour().Green,
				(float) this.Config.GetViewportBackgroundColour().Blue,
				(float) this.Config.GetViewportBackgroundColour().Alpha);

			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			this.IsInitialized = true;
		}

		/// <summary>
		/// The primary rendering logic. Here, the current object is rendered using OpenGL.
		/// </summary>
		public void RenderFrame()
		{
			lock (this.RenderTargetLock)
			{
				frameWatch.Reset();
				frameWatch.Start();

				// Make sure the viewport is accurate for the current widget size on screen
				int widgetWidth = this.ViewportWidget.AllocatedWidth;
				int widgetHeight = this.ViewportWidget.AllocatedHeight;
				GL.Viewport(0, 0, widgetWidth, widgetHeight);
				GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

				if (this.RenderTarget != null)
				{

					// Calculate the current relative movement of the camera
					if (this.WantsToMove)
					{
						int mouseX = Mouse.GetCursorState().X;
						int mouseY = Mouse.GetCursorState().Y;

						float deltaMouseX = this.InitialMouseX - mouseX;
						float deltaMouseY = this.InitialMouseY - mouseY;

						this.Movement.CalculateMovement(deltaMouseX, deltaMouseY, this.deltaTime);

						// Return the mouse to its original position
						Mouse.SetPosition(this.InitialMouseX, this.InitialMouseY);
					}

					// Render the current object
					// Tick the actor, advancing any time-dependent behaviour
					ITickingActor tickingRenderable = this.RenderTarget as ITickingActor;
					if (tickingRenderable != null)
					{
						tickingRenderable.Tick(this.deltaTime);
					}

					// Then render the visual component
					Matrix4 view = this.Camera.GetViewMatrix();
					Matrix4 projection = this.Camera.GetProjectionMatrix(this.RenderTarget.Projection, widgetWidth, widgetHeight);
					this.Camera.RecalculateFrustrum(widgetWidth, widgetHeight);

					this.RenderTarget.Render(view, projection, this.Camera);

					GraphicsContext.CurrentContext.SwapBuffers();

				}

				frameWatch.Stop();
				this.deltaTime = (float) frameWatch.Elapsed.TotalMilliseconds / 1000;
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
			lock (this.RenderTargetLock)
			{
				// Dispose of the old render target
				if (this.RenderTarget != null)
				{
					this.RenderTarget.Dispose();
				}

				// Assign the new one
				this.RenderTarget = inRenderable;
			}

			this.Camera.ResetPosition();
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

