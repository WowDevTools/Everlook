//
//  WorldModelShader.cs
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

using Gdk;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace Everlook.Viewport.Rendering.Shaders.Components
{
	/// <summary>
	/// This shader component controls an implementation of solid wireframe rendering.
	/// </summary>
	public class SolidWireframe
	{
		private const string WireframeFadeWidth = nameof(WireframeFadeWidth);
		private const string ViewportMatrix = nameof(ViewportMatrix);

		private const string IsWireframeEnabled = nameof(IsWireframeEnabled);
		private const string WireframeColour = nameof(WireframeColour);
		private const string WireframeLineWidth = nameof(WireframeLineWidth);

		private static readonly Color4 StandardColour = new Color4(234, 161, 0, 255);
		private static readonly Color4 HighlightColour = new Color4(217, 129, 3, 255);

		private readonly int ParentShaderNativeID;

		private bool EnabledInternal;

		/// <summary>
		/// Gets or sets a value indicating whether or not the wireframe should be rendered.
		/// </summary>
		public bool Enabled
		{
			get => this.EnabledInternal;
			set
			{
				this.EnabledInternal = value;
				SetWireframeState(value);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SolidWireframe"/> class, and attaches it to the given parent shader.
		/// </summary>
		/// <param name="parentShaderID">The native ID of the parent shader.</param>
		public SolidWireframe(int parentShaderID)
		{
			this.ParentShaderNativeID = parentShaderID;
			this.Enabled = false;

			SetWireframeLineWidth(2);
			SetWireframeFadeWidth(2);
			SetWireframeColour(StandardColour);
		}

		private void EnableParent()
		{
			GL.UseProgram(this.ParentShaderNativeID);
		}

		private void SetWireframeState(bool isEnabled)
		{
			EnableParent();

			int enabledLoc = GL.GetUniformLocation(this.ParentShaderNativeID, IsWireframeEnabled);
			GL.Uniform1(enabledLoc, isEnabled ? 1 : 0);
		}

		/// <summary>
		/// Sets the width of the lines in the wireframe. Note that this line is for the edge of one triangle, and will
		/// effectively be doubled, since triangles share lines.
		/// </summary>
		/// <param name="lineWidth">The line width, in pixels.</param>
		public void SetWireframeLineWidth(int lineWidth)
		{
			EnableParent();

			int lineWidthLoc = GL.GetUniformLocation(this.ParentShaderNativeID, WireframeLineWidth);
			GL.Uniform1(lineWidthLoc, lineWidth);
		}

		/// <summary>
		/// Sets the colour of the wireframe.
		/// </summary>
		/// <param name="wireframeColour">The wire colour.</param>
		public void SetWireframeColour(RGBA wireframeColour)
		{
			Color4 colour = new Color4
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
		public void SetWireframeColour(Color4 wireframeColour)
		{
			EnableParent();

			int colourLoc = GL.GetUniformLocation(this.ParentShaderNativeID, WireframeColour);
			GL.Uniform4(colourLoc, wireframeColour);
		}

		/// <summary>
		/// Sets the width of the edge fade of the wireframe's lines.
		/// </summary>
		/// <param name="fadeWidth">The width in pixels.</param>
		public void SetWireframeFadeWidth(int fadeWidth)
		{
			EnableParent();

			int fadeWidthLoc = GL.GetUniformLocation(this.ParentShaderNativeID, WireframeFadeWidth);
			GL.Uniform1(fadeWidthLoc, fadeWidth);
		}

		/// <summary>
		/// Sets the viewport matrix that will transform NDC coordinates to screen space coordinates.
		/// </summary>
		/// <param name="viewportMatrix">The viewport matrix.</param>
		public void SetViewportMatrix(Matrix4 viewportMatrix)
		{
			EnableParent();

			int viewportMatrixLoc = GL.GetUniformLocation(this.ParentShaderNativeID, ViewportMatrix);
			GL.UniformMatrix4(viewportMatrixLoc, false, ref viewportMatrix);
		}
	}
}
