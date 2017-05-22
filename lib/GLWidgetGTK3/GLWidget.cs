#region License
//
//  GLWidget.cs
//
//  The Open Toolkit Library License
//
//  Copyright (c) 2006 - 2009 the Open Toolkit library, except where noted.
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights to
//  use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
//  the Software, and to permit persons to whom the Software is furnished to do
//  so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
//  OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
//  HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
//  WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
//  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
//  OTHER DEALINGS IN THE SOFTWARE.
//
#endregion

using System;
using System.Threading;
using System.ComponentModel;
using OpenTK.Graphics;
using OpenTK.Platform;

using Gtk;
using log4net;
using OpenTK.Graphics.OpenGL;
using OpenTK.OSX;
using OpenTK.Win;
using OpenTK.X11;

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global
// ReSharper disable EventNeverSubscribedTo.Global

namespace OpenTK
{
	/// <summary>
	/// The <see cref="GLWidget"/> is a GTK widget for which an OpenGL context can be used to draw arbitrary graphics.
	/// </summary>
	[ToolboxItem(true)]
	public class GLWidget: DrawingArea
	{
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
		/// Called when this <see cref="GLWidget"/> has finished initializing and has a valid
		/// <see cref="GraphicsContext"/>.
		/// </summary>
		public event EventHandler Initialized;

		/// <summary>
		/// Called when this <see cref="GLWidget"/> needs to render a frame.
		/// </summary>
		public event EventHandler RenderFrame;

		/// <summary>
		/// Called when this <see cref="GLWidget"/> is being disposed.
		/// </summary>
		public event EventHandler ShuttingDown;

		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(GLWidget));

		private static int GraphicsContextCount;
		private static bool IsSharedContextInitialized;

		private IGraphicsContext GraphicsContext;
		private IWindowInfo WindowInfo;
		private bool IsInitialized;

		/// <summary>
		/// Use a single buffer versus a double buffer.
		/// </summary>
		[Browsable(true)]
		public bool SingleBuffer { get; set; }

		/// <summary>
		/// Color Buffer Bits-Per-Pixel
		/// </summary>
		public int ColorBPP { get; set; }

		/// <summary>
		/// Accumulation Buffer Bits-Per-Pixel
		/// </summary>
		public int AccumulatorBPP { get; set; }

		/// <summary>
		/// Depth Buffer Bits-Per-Pixel
		/// </summary>
		public int DepthBPP { get; set; }

		/// <summary>
		/// Stencil Buffer Bits-Per-Pixel
		/// </summary>
		public int StencilBPP { get; set; }

		/// <summary>
		/// Number of samples
		/// </summary>
		public int Samples { get; set; }

		/// <summary>
		/// Indicates if steropic renderering is enabled
		/// </summary>
		public bool Stereo { get; set; }

		/// <summary>
		/// The major version of OpenGL to use.
		/// </summary>
		public int GLVersionMajor { get; set; }

		/// <summary>
		/// The minor version of OpenGL to use.
		/// </summary>
		public int GLVersionMinor { get; set; }

		/// <summary>
		/// The set <see cref="GraphicsContextFlags"/> for this widget.
		/// </summary>
		public GraphicsContextFlags GraphicsContextFlags { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="GLWidget"/> class.
		/// </summary>
		public GLWidget()
			: this(GraphicsMode.Default)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="GLWidget"/> class.
		/// </summary>
		/// <param name="graphicsMode">The <see cref="GraphicsMode"/> which the widget should be constructed with.</param>
		/// <param name="glVersionMajor">The major OpenGL version to attempt to initialize.</param>
		/// <param name="glVersionMinor">The minor OpenGL version to attempt to initialize.</param>
		/// <param name="graphicsContextFlags">
		/// Any flags which should be used during initialization of the <see cref="GraphicsContext"/>.
		/// </param>
		public GLWidget(GraphicsMode graphicsMode, 
			int glVersionMajor = 1, 
			int glVersionMinor = 0, 
			GraphicsContextFlags graphicsContextFlags = GraphicsContextFlags.Default)
		{
			this.DoubleBuffered = false;

			this.SingleBuffer = graphicsMode.Buffers == 1;
			this.ColorBPP = graphicsMode.ColorFormat.BitsPerPixel;
			this.AccumulatorBPP = graphicsMode.AccumulatorFormat.BitsPerPixel;
			this.DepthBPP = graphicsMode.Depth;
			this.StencilBPP = graphicsMode.Stencil;
			this.Samples = graphicsMode.Samples;
			this.Stereo = graphicsMode.Stereo;

			this.GLVersionMajor = glVersionMajor;
			this.GLVersionMinor = glVersionMinor;
			this.GraphicsContextFlags = graphicsContextFlags;
		}

		/// <summary>
		/// Destructs this object.
		/// </summary>
		~GLWidget()
		{
			Dispose(false);
		}

		/// <summary>
		/// Initializes the <see cref="GLWidget"/> with its given values and creates a <see cref="GraphicsContext"/>.
		/// </summary>
		private void Initialize()
		{
			this.IsInitialized = true;

			// If this looks uninitialized...  initialize.
			if (this.ColorBPP == 0)
			{
				this.ColorBPP = 32;

				if (this.DepthBPP == 0)
				{
					this.DepthBPP = 16;
				}
			}

			ColorFormat colorBufferColorFormat = new ColorFormat(this.ColorBPP);

			ColorFormat accumulationColorFormat = new ColorFormat(this.AccumulatorBPP);

			int buffers = 2;
			if (this.SingleBuffer)
			{
				buffers--;
			}

			GraphicsMode graphicsMode = new GraphicsMode(colorBufferColorFormat, this.DepthBPP, this.StencilBPP, this.Samples, accumulationColorFormat, buffers, this.Stereo);

			if (Configuration.RunningOnWindows)
			{
				Log.Info("OpenTK running on windows");
			}
			else if (Configuration.RunningOnMacOS)
			{
				Log.Info("OpenTK running on OSX");
			}
			else
			{
				Log.Info("OpenTK running on X11");
			}

			// IWindowInfo
			if (Configuration.RunningOnWindows)
			{
				this.WindowInfo = WinWindowsInfoInitializer.Initialize(this.Window.Handle);
			}
			else if (Configuration.RunningOnMacOS)
			{
				this.WindowInfo = OSXWindowInfoInitializer.Initialize(this.Window.Handle);
			}
			else
			{
				this.WindowInfo = XWindowInfoInitializer.Initialize(graphicsMode, this.Display.Handle, this.Screen.Number, this.Window.Handle, this.RootWindow.Handle);
			}

			// GraphicsContext
			try
			{
				this.GraphicsContext = new GraphicsContext(graphicsMode, this.WindowInfo, this.GLVersionMajor, this.GLVersionMinor, this.GraphicsContextFlags);
			}
			catch (GraphicsException gex)
			{
				Log.Warn("Failed to create a graphics context for the requested version and flag combination.", gex);

				GraphicsContext dummyContext = new GraphicsContext
				(
					graphicsMode, 
					this.WindowInfo,
					1,
					0,
					GraphicsContextFlags.Default
				);
				dummyContext.MakeCurrent(this.WindowInfo);
				dummyContext.LoadAll();

				string version = GL.GetString(StringName.Version);
				string glslVersion = GL.GetString(StringName.ShadingLanguageVersion);
				string renderer = GL.GetString(StringName.Renderer);
				string vendor = GL.GetString(StringName.Vendor);
				Log.Error
				(
					"Please note that Everlook requires at least OpenGL 3.3.\n" +
	                "A lesser context could be created with the following information: \n" +
					$"Version: {version}\n" +
					$"GLSL Version: {glslVersion}\n" +
					$"Renderer: {renderer}\n" +
					$"Vendor: {vendor}\n" +
					$"Flags: {GraphicsContextFlags.Default}"
				);

				throw;
			}

			this.GraphicsContext.MakeCurrent(this.WindowInfo);

			if (Graphics.GraphicsContext.ShareContexts)
			{
				Interlocked.Increment(ref GraphicsContextCount);

				if (!IsSharedContextInitialized)
				{
					IsSharedContextInitialized = true;
					((IGraphicsContextInternal) this.GraphicsContext).LoadAll();
					OnGraphicsContextInitialized();
				}
			}
			else
			{
				((IGraphicsContextInternal) this.GraphicsContext).LoadAll();
				OnGraphicsContextInitialized();
			}

			OnInitialized();
		}

		/// <summary>
		/// Called when the widget needs to be (fully or partially) redrawn.
		/// </summary>
		/// <param name="cr"></param>
		/// <returns></returns>
		protected override bool OnDrawn(Cairo.Context cr)
		{
			if (!this.IsInitialized)
			{
				Initialize();
			}
			else
			{
				this.GraphicsContext.MakeCurrent(this.WindowInfo);
			}

			bool result = base.OnDrawn(cr);
			OnRenderFrame();

			this.GraphicsContext.SwapBuffers();

			return result;
		}

		/// <summary>
		/// Called whenever the widget is resized.
		/// </summary>
		/// <param name="evnt"></param>
		/// <returns></returns>
		protected override bool OnConfigureEvent(Gdk.EventConfigure evnt)
		{
			bool result = base.OnConfigureEvent(evnt);

			this.GraphicsContext?.Update(this.WindowInfo);

			return result;
		}

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
		/// Invokes the <see cref="RenderFrame"/> event.
		/// </summary>
		protected virtual void OnRenderFrame()
		{
			this.RenderFrame?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Invokes the <see cref="ShuttingDown"/> event.
		/// </summary>
		protected virtual void OnShuttingDown()
		{
			this.ShuttingDown?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Destroys this <see cref="Widget"/>, disposing it and destroying it in the context of GTK.
		/// </summary>
		public override void Destroy()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
			base.Destroy();
		}

		/// <summary>
		/// Disposes the current object, releasing any native resources it was using.
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (!disposing)
			{
				return;
			}

			this.GraphicsContext.MakeCurrent(this.WindowInfo);
			OnShuttingDown();
			if (Graphics.GraphicsContext.ShareContexts && (Interlocked.Decrement(ref GraphicsContextCount) == 0))
			{
				OnGraphicsContextShuttingDown();
				IsSharedContextInitialized = false;
			}
			this.GraphicsContext.Dispose();
		}
	}
}