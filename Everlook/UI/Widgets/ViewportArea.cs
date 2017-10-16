//
//  ViewportArea.cs
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
using System.ComponentModel;
using System.Threading;
using Gdk;
using Gtk;
using OpenTK;
using OpenTK.Graphics;

namespace Everlook.UI.Widgets
{
	/// <summary>
	/// The <see cref="ViewportArea"/> is a GTK widget for which an OpenGL context can be used to draw arbitrary graphics.
	/// </summary>
	[CLSCompliant(false)]
	[ToolboxItem(true)]
	public class ViewportArea : GLArea
	{
		private static int GraphicsContextCount;
		private static bool IsSharedContextInitialized;

		private IGraphicsContext TKGraphicsContext;
		private bool IsInitialized;

		/// <summary>
		/// The previous frame time reported by GTK.
		/// </summary>
		private double? PreviousFrameTime;

		/// <summary>
		/// Gets the time taken to render the last frame (in seconds).
		/// </summary>
		public double DeltaTime { get; private set; }

		/// <summary>
		/// Gets the context flags used in the creation of this widget.
		/// </summary>
		public GraphicsContextFlags ContextFlags { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ViewportArea"/> class.
		/// </summary>
		public ViewportArea()
			: this(GraphicsMode.Default)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ViewportArea"/> class. The given <see cref="GraphicsMode"/> is
		/// used to hint the area about context creation.
		/// </summary>
		/// <param name="graphicsMode">The <see cref="GraphicsMode"/> which the widget should be constructed with.</param>
		public ViewportArea(GraphicsMode graphicsMode)
			: this(graphicsMode, 1, 0, GraphicsContextFlags.Default)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ViewportArea"/> class.
		/// </summary>
		/// <param name="graphicsMode">The <see cref="GraphicsMode"/> which the widget should be constructed with.</param>
		/// <param name="glVersionMajor">The major OpenGL version to attempt to initialize.</param>
		/// <param name="glVersionMinor">The minor OpenGL version to attempt to initialize.</param>
		/// <param name="contextFlags">
		/// Any flags which should be used during initialization of the <see cref="GraphicsContext"/>.
		/// </param>
		public ViewportArea(GraphicsMode graphicsMode, int glVersionMajor, int glVersionMinor, GraphicsContextFlags contextFlags)
		{
			this.ContextFlags = contextFlags;

			AddTickCallback(UpdateFrameTime);

			SetRequiredVersion(glVersionMajor, glVersionMinor);

			if (graphicsMode.Depth > 0)
			{
				this.HasDepthBuffer = true;
			}

			if (graphicsMode.Stencil > 0)
			{
				this.HasStencilBuffer = true;
			}

			if (graphicsMode.ColorFormat.Alpha > 0)
			{
				this.HasAlpha = true;
			}
		}

		/// <summary>
		/// Updates the time delta with a new value from the last frame.
		/// </summary>
		/// <param name="widget">The sending widget.</param>
		/// <param name="frameClock">The relevant frame clock.</param>
		/// <returns>true if the callback should be called again; otherwise, false.</returns>
		private bool UpdateFrameTime(Widget widget, FrameClock frameClock)
		{
			var frameTimeµSeconds = frameClock.FrameTime;

			if (!this.PreviousFrameTime.HasValue)
			{
				this.PreviousFrameTime = frameTimeµSeconds;

				return true;
			}

			var frameTimeSeconds = (frameTimeµSeconds - this.PreviousFrameTime) / 10e6;

			this.DeltaTime = (float)frameTimeSeconds;
			this.PreviousFrameTime = frameTimeµSeconds;

			return true;
		}

		/// <inheritdoc />
		protected override GLContext OnCreateContext()
		{
			var gdkGLContext = this.Window.CreateGlContext();

			GetRequiredVersion(out var major, out var minor);
			gdkGLContext.SetRequiredVersion(major, minor);

			gdkGLContext.DebugEnabled = this.ContextFlags.HasFlag(GraphicsContextFlags.Debug);
			gdkGLContext.ForwardCompatible = this.ContextFlags.HasFlag(GraphicsContextFlags.ForwardCompatible);

			gdkGLContext.Realize();
			return gdkGLContext;
		}

		/// <inheritdoc />
		public override void Destroy()
		{
			GC.SuppressFinalize(this);
			Dispose(true);

			base.Destroy();
		}

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (!disposing)
			{
				return;
			}

			MakeCurrent();
			OnShuttingDown();
			if (!GraphicsContext.ShareContexts || (Interlocked.Decrement(ref GraphicsContextCount) != 0))
			{
				return;
			}

			OnGraphicsContextShuttingDown();
			IsSharedContextInitialized = false;
		}

		/// <summary>
		/// Called when the first <see cref="GraphicsContext"/> is created in the case where
		/// GraphicsContext.ShareContexts == true;
		/// </summary>
		public static event EventHandler GraphicsContextInitialized;

		/// <summary>
		/// Called when the first <see cref="GraphicsContext"/> is being destroyed in the case where
		/// GraphicsContext.ShareContext == true;
		/// </summary>
		public static event EventHandler GraphicsContextShuttingDown;

		/// <summary>
		/// Called when this <see cref="ViewportArea"/> has finished initializing and has a valid
		/// <see cref="GraphicsContext"/>.
		/// </summary>
		public event EventHandler Initialized;

		/// <summary>
		/// Called when this <see cref="ViewportArea"/> is being disposed.
		/// </summary>
		public event EventHandler ShuttingDown;

		/// <summary>
		/// Invokes the <see cref="GraphicsContextInitialized"/> event.
		/// </summary>
		private static void OnGraphicsContextInitialized()
		{
			GraphicsContextInitialized?.Invoke(null, EventArgs.Empty);
		}

		/// <summary>
		/// Invokes the <see cref="GraphicsContextShuttingDown"/> event.
		/// </summary>
		private static void OnGraphicsContextShuttingDown()
		{
			GraphicsContextShuttingDown?.Invoke(null, EventArgs.Empty);
		}

		/// <summary>
		/// Invokes the <see cref="Initialized"/> event.
		/// </summary>
		protected virtual void OnInitialized()
		{
			this.Initialized?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Invokes the <see cref="ShuttingDown"/> event.
		/// </summary>
		protected virtual void OnShuttingDown()
		{
			this.ShuttingDown?.Invoke(this, EventArgs.Empty);
		}

		/// <inheritdoc />
		protected override bool OnDrawn(Cairo.Context cr)
		{
			if (!this.IsInitialized)
			{
				Initialize();
			}

			var result = base.OnDrawn(cr);
			return result;
		}

		/// <summary>
		/// Initializes the <see cref="ViewportArea"/> with its given values and creates a <see cref="GraphicsContext"/>.
		/// </summary>
		private void Initialize()
		{
			this.IsInitialized = true;

			// Make the GDK GL context current
			MakeCurrent();

			// Create a dummy context that will grab the GdkGLContext that is current on the thread
			this.TKGraphicsContext = new GraphicsContext(ContextHandle.Zero, null);

			if (this.ContextFlags.HasFlag(GraphicsContextFlags.Debug))
			{
				this.TKGraphicsContext.ErrorChecking = true;
			}

			if (GraphicsContext.ShareContexts)
			{
				Interlocked.Increment(ref GraphicsContextCount);

				if (!IsSharedContextInitialized)
				{
					IsSharedContextInitialized = true;
					((IGraphicsContextInternal)this.TKGraphicsContext).LoadAll();
					OnGraphicsContextInitialized();
				}
			}
			else
			{
				((IGraphicsContextInternal)this.TKGraphicsContext).LoadAll();
				OnGraphicsContextInitialized();
			}

			OnInitialized();
		}
	}
}
