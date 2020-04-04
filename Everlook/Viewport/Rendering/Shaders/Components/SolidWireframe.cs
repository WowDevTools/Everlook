//
//  SolidWireframe.cs
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

using System.Numerics;
using Everlook.Viewport.Rendering.Core;
using Gdk;
using Silk.NET.OpenGL;

namespace Everlook.Viewport.Rendering.Shaders.Components
{
    /// <summary>
    /// This shader component controls an implementation of solid wireframe rendering.
    /// </summary>
    public class SolidWireframe : GraphicsObject
    {
        private const string ViewportMatrix = nameof(ViewportMatrix);

        private const string IsWireframeEnabled = nameof(IsWireframeEnabled);

        private const string WireframeColour = nameof(WireframeColour);

        private const string WireframeLineWidth = nameof(WireframeLineWidth);
        private const string WireframeFadeWidth = nameof(WireframeFadeWidth);

        /// <summary>
        /// The standard colour of the wireframe.
        /// </summary>
        public static readonly Vector4 StandardColour = new Vector4(234, 161, 0, 255);

        /// <summary>
        /// The standard highlight colour of the wireframe.
        /// </summary>
        public static readonly Vector4 HighlightColour = new Vector4(217, 129, 3, 255);

        private readonly uint _parentShaderNativeID;

        private bool _enabledInternal;

        /// <summary>
        /// Gets or sets a value indicating whether or not the wireframe should be rendered.
        /// </summary>
        public bool Enabled
        {
            get => _enabledInternal;
            set
            {
                _enabledInternal = value;
                SetWireframeState(value);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SolidWireframe"/> class, and attaches it to the given parent
        /// shader.
        /// </summary>
        /// <param name="gl">The OpenGL API.</param>
        /// <param name="parentShaderID">The native ID of the parent shader.</param>
        public SolidWireframe(GL gl, uint parentShaderID)
            : base(gl)
        {
            _parentShaderNativeID = parentShaderID;
            this.Enabled = false;

            SetWireframeLineWidth(2);
            SetWireframeFadeWidth(2);
            SetWireframeColour(StandardColour);
        }

        private void EnableParent()
        {
            this.GL.UseProgram(_parentShaderNativeID);
        }

        private void SetWireframeState(bool isEnabled)
        {
            EnableParent();

            var enabledLoc = this.GL.GetUniformLocation(_parentShaderNativeID, IsWireframeEnabled);
            this.GL.Uniform1(enabledLoc, isEnabled ? 1 : 0);
        }

        /// <summary>
        /// Sets the width of the lines in the wireframe. Note that this line is for the edge of one triangle, and will
        /// effectively be doubled, since triangles share lines.
        /// </summary>
        /// <param name="lineWidth">The line width, in pixels.</param>
        public void SetWireframeLineWidth(int lineWidth)
        {
            EnableParent();

            var lineWidthLoc = this.GL.GetUniformLocation(_parentShaderNativeID, WireframeLineWidth);
            this.GL.Uniform1(lineWidthLoc, lineWidth);
        }

        /// <summary>
        /// Sets the colour of the wireframe.
        /// </summary>
        /// <param name="wireframeColour">The wire colour.</param>
        public void SetWireframeColour(RGBA wireframeColour)
        {
            var colour = new Vector4
            (
                (float)wireframeColour.Red,
                (float)wireframeColour.Green,
                (float)wireframeColour.Blue,
                (float)wireframeColour.Alpha
            );

            SetWireframeColour(colour);
        }

        /// <summary>
        /// Sets the colour of the wireframe.
        /// </summary>
        /// <param name="wireframeColour">The wire colour.</param>
        public void SetWireframeColour(Vector4 wireframeColour)
        {
            EnableParent();

            var colourLoc = this.GL.GetUniformLocation(_parentShaderNativeID, WireframeColour);
            this.GL.Uniform4(colourLoc, wireframeColour);
        }

        /// <summary>
        /// Sets the width of the edge fade of the wireframe's lines.
        /// </summary>
        /// <param name="fadeWidth">The width in pixels.</param>
        public void SetWireframeFadeWidth(int fadeWidth)
        {
            EnableParent();

            var fadeWidthLoc = this.GL.GetUniformLocation(_parentShaderNativeID, WireframeFadeWidth);
            this.GL.Uniform1(fadeWidthLoc, fadeWidth);
        }

        /// <summary>
        /// Sets the viewport matrix that will transform NDC coordinates to screen space coordinates.
        /// </summary>
        /// <param name="viewportMatrix">The viewport matrix.</param>
        public void SetViewportMatrix(Matrix4x4 viewportMatrix)
        {
            EnableParent();

            var viewportMatrixLoc = this.GL.GetUniformLocation(_parentShaderNativeID, ViewportMatrix);
            unsafe
            {
                this.GL.UniformMatrix4(viewportMatrixLoc, 4 * 4, false, &viewportMatrix.M11);
            }
        }
    }
}
