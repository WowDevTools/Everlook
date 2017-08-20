//
//  GameModelShader.cs
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
using Everlook.Viewport.Rendering.Shaders.Components;

namespace Everlook.Viewport.Rendering.Shaders
{
	/// <summary>
	/// A game model shader.
	/// </summary>
	public class GameModelShader : ShaderProgram
	{
		/// <inheritdoc />
		protected override string VertexShaderResourceName => "GameModel.GameModelVertex";

		/// <inheritdoc />
		protected override string FragmentShaderResourceName => "GameModel.GameModelFragment";

		/// <inheritdoc />
		protected override string GeometryShaderResourceName => "GameModel.GameModelGeometry";

		/// <summary>
		/// Gets the <see cref="SolidWireframe"/> shader component, which enables solid wireframe rendering.
		/// </summary>
		public SolidWireframe Wireframe { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="GameModelShader"/> class.
		/// </summary>
		public GameModelShader()
		{
			this.Wireframe = new SolidWireframe(this.NativeShaderProgramID);
		}
	}
}
