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

		public void SetChannelMask(Vector4 channelMask)
		{
			int channelMaskVariableHandle = GL.GetUniformLocation(this.NativeShaderProgramID, ChannelMaskIdentifier);
			GL.Uniform4(channelMaskVariableHandle, channelMask);
		}

		public void SetTexture(Texture2D texture)
		{
			GL.ActiveTexture(TextureUnit.Texture0);

			texture.Bind();

			int textureVariableHandle = GL.GetUniformLocation(this.NativeShaderProgramID, TextureIdentifier);
			int textureUnit = 0;
			GL.Uniform1(textureVariableHandle, 1, ref textureUnit);
		}

		protected override string VertexShaderResourceName => "Plain2D.Plain2DVertex";
		protected override string FragmentShaderResourceName => "Plain2D.Plain2DFragment";
	}
}