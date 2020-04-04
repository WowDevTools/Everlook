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
using Gdk;
using Gtk;
using Silk.NET.OpenGL;

namespace Everlook.UI.Widgets
{
    /// <summary>
    /// The <see cref="ViewportArea"/> is a GTK widget for which an OpenGL context can be used to draw arbitrary
    /// graphics.
    /// </summary>
    [CLSCompliant(false)]
    [ToolboxItem(true)]
    public class ViewportArea : GLArea
    {
        private readonly bool _isDebugEnabled;
        private readonly bool _isForwardCompatible;
        private readonly bool _withDepthBuffer;
        private readonly bool _withStencilBuffer;
        private readonly bool _withAlpha;

        private bool _isInitialized;

        /// <summary>
        /// The previous frame time reported by GTK.
        /// </summary>
        private double? _previousFrameTime;

        /// <summary>
        /// Gets the time taken to render the last frame (in seconds).
        /// </summary>
        public double DeltaTime { get; private set; }

        /// <summary>
        /// Called when this <see cref="ViewportArea"/> has finished initializing.
        /// </summary>
        public event EventHandler? Initialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewportArea"/> class.
        /// </summary>
        /// <param name="glVersionMajor">The major version of OpenGL to request.</param>
        /// <param name="glVersionMinor">The minor version of OpenGL to request.</param>
        /// <param name="isDebugEnabled">Whether the OpenGL context should be debuggable.</param>
        /// <param name="isForwardCompatible">Whether the OpenGL context should be forward compatible.</param>
        /// <param name="withDepthBuffer">Whether the viewport area should have a depth buffer.</param>
        /// <param name="withStencilBuffer">Whether the viewport area should have a stencil buffer.</param>
        /// <param name="withAlpha">Whether the viewport area should have an alpha channel.</param>
        public ViewportArea
        (
            int glVersionMajor,
            int glVersionMinor,
            bool isDebugEnabled = false,
            bool isForwardCompatible = false,
            bool withDepthBuffer = true,
            bool withStencilBuffer = true,
            bool withAlpha = true
        )
        {
            _isDebugEnabled = isDebugEnabled;
            _isForwardCompatible = isForwardCompatible;
            _withDepthBuffer = withDepthBuffer;
            _withStencilBuffer = withStencilBuffer;
            _withAlpha = withAlpha;

            AddTickCallback(UpdateFrameTime);

            SetRequiredVersion(glVersionMajor, glVersionMinor);

            this.HasDepthBuffer = _withDepthBuffer;
            this.HasStencilBuffer = _withStencilBuffer;
            this.HasAlpha = _withAlpha;
        }

        /// <summary>
        /// Gets an instance of the OpenGL API.
        /// </summary>
        /// <returns>The API instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the viewport area has not been initialized.</exception>
        public GL GetAPI()
        {
            if (_isInitialized)
            {
                return GL.GetApi();
            }

            throw new InvalidOperationException();
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

            if (!_previousFrameTime.HasValue)
            {
                _previousFrameTime = frameTimeµSeconds;

                return true;
            }

            var frameTimeSeconds = (frameTimeµSeconds - _previousFrameTime.Value) / 10e6;

            this.DeltaTime = (float)frameTimeSeconds;
            _previousFrameTime = frameTimeµSeconds;

            return true;
        }

        /// <inheritdoc />
        protected override GLContext OnCreateContext()
        {
            var gdkGLContext = this.Window.CreateGlContext();

            GetRequiredVersion(out var major, out var minor);
            gdkGLContext.SetRequiredVersion(major, minor);
            gdkGLContext.DebugEnabled = _isDebugEnabled;
            gdkGLContext.ForwardCompatible = _isForwardCompatible;

            gdkGLContext.Realize();
            return gdkGLContext;
        }

        /// <inheritdoc/>
        protected override void OnDestroyed()
        {
            Dispose(true);
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
        }

        /// <summary>
        /// Invokes the <see cref="Initialized"/> event.
        /// </summary>
        protected virtual void OnInitialized()
        {
            this.Initialized?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        protected override bool OnDrawn(Cairo.Context cr)
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            var result = base.OnDrawn(cr);
            return result;
        }

        /// <summary>
        /// Initializes the <see cref="ViewportArea"/> with its given values.
        /// </summary>
        private void Initialize()
        {
            _isInitialized = true;

            // Make the GDK GL context current
            MakeCurrent();

            OnInitialized();
        }
    }
}
