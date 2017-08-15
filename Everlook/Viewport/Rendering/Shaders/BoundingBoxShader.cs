//
//  BoundingBoxShader.cs
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

using Everlook.Viewport.Rendering.Core;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace Everlook.Viewport.Rendering.Shaders
{
	/// <summary>
	/// A bounding box shader.
	/// </summary>
	public class BoundingBoxShader : ShaderProgram
	{
		private const string ColourIdentifier = "boxColour";

		/// <inheritdoc />
		protected override string VertexShaderResourceName => "BoundingBox.BoundingBoxVertex";

		/// <inheritdoc />
		protected override string FragmentShaderResourceName => "BoundingBox.BoundingBoxFragment";

		/// <inheritdoc />
		protected override string GeometryShaderResourceName => null;

		/// <summary>
		/// Sets the line colour of the bounding box.
		/// </summary>
		/// <param name="colour">The colour to set the lines to.</param>
		public void SetLineColour(Color4 colour)
		{
			Enable();

			int boxColourShaderVariableHandle = GL.GetUniformLocation(this.NativeShaderProgramID, ColourIdentifier);
			GL.Uniform4(boxColourShaderVariableHandle, colour);
		}
	}
}
