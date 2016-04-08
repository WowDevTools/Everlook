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
	public class ViewportRenderer
	{
		public event FrameRenderedEventHandler FrameRendered;

		public readonly FrameRendererEventArgs FrameRenderedArgs = new FrameRendererEventArgs();

		private readonly Thread RenderThread;
		private bool bShouldRender;

		public ViewportRenderer()
		{			
			this.RenderThread = new Thread(RenderLoop);
			this.bShouldRender = false;
		}

		public bool IsActive
		{
			get
			{
				return bShouldRender;
			}
		}

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

		protected void OnFrameRendered()
		{
			if (FrameRendered != null)
			{
				FrameRendered(this, FrameRenderedArgs);
			}
		}
	}

	public delegate void FrameRenderedEventHandler(object sender,FrameRendererEventArgs e);

	public class FrameRendererEventArgs : EventArgs
	{
		public Pixbuf Frame
		{
			get;
			set;
		}

		public long FrameDelta
		{
			get;
			set;
		}
	}
}

