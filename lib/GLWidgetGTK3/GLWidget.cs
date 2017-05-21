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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.ComponentModel;
using OpenTK.Graphics;
using OpenTK.Platform;

using Gtk;
using log4net;
using OpenTK.Graphics.OpenGL;
using OpenTK.X11;

namespace OpenTK
{
	[ToolboxItem(true)]
	public class GLWidget: DrawingArea
	{
		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(GLWidget));

		#region Static attrs.

		static int _GraphicsContextCount;
		static bool _SharedContextInitialized;

		#endregion

		#region Attributes

		IGraphicsContext _GraphicsContext;
		IWindowInfo _WindowInfo;
		bool _Initialized;

		#endregion

		#region Properties
		/// <summary>Use a single buffer versus a double buffer.</summary>
		[Browsable(true)]
		public bool SingleBuffer { get; set; }

		/// <summary>Color Buffer Bits-Per-Pixel</summary>
		public int ColorBPP { get; set; }

		/// <summary>Accumulation Buffer Bits-Per-Pixel</summary>
		public int AccumulatorBPP { get; set; }

		/// <summary>Depth Buffer Bits-Per-Pixel</summary>
		public int DepthBPP { get; set; }

		/// <summary>Stencil Buffer Bits-Per-Pixel</summary>
		public int StencilBPP { get; set; }

		/// <summary>Number of samples</summary>
		public int Samples { get; set; }

		/// <summary>Indicates if steropic renderering is enabled</summary>
		public bool Stereo { get; set; }

		/// <summary>The major version of OpenGL to use.</summary>
		public int GLVersionMajor { get; set; }

		/// <summary>The minor version of OpenGL to use.</summary>
		public int GLVersionMinor { get; set; }

		public GraphicsContextFlags GraphicsContextFlags
		{
			get;
			set;
		}

		#endregion

		#region Construction/Destruction

		/// <summary>Constructs a new GLWidget.</summary>
		public GLWidget()
			: this(GraphicsMode.Default)
		{
		}

		/// <summary>Constructs a new GLWidget</summary>
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

		~GLWidget()
		{
			Dispose(false);
		}

		public override void Destroy()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
			base.Destroy();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				this._GraphicsContext.MakeCurrent(this._WindowInfo);
				OnShuttingDown();
				if (GraphicsContext.ShareContexts && (Interlocked.Decrement(ref _GraphicsContextCount) == 0))
				{
					OnGraphicsContextShuttingDown();
					_SharedContextInitialized = false;
				}
				this._GraphicsContext.Dispose();
			}
		}

		#endregion

		#region New Events

		// Called when the first GraphicsContext is created in the case of GraphicsContext.ShareContexts == True;
		public static event EventHandler GraphicsContextInitialized;

		private static void OnGraphicsContextInitialized()
		{
			GraphicsContextInitialized?.Invoke(null, EventArgs.Empty);
		}

		// Called when the first GraphicsContext is being destroyed in the case of GraphicsContext.ShareContexts == True;
		public static event EventHandler GraphicsContextShuttingDown;

		private static void OnGraphicsContextShuttingDown()
		{
			GraphicsContextShuttingDown?.Invoke(null, EventArgs.Empty);
		}

		// Called when this GLWidget has a valid GraphicsContext
		public event EventHandler Initialized;

		protected virtual void OnInitialized()
		{
			this.Initialized?.Invoke(this, EventArgs.Empty);
		}

		// Called when this GLWidget needs to render a frame
		public event EventHandler RenderFrame;

		protected virtual void OnRenderFrame()
		{
			this.RenderFrame?.Invoke(this, EventArgs.Empty);
		}

		// Called when this GLWidget is being Disposed
		public event EventHandler ShuttingDown;

		protected virtual void OnShuttingDown()
		{
			this.ShuttingDown?.Invoke(this, EventArgs.Empty);
		}

		#endregion

		// Called when a widget is realized. (window handles and such are valid)
		// protected override void OnRealized() { base.OnRealized(); }

		// Called when the widget needs to be (fully or partially) redrawn.
		protected override bool OnDrawn(Cairo.Context cr)
        {
			if (!this._Initialized)
			{
				Initialize();
			}
			else
			{
				this._GraphicsContext.MakeCurrent(this._WindowInfo);
			}

	        bool result = base.OnDrawn(cr);
			OnRenderFrame();

	        this._GraphicsContext.SwapBuffers();

			return result;
		}

		// Called on Resize
		protected override bool OnConfigureEvent(Gdk.EventConfigure evnt)
		{
			bool result = base.OnConfigureEvent(evnt);

			this._GraphicsContext?.Update(this._WindowInfo);

			return result;
		}

		private void Initialize()
		{
			this._Initialized = true;

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
				Console.WriteLine("OpenTK running on windows");
			}
			else if (Configuration.RunningOnMacOS)
			{
				Console.WriteLine("OpenTK running on OSX");
			}
			else
			{
				Console.WriteLine("OpenTK running on X11");
			}

			// IWindowInfo
			if (Configuration.RunningOnWindows)
			{
				this._WindowInfo = InitializeWindows();
			}
			else if (Configuration.RunningOnMacOS)
			{
				this._WindowInfo = InitializeOSX();
			}
			else
			{
				this._WindowInfo = InitializeX(graphicsMode);
			}

			// GraphicsContext
			try
			{
				this._GraphicsContext = new GraphicsContext(graphicsMode, this._WindowInfo, this.GLVersionMajor, this.GLVersionMinor, this.GraphicsContextFlags);
			}
			catch (GraphicsException gex)
			{
				GraphicsContext dummyContext = new GraphicsContext
				(
					graphicsMode, 
					this._WindowInfo,
					1,
					0,
					GraphicsContextFlags.Default
				);
				dummyContext.MakeCurrent(this._WindowInfo);
				dummyContext.LoadAll();

				string version = GL.GetString(StringName.Version);
				string glslVersion = GL.GetString(StringName.ShadingLanguageVersion);
				string renderer = GL.GetString(StringName.Renderer);
				string vendor = GL.GetString(StringName.Vendor);
				Log.Error($"Failed to create a graphics context for the requested version and flag combination. \n" +
				          $"Please note that Everlook requires at least OpenGL 3.3.\n" +
				          $"A lesser context could be created with the following information: \n" +
				          $"Version: {version}\n" +
				          $"GLSL Version: {glslVersion}\n" +
				          $"Renderer: {renderer}\n" +
				          $"Vendor: {vendor}\n" +
				          $"Flags: {GraphicsContextFlags.Default}");

				throw;
			}

			this._GraphicsContext.MakeCurrent(this._WindowInfo);

			if (GraphicsContext.ShareContexts)
			{
				Interlocked.Increment(ref _GraphicsContextCount);

				if (!_SharedContextInitialized)
				{
					_SharedContextInitialized = true;
					((IGraphicsContextInternal) this._GraphicsContext).LoadAll();
					OnGraphicsContextInitialized();
				}
			}
			else
			{
				((IGraphicsContextInternal) this._GraphicsContext).LoadAll();
				OnGraphicsContextInitialized();
			}

			OnInitialized();
		}

		#region Windows Specific initalization

		private IWindowInfo InitializeWindows()
		{
			IntPtr windowHandle = gdk_win32_window_get_handle(this.Window.Handle);
			return Utilities.CreateWindowsWindowInfo(windowHandle);
		}

		[SuppressUnmanagedCodeSecurity, DllImport("libgdk-3-0.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr gdk_win32_window_get_handle(IntPtr w);

		#endregion

		#region OSX Specific Initialization

		private IWindowInfo InitializeOSX()
		{
			IntPtr windowHandle = gdk_quartz_window_get_nswindow(this.Window.Handle);
			IntPtr viewHandle = gdk_quartz_window_get_nsview(this.Window.Handle);

			return Utilities.CreateMacOSWindowInfo(windowHandle, viewHandle);
		}

		[SuppressUnmanagedCodeSecurity, DllImport("libgtk-3.dylib")]
		private static extern IntPtr gdk_quartz_window_get_nswindow(IntPtr handle);

		[SuppressUnmanagedCodeSecurity, DllImport("libgtk-3.dylib")]
		private static extern IntPtr gdk_quartz_window_get_nsview(IntPtr handle);

		#endregion

		#region X Specific Initialization

		private const string UnixLibGdkName = "libgdk-3.so.0";
		private const string UnixLibX11Name = "libX11.so.6";
		private const string UnixLibGLName = "libGL.so.1";

		private IWindowInfo InitializeX(GraphicsMode mode)
		{
			IntPtr display = gdk_x11_display_get_xdisplay(this.Display.Handle);
			int screen = this.Screen.Number;

			IntPtr windowHandle = gdk_x11_window_get_xid(this.Window.Handle);
			IntPtr rootWindow = gdk_x11_window_get_xid(this.RootWindow.Handle);

			IntPtr visualInfo;
			if (mode.Index.HasValue)
			{
				XVisualInfo info = new XVisualInfo
				{
					VisualID = mode.Index.Value
				};

				int dummy;
				visualInfo = XGetVisualInfo(display, XVisualInfoMask.ID, ref info, out dummy);
			}
			else
			{
				visualInfo = GetVisualInfo(display);
			}

			IWindowInfo retval = Utilities.CreateX11WindowInfo(display, screen, windowHandle, rootWindow, visualInfo);
			XFree(visualInfo);

			return retval;
		}

		private static IntPtr XGetVisualInfo(IntPtr display, XVisualInfoMask vinfo_mask, ref XVisualInfo template, out int nitems)
		{
			return XGetVisualInfoInternal(display, (IntPtr)(int)vinfo_mask, ref template, out nitems);
		}

		private IntPtr GetVisualInfo(IntPtr display)
		{
			try
			{
				int[] attributes = this.AttributeList.ToArray();
				return glXChooseVisual(display, this.Screen.Number, attributes);
			}
			catch (DllNotFoundException e)
			{
				throw new DllNotFoundException("OpenGL dll not found!", e);
			}
			catch (EntryPointNotFoundException enf)
			{
				throw new EntryPointNotFoundException("Glx entry point not found!", enf);
			}
		}

		private List<int> AttributeList
		{
			get
			{
				List<int> attributeList = new List<int>(24);

				attributeList.Add((int)GLXAttribute.RGBA);

				if (!this.SingleBuffer)
				{
					attributeList.Add((int)GLXAttribute.DoubleBuffer);
				}

				if (this.Stereo)
				{
					attributeList.Add((int)GLXAttribute.Stereo);
				}

				attributeList.Add((int)GLXAttribute.RedSize);
				attributeList.Add(this.ColorBPP / 4); // TODO support 16-bit

				attributeList.Add((int)GLXAttribute.GreenSize);
				attributeList.Add(this.ColorBPP / 4); // TODO support 16-bit

				attributeList.Add((int)GLXAttribute.BlueSize);
				attributeList.Add(this.ColorBPP / 4); // TODO support 16-bit

				attributeList.Add((int)GLXAttribute.AlphaSize);
				attributeList.Add(this.ColorBPP / 4); // TODO support 16-bit

				attributeList.Add((int)GLXAttribute.DepthSize);
				attributeList.Add(this.DepthBPP);

				attributeList.Add((int)GLXAttribute.StencilSize);
				attributeList.Add(this.StencilBPP);

				//attributeList.Add(GLX_AUX_BUFFERS);
				//attributeList.Add(Buffers);

				attributeList.Add((int)GLXAttribute.AccumRedSize);
				attributeList.Add(this.AccumulatorBPP / 4);// TODO support 16-bit

				attributeList.Add((int)GLXAttribute.AccumGreenSize);
				attributeList.Add(this.AccumulatorBPP / 4);// TODO support 16-bit

				attributeList.Add((int)GLXAttribute.AccumBlueSize);
				attributeList.Add(this.AccumulatorBPP / 4);// TODO support 16-bit

				attributeList.Add((int)GLXAttribute.AccumAlphaSize);
				attributeList.Add(this.AccumulatorBPP / 4);// TODO support 16-bit

				attributeList.Add((int)GLXAttribute.None);

				return attributeList;
			}
		}

		[DllImport(UnixLibX11Name, EntryPoint = "XGetVisualInfo")]
		private static extern IntPtr XGetVisualInfoInternal(IntPtr display, IntPtr vinfo_mask, ref XVisualInfo template, out int nitems);

		[SuppressUnmanagedCodeSecurity, DllImport(UnixLibX11Name)]
		private static extern void XFree(IntPtr handle);

		/// <summary> Returns the X resource (window or pixmap) belonging to a GdkDrawable. </summary>
		/// <remarks> XID gdk_x11_drawable_get_xid(GdkDrawable *drawable); </remarks>
		/// <param name="gdkDisplay"> The GdkDrawable. </param>
		/// <returns> The ID of drawable's X resource. </returns>
		[SuppressUnmanagedCodeSecurity, DllImport(UnixLibGdkName)]
		private static extern IntPtr gdk_x11_drawable_get_xid(IntPtr gdkDisplay);

		/// <summary> Returns the X resource (window or pixmap) belonging to a GdkDrawable. </summary>
		/// <remarks> XID gdk_x11_drawable_get_xid(GdkDrawable *drawable); </remarks>
		/// <param name="gdkDisplay"> The GdkDrawable. </param>
		/// <returns> The ID of drawable's X resource. </returns>
		[SuppressUnmanagedCodeSecurity, DllImport(UnixLibGdkName)]
		private static extern IntPtr gdk_x11_window_get_xid(IntPtr gdkDisplay);

		/// <summary> Returns the X display of a GdkDisplay. </summary>
		/// <remarks> Display* gdk_x11_display_get_xdisplay(GdkDisplay *display); </remarks>
		/// <param name="gdkDisplay"> The GdkDrawable. </param>
		/// <returns> The X Display of the GdkDisplay. </returns>
		[SuppressUnmanagedCodeSecurity, DllImport(UnixLibGdkName)]
		private static extern IntPtr gdk_x11_display_get_xdisplay(IntPtr gdkDisplay);

		[SuppressUnmanagedCodeSecurity, DllImport(UnixLibGLName)]
		private static extern IntPtr glXChooseVisual(IntPtr display, int screen, int[] attr);

		#endregion

	}
}