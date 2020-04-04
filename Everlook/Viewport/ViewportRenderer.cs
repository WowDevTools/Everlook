//
//  ViewportRenderer.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Everlook.Configuration;
using Everlook.UI.Widgets;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using Gdk;
using log4net;
using Silk.NET.OpenGL;

namespace Everlook.Viewport
{
    /// <summary>
    /// Viewport renderer for the main Everlook UI. This class manages an OpenGL rendering thread, which
    /// uses rendering built into the different renderable objects.
    /// </summary>
    public sealed class ViewportRenderer : GraphicsObject, IDisposable
    {
        /// <summary>
        /// Logger instance for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(ViewportRenderer));

        /// <summary>
        /// The viewport widget displayed to the user in the main interface.
        /// Used to get proper dimensions for the OpenGL viewport.
        /// </summary>
        private readonly ViewportArea _viewportWidget;

        /// <summary>
        /// Holds the rendering cache.
        /// </summary>
        private readonly RenderCache _renderCache;

        /*
            RenderTarget and related control flow data.
        */

        /// <summary>
        /// A lock object used to enforce that the rendering target can finish its current
        /// frame before a new one is assigned.
        /// </summary>
        private readonly object _renderTargetLock = new object();

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="ViewportRenderer"/> has been disposed.
        /// </summary>
        private bool IsDisposed { get; set; }

        /// <summary>
        /// Gets a value indicating whether or not the renderer currently has an object to render.
        /// </summary>
        public bool HasRenderTarget => this.RenderTarget != null;

        /// <summary>
        /// Gets the current rendering target. This is an object capable of being shown in an
        /// OpenGL viewport.
        /// </summary>
        public IRenderable? RenderTarget { get; private set; }

        /// <summary>
        /// The camera viewpoint of the observer.
        /// </summary>
        private readonly ViewportCamera _camera;

        /// <summary>
        /// The movement component for the camera.
        /// </summary>
        private readonly CameraMovement _movement;

        /// <summary>
        /// Gets the time taken to render the last frame (in seconds).
        /// </summary>
        private float DeltaTime => (float)_viewportWidget.DeltaTime;

        /// <summary>
        /// Gets or sets a value indicating whether or not the viewer wants to move in the world.
        /// </summary>
        public bool WantsToMove { get; set; }

        /// <summary>
        /// Gets or sets the X position of the mouse during the last frame, relative to the <see cref="_viewportWidget"/>.
        /// </summary>
        public double InitialMouseX { get; set; }

        /// <summary>
        /// Gets or sets the Y position of the mouse during the last frame, relative to the <see cref="_viewportWidget"/>.
        /// </summary>
        public double InitialMouseY { get; set; }

        /*
            Runtime transitional OpenGL data.
        */

        /// <summary>
        /// The OpenGL ID of the vertex array valid for the current context.
        /// </summary>
        private uint _vertexArrayID;

        /// <summary>
        /// Gets a value indicating whether or not this instance has been initialized and is ready
        /// to render objects.
        /// </summary>
        public bool IsInitialized
        {
            get;
            private set;
        }

        /*
            Everlook caching and static data accessors.
        */

        /// <summary>
        /// Static reference to the configuration handler.
        /// </summary>
        private readonly EverlookConfiguration _configuration = EverlookConfiguration.Instance;

        /// <summary>
        /// The base grid, rendered underneath models.
        /// </summary>
        private BaseGrid? _grid;

        /// <summary>
        /// Initializes a new instance of the <see cref="Everlook.Viewport.ViewportRenderer"/> class.
        /// </summary>
        /// <param name="viewportWidget">The widget which the viewport should be rendered to.</param>
        public ViewportRenderer(ViewportArea viewportWidget)
            : base(viewportWidget.GetAPI())
        {
            _viewportWidget = viewportWidget;
            _camera = new ViewportCamera();
            _movement = new CameraMovement(_camera);
            _renderCache = new RenderCache(this.GL);

            this.IsInitialized = false;
        }

        /// <summary>
        /// Initializes the viewport renderer.
        /// </summary>
        public void Initialize()
        {
            ThrowIfDisposed();

            Log.Info($"Initializing {nameof(ViewportRenderer)} and setting up default OpenGL state...");

            var numExtensions = this.GL.GetInteger(GetPName.NumExtensions);
            var extensions = new List<string>();
            for (var i = 0u; i < numExtensions; ++i)
            {
                extensions.Add(this.GL.GetString(StringName.Extensions, i));
            }

            if (extensions.Contains("GL_KHR_debug"))
            {
                this.GL.Enable(EnableCap.DebugOutputSynchronous);

                unsafe
                {
                    this.GL.DebugMessageCallback(OnGLDebugMessage, (void*)0);
                }
            }

            // Generate the vertex array
            unsafe
            {
                fixed (uint* ptr = &_vertexArrayID)
                {
                    this.GL.GenVertexArrays(1, ptr);
                }
            }

            this.GL.BindVertexArray(_vertexArrayID);

            // GL.Disable(EnableCap.AlphaTest);

            // Make sure we use the depth buffer when drawing
            this.GL.Enable(EnableCap.DepthTest);
            this.GL.DepthFunc(DepthFunction.Lequal);
            this.GL.DepthMask(true);

            // Enable backface culling for performance reasons
            this.GL.Enable(EnableCap.CullFace);

            // Set a simple default blending function
            this.GL.Enable(EnableCap.Blend);
            this.GL.BlendEquation(BlendEquationModeEXT.FuncAdd);
            this.GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // Initialize the viewport
            var widgetWidth = (uint)_viewportWidget.AllocatedWidth;
            var widgetHeight = (uint)_viewportWidget.AllocatedHeight;

            this.GL.Viewport(0, 0, widgetWidth, widgetHeight);
            this.GL.ClearColor
            (
                (float)_configuration.ViewportBackgroundColour.Red,
                (float)_configuration.ViewportBackgroundColour.Green,
                (float)_configuration.ViewportBackgroundColour.Blue,
                (float)_configuration.ViewportBackgroundColour.Alpha
            );

            this.GL.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            _grid = new BaseGrid(this.GL, _renderCache);
            _grid.Initialize();

            this.IsInitialized = true;
        }

        /// <summary>
        /// Sets the clear colour of the OpenGL viewport.
        /// </summary>
        /// <param name="colour">The new colour to use.</param>
        public void SetClearColour(RGBA colour)
        {
            this.GL.ClearColor
            (
                (float)colour.Red,
                (float)colour.Green,
                (float)colour.Blue,
                (float)colour.Alpha
            );
        }

        private static void OnGLDebugMessage
        (
            GLEnum source,
            GLEnum type,
            int id,
            GLEnum severity,
            int length,
            IntPtr message,
            IntPtr userparam
        )
        {
            var messageContent = Marshal.PtrToStringAuto(message, length);

            Log.Debug
            (
                $"An OpenGL debug message has been received from \"{source}\" of type \"{type}\".\n{messageContent}"
            );
        }

        /// <summary>
        /// The primary rendering logic. Here, the current object is rendered using OpenGL.
        /// </summary>
        public void RenderFrame()
        {
            ThrowIfDisposed();

            lock (_renderTargetLock)
            {
                // Make sure the viewport is accurate for the current widget size on screen
                var widgetWidth = (uint)_viewportWidget.AllocatedWidth;
                var widgetHeight = (uint)_viewportWidget.AllocatedHeight;

                this.GL.Viewport(0, 0, widgetWidth, widgetHeight);
                this.GL.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

                if (!this.HasRenderTarget || this.RenderTarget is null)
                {
                    return;
                }

                // Calculate the current relative movement of the camera
                if (this.WantsToMove)
                {
                    switch (this.RenderTarget.Projection)
                    {
                        case ProjectionType.Orthographic:
                        {
                            Calculate2DMovement();
                            break;
                        }
                        case ProjectionType.Perspective:
                        {
                            Calculate3DMovement();
                            break;
                        }
                    }
                }

                // Render the current object
                // Tick the actor, advancing any time-dependent behaviour
                var tickingRenderable = this.RenderTarget as ITickingActor;
                tickingRenderable?.Tick(this.DeltaTime);

                // Update the camera with new parameters
                _camera.ViewportHeight = widgetHeight;
                _camera.ViewportWidth = widgetWidth;

                var view = _camera.GetViewMatrix();
                var projection = _camera.GetProjectionMatrix();

                if (this.RenderTarget.Projection == ProjectionType.Perspective)
                {
                    _grid?.Render(view, projection, _camera);
                }

                // Then render the visual component
                this.RenderTarget.Render(view, projection, _camera);
            }
        }

        /// <summary>
        /// Computes the movement of the camera in 3D space for this frame.
        /// </summary>
        private void Calculate2DMovement()
        {
            var mouse = Display.Default.ListDevices().FirstOrDefault(d => d.HasCursor);
            if (mouse is null)
            {
                return;
            }

            mouse.GetPosition(Screen.Default, out var mouseX, out var mouseY);

            var deltaMouseX = this.InitialMouseX - mouseX;
            var deltaMouseY = this.InitialMouseY - mouseY;

            _movement.Calculate2DMovement(deltaMouseX, deltaMouseY, this.DeltaTime);

            // Update the initial location for the next frame
            this.InitialMouseX = mouseX;
            this.InitialMouseY = mouseY;
        }

        /// <summary>
        /// Computes the movement of the camera in 3D space for this frame.
        /// </summary>
        private void Calculate3DMovement()
        {
            var mouse = Display.Default.ListDevices().FirstOrDefault(d => d.HasCursor);
            if (mouse is null)
            {
                return;
            }

            mouse.GetPosition(Screen.Default, out var mouseX, out var mouseY);

            var deltaMouseX = this.InitialMouseX - mouseX;
            var deltaMouseY = this.InitialMouseY - mouseY;

            _movement.Calculate3DMovement(deltaMouseX, deltaMouseY, this.DeltaTime);

            // Return the mouse to its original position
            mouse.Warp(Screen.Default, (int)this.InitialMouseX, (int)this.InitialMouseY);
        }

        /// <summary>
        /// Determines whether or not movement is currently disabled for the rendered object.
        /// </summary>
        /// <returns>true if movement is disabled; false otherwise.</returns>
        public bool IsMovementDisabled()
        {
            return this.RenderTarget == null || !this.RenderTarget.IsInitialized;
        }

        /// <summary>
        /// Sets the render target that is currently being rendered by the viewport renderer.
        /// </summary>
        /// <param name="inRenderable">inRenderable.</param>
        public void SetRenderTarget(IRenderable? inRenderable)
        {
            ThrowIfDisposed();

            lock (_renderTargetLock)
            {
                // Dispose of the old render target
                this.RenderTarget?.Dispose();

                // Assign the new one
                this.RenderTarget = inRenderable;

                // Set the default camera values
                if (!this.HasRenderTarget || this.RenderTarget is null)
                {
                    return;
                }
                _camera.ViewportWidth = (uint)_viewportWidget.AllocatedWidth;
                _camera.ViewportHeight = (uint)_viewportWidget.AllocatedHeight;

                if (this.RenderTarget is IDefaultCameraPositionProvider cameraPositionProvider)
                {
                    _camera.Position = cameraPositionProvider.DefaultCameraPosition;
                }

                _camera.Projection = this.RenderTarget.Projection;

                _camera.ResetRotation();
            }
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the object has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        private void ThrowIfDisposed()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(ToString() ?? nameof(ViewportRenderer));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.IsDisposed = true;

            this.RenderTarget?.Dispose();

            unsafe
            {
                fixed (uint* ptr = &_vertexArrayID)
                {
                    this.GL.DeleteVertexArrays(1u, ptr);
                }
            }
        }
    }
}
