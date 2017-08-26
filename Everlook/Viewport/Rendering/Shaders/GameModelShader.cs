//
//  GameModelShader.cs
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
using Everlook.Viewport.Rendering.Shaders.Components;
using OpenTK.Graphics.OpenGL;
using Warcraft.Core.Shading.Blending;
using Warcraft.MDX.Visual;

namespace Everlook.Viewport.Rendering.Shaders
{
	/// <summary>
	/// A game model shader.
	/// </summary>
	public class GameModelShader : ShaderProgram
	{
		private const string AlphaThresholdIdentifier = "alphaThreshold";
		private const string Diffuse0Identifier = nameof(Diffuse0Identifier);
		private const string Diffuse1Identifier = nameof(Diffuse1Identifier);

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

		/// <summary>
		/// Binds a texture to a sampler in the shader
		/// TODO: Refactor or remove this
		/// </summary>
		/// <param name="textureUnit">The texture unit to bind the texture to</param>
		/// <param name="uniform">The uniform name where the texture should be bound.</param>
		/// <param name="texture">The texture to bind.</param>
		public void BindTexture2D(TextureUnit textureUnit, TextureUniform uniform, Texture2D texture)
		{
			if (texture == null)
			{
				throw new ArgumentNullException(nameof(texture));
			}

			Enable();

			GL.ActiveTexture(textureUnit);
			texture.Bind();

			int textureVariableHandle = GL.GetUniformLocation(this.NativeShaderProgramID, uniform.ToString());
			GL.Uniform1(textureVariableHandle, (int)uniform);
		}

		/// <summary>
		/// Sets the current <see cref="MDXMaterial"/> that the shader renders.
		/// </summary>
		/// <param name="modelMaterial">The material to use.</param>
		public void SetMaterial(MDXMaterial modelMaterial)
		{
			if (modelMaterial == null)
			{
				throw new ArgumentNullException(nameof(modelMaterial));
			}

			Enable();

			// Set two-sided rendering
			if (modelMaterial.Flags.HasFlag(EMDXRenderFlag.TwoSided))
			{
				GL.Disable(EnableCap.CullFace);
			}
			else
			{
				GL.Enable(EnableCap.CullFace);
			}

			if (BlendingState.EnableBlending[modelMaterial.BlendMode])
			{
				GL.Enable(EnableCap.Blend);
			}
			else
			{
				GL.Disable(EnableCap.Blend);
			}

			var dstA = BlendingState.DestinationAlpha[modelMaterial.BlendMode];
			var srcA = BlendingState.SourceAlpha[modelMaterial.BlendMode];

			var dstC = BlendingState.DestinationColour[modelMaterial.BlendMode];
			var srcC = BlendingState.SourceColour[modelMaterial.BlendMode];

			GL.BlendFuncSeparate
			(
				(BlendingFactorSrc)srcC,
				(BlendingFactorDest)dstC,
				(BlendingFactorSrc)srcA,
				(BlendingFactorDest)dstA
			);

			switch (modelMaterial.BlendMode)
			{
				case BlendingMode.AlphaKey:
				{
					SetAlphaDiscardThreshold(224.0f / 255.0f);
					break;
				}
				case BlendingMode.Opaque:
				{
					SetAlphaDiscardThreshold(0.0f);
					break;
				}
				default:
				{
					SetAlphaDiscardThreshold(1.0f / 225.0f);
					break;
				}
			}
		}

		/// <summary>
		/// Sets the current threshold for pixel discarding. Unused if the current material is opaque.
		/// </summary>
		/// <param name="threshold">The alpha value threshold.</param>
		public void SetAlphaDiscardThreshold(float threshold)
		{
			Enable();

			int alphaThresholdLoc = GL.GetUniformLocation(this.NativeShaderProgramID, AlphaThresholdIdentifier);
			GL.Uniform1(alphaThresholdLoc, threshold);
		}
	}
}
