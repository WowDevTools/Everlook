using Everlook.Viewport.Rendering.Core;
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

		/// <summary>
		/// Binds a texture to a sampler in the shader
		/// TODO: Refactor or remove this
		/// </summary>
		/// <param name="textureUnit"></param>
		/// <param name="uniform"></param>
		/// <param name="texture"></param>
		public void BindTexture2D(TextureUnit textureUnit, TextureUniform uniform, Texture2D texture)
		{
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
			int alphaThresholdLoc = GL.GetUniformLocation(this.NativeShaderProgramID, AlphaThresholdIdentifier);
			GL.Uniform1(alphaThresholdLoc, threshold);
		}

		protected override string VertexShaderResourceName => "WorldModel.WorldModelVertex";
		protected override string FragmentShaderResourceName => "WorldModel.WorldModelFragment";
	}
}