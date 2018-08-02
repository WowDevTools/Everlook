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
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using Warcraft.Core.Shading.Blending;
using Warcraft.Core.Shading.MDX;
using Warcraft.MDX.Visual;

namespace Everlook.Viewport.Rendering.Shaders
{
	/// <summary>
	/// A game model shader.
	/// </summary>
	public class GameModelShader : ShaderProgram
	{
		private const string AlphaThresholdIdentifier = "alphaThreshold";
		private const string VertexShaderPath = nameof(VertexShaderPath);
		private const string FragmentShaderPath = nameof(FragmentShaderPath);
		private const string BaseColour = nameof(BaseColour);
		private const string IsInstance = nameof(IsInstance);

		private const string ModelMatrix = nameof(ModelMatrix);
		private const string ViewMatrix = nameof(ViewMatrix);
		private const string ProjectionMatrix = nameof(ProjectionMatrix);

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

			SetBaseInputColour(Color4.White);
			SetFragmentShaderType(MDXFragmentShaderType.Combiners_Opaque);
		}

		/// <summary>
		/// Sets the instancing flag.
		/// </summary>
		/// <param name="isInstanced">Whether or not the shader should render instances.</param>
		public void SetIsInstance(bool isInstanced)
		{
			SetBoolean(isInstanced, IsInstance);
		}

		/// <summary>
		/// Sets the current model matrix of the shader.
		/// </summary>
		/// <param name="modelMatrix">The model matrix.</param>
		public void SetModelMatrix(Matrix4 modelMatrix)
		{
			SetMatrix(modelMatrix, ModelMatrix);
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
		/// Sets the base input colour for the shader.
		/// </summary>
		/// <param name="colour">The base colour.</param>
		public void SetBaseInputColour(Color4 colour)
		{
			SetColor4(colour, BaseColour);
		}

		/// <summary>
		/// Set the fragment shading path in use.
		/// </summary>
		/// <param name="shaderType">The shader path.</param>
		public void SetFragmentShaderType(MDXFragmentShaderType shaderType)
		{
			SetInteger((int)shaderType, FragmentShaderPath);
		}

		/// <summary>
		/// Set the vertex shading path in use.
		/// </summary>
		/// <param name="shaderType">The shader path.</param>
		public void SetVertexShaderType(MDXVertexShaderType shaderType)
		{
			SetInteger((int)shaderType, VertexShaderPath);
		}

		/// <summary>
		/// Binds a texture to the first texture in the shader.
		/// </summary>
		/// <param name="texture">The texture to bind.</param>
		public void BindTexture0(Texture2D texture)
		{
			BindTexture2D(TextureUnit.Texture0, TextureUniform.Texture0, texture);
		}

		/// <summary>
		/// Binds a texture to the second texture in the shader.
		/// </summary>
		/// <param name="texture">The texture to bind.</param>
		public void BindTexture1(Texture2D texture)
		{
			BindTexture2D(TextureUnit.Texture1, TextureUniform.Texture1, texture);
		}

		/// <summary>
		/// Binds a texture to the second texture in the shader.
		/// </summary>
		/// <param name="texture">The texture to bind.</param>
		public void BindTexture2(Texture2D texture)
		{
			BindTexture2D(TextureUnit.Texture2, TextureUniform.Texture2, texture);
		}

		/// <summary>
		/// Binds a texture to the second texture in the shader.
		/// </summary>
		/// <param name="texture">The texture to bind.</param>
		public void BindTexture3(Texture2D texture)
		{
			BindTexture2D(TextureUnit.Texture3, TextureUniform.Texture3, texture);
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
			SetFloat(threshold, AlphaThresholdIdentifier);
		}
	}
}
