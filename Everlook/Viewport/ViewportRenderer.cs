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
using Gdk;
using OpenGL;
using System.Threading;
using System.Diagnostics;

namespace Everlook.Viewport
{
	/// <summary>
	/// Viewport renderer for the main Everlook UI.
	/// </summary>
	public class ViewportRenderer
	{
		/// <summary>
		/// Occurs when a frame has been rendered.
		/// </summary>
		public event FrameRenderedEventHandler FrameRendered;

		/// <summary>
		/// The frame rendered arguments. Contains the frame as a pixel buffer, as well as the frame delta.
		/// </summary>
		public readonly FrameRendererEventArgs FrameRenderedArgs = new FrameRendererEventArgs();

		private readonly Thread RenderThread;
		private bool bShouldRender;

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Viewport.ViewportRenderer"/> class.
		/// </summary>
		public ViewportRenderer()
		{			
			this.RenderThread = new Thread(RenderLoop);
			this.bShouldRender = false;
		}

		/// <summary>
		/// Gets a value indicating whether this instance is actively rendering frames for the viewport.
		/// </summary>
		/// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
		public bool IsActive
		{
			get
			{
				return bShouldRender;
			}
		}

		/// <summary>
		/// Starts the rendering thread in the background.
		/// </summary>
		public void Start()
		{
			if (!RenderThread.IsAlive)
			{
				this.bShouldRender = true;
				this.RenderThread.Start();
			}
			else
			{
				throw new ThreadStateException("The rendering thread has already been started.");
			}
		}

		/// <summary>
		/// Stops the rendering thread, allowing it to finish the current frame.
		/// </summary>
		public void Stop()
		{
			if (RenderThread.IsAlive)
			{
				this.bShouldRender = false;
			}
			else
			{
				throw new ThreadStateException("The rendering thread has not been started.");
			}
		}

		/// <summary>
		/// The primary rendering loop. Here, the current object is rendered using OpenGL and gets
		/// passed to the listeners via the FrameRendered event.
		/// </summary>
		private void RenderLoop()
		{			
			long previousFrameDelta = 0;
			while (bShouldRender)
			{
				Stopwatch sw = new Stopwatch();

				sw.Start();
				FrameRenderedArgs.Frame = RenderFrame(previousFrameDelta);
				sw.Stop();

				FrameRenderedArgs.FrameDelta = sw.ElapsedMilliseconds;
				previousFrameDelta = sw.ElapsedMilliseconds;
				OnFrameRendered();
			}
		}

		private Pixbuf RenderFrame(long FrameDelta)
		{			
			return null;
		}

		/// <summary>
		/// Raises the frame rendered event.
		/// </summary>
		protected void OnFrameRendered()
		{
			if (FrameRendered != null)
			{
				FrameRendered(this, FrameRenderedArgs);
			}
		}
	}

	/// <summary>
	/// Frame rendered event handler.
	/// </summary>
	public delegate void FrameRenderedEventHandler(object sender,FrameRendererEventArgs e);

	/// <summary>
	/// Frame renderer event arguments.
	/// </summary>
	public class FrameRendererEventArgs : EventArgs
	{
		/// <summary>
		/// The pixel buffer containing the current frame data.
		/// </summary>
		/// <value>The frame.</value>
		public Pixbuf Frame
		{
			get;
			set;
		}

		/// <summary>
		/// The frame delta; i.e, the time taken to render the frame.
		/// </summary>
		/// <value>The frame delta.</value>
		public long FrameDelta
		{
			get;
			set;
		}
	}
}

