//
//  Plain2DShader.cs
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
using Everlook.Viewport.Rendering.Core;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Everlook.Viewport.Rendering.Shaders
{
	/// <summary>
	/// A 2D object shader (billboards, textures, etc)
	/// </summary>
	public class Plain2DShader : ShaderProgram
	{
		private const string ChannelMaskIdentifier = "channelMask";
		private const string TextureIdentifier = "imageTextureSampler";

		/// <inheritdoc />
		protected override string VertexShaderResourceName => "Plain2D.Plain2DVertex";

		/// <inheritdoc />
		protected override string FragmentShaderResourceName => "Plain2D.Plain2DFragment";

		/// <inheritdoc />
		protected override string GeometryShaderResourceName => null;

		/// <summary>
		/// Sets the channel mask of the shader.
		/// </summary>
		/// <param name="channelMask">
		/// A four-component vector. This is multiplied with the final colour of the texture, and its components
		/// should typically be set to 1 or 0.
		/// </param>
		public void SetChannelMask(Vector4 channelMask)
		{
			Enable();

			int channelMaskVariableHandle = GL.GetUniformLocation(this.NativeShaderProgramID, ChannelMaskIdentifier);
			GL.Uniform4(channelMaskVariableHandle, channelMask);
		}

		/// <summary>
		/// Sets the texture of the shader.
		/// </summary>
		/// <param name="texture">The texture.</param>
		public void SetTexture(Texture2D texture)
		{
			if (texture == null)
			{
				throw new ArgumentNullException(nameof(texture));
			}

			Enable();

			GL.ActiveTexture(TextureUnit.Texture0);

			texture.Bind();

			int textureVariableHandle = GL.GetUniformLocation(this.NativeShaderProgramID, TextureIdentifier);
			int textureUnit = 0;
			GL.Uniform1(textureVariableHandle, 1, ref textureUnit);
		}
	}
}
