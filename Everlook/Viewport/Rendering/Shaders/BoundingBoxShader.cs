//
//  BoundingBoxShader.cs
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

using Everlook.Viewport.Rendering.Core;
using OpenTK;
using OpenTK.Graphics;

namespace Everlook.Viewport.Rendering.Shaders
{
	/// <summary>
	/// A bounding box shader.
	/// </summary>
	public class BoundingBoxShader : ShaderProgram
	{
		private const string IsInstance = nameof(IsInstance);

		private const string ViewMatrix = nameof(ViewMatrix);
		private const string ProjectionMatrix = nameof(ProjectionMatrix);

		private const string ColourIdentifier = "boxColour";

		/// <inheritdoc />
		protected override string VertexShaderResourceName => "BoundingBox.BoundingBoxVertex";

		/// <inheritdoc />
		protected override string FragmentShaderResourceName => "BoundingBox.BoundingBoxFragment";

		/// <inheritdoc />
		protected override string GeometryShaderResourceName => null;

		/// <summary>
		/// Sets the instancing flag.
		/// </summary>
		/// <param name="isInstanced">Whether or not the shader should render instances.</param>
		public void SetIsInstance(bool isInstanced)
		{
			SetBoolean(isInstanced, IsInstance);
		}

		/// <summary>
		/// Sets the current view matrix of the shader.
		/// </summary>
		/// <param name="viewMatrix">The model-view matrix.</param>
		public void SetViewMatrix(Matrix4 viewMatrix)
		{
			SetMatrix(viewMatrix, ViewMatrix);
		}

		/// <summary>
		/// Sets the current projection matrix of the shader.
		/// </summary>
		/// <param name="projectionMatrix">The projection matrix.</param>
		public void SetProjectionMatrix(Matrix4 projectionMatrix)
		{
			SetMatrix(projectionMatrix, ProjectionMatrix);
		}

		/// <summary>
		/// Sets the line colour of the bounding box.
		/// </summary>
		/// <param name="colour">The colour to set the lines to.</param>
		public void SetLineColour(Color4 colour)
		{
			SetColor4(colour, ColourIdentifier);
		}
	}
}
