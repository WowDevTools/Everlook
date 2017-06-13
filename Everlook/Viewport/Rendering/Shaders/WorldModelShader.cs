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

using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Shaders.Components;
using OpenTK.Graphics.OpenGL;
using Warcraft.Core.Shading.Blending;
using Warcraft.WMO.RootFile.Chunks;

namespace Everlook.Viewport.Rendering.Shaders
{
	/// <summary>
	/// Shader for world model objects (WMO).
	/// </summary>
	public class WorldModelShader : ShaderProgram
	{
		private const string AlphaThresholdIdentifier = "alphaThreshold";

		public readonly GlobalLighting Lighting;
		public readonly SolidWireframe Wireframe;

		/// <summary>
		/// Initializes a new <see cref="WorldModelShader"/> object.
		/// </summary>
		public WorldModelShader()
		{
			this.Lighting = new GlobalLighting(this.NativeShaderProgramID);
			this.Wireframe = new SolidWireframe(this.NativeShaderProgramID);
		}

		/// <summary>
		/// Binds a texture to a sampler in the shader
		/// TODO: Refactor or remove this
		/// </summary>
		/// <param name="textureUnit"></param>
		/// <param name="uniform"></param>
		/// <param name="texture"></param>
		public void BindTexture2D(TextureUnit textureUnit, TextureUniform uniform, Texture2D texture)
		{
			Enable();

			GL.ActiveTexture(textureUnit);
			texture.Bind();

			int textureVariableHandle = GL.GetUniformLocation(this.NativeShaderProgramID, uniform.ToString());
			GL.Uniform1(textureVariableHandle, (int)uniform);
		}

		/// <summary>
		/// Sets the current <see cref="ModelMaterial"/> that the shader renders.
		/// </summary>
		/// <param name="modelMaterial"></param>
		public void SetMaterial(ModelMaterial modelMaterial)
		{
			Enable();

			// Set two-sided rendering
			if (modelMaterial.Flags.HasFlag(MaterialFlags.TwoSided))
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
		/// <param name="threshold"></param>
		public void SetAlphaDiscardThreshold(float threshold)
		{
			Enable();

			int alphaThresholdLoc = GL.GetUniformLocation(this.NativeShaderProgramID, AlphaThresholdIdentifier);
			GL.Uniform1(alphaThresholdLoc, threshold);
		}

		protected override string VertexShaderResourceName => "WorldModel.WorldModelVertex";
		protected override string FragmentShaderResourceName => "WorldModel.WorldModelFragment";
		protected override string GeometryShaderResourceName => "WorldModel.WorldModelGeometry";
	}
}