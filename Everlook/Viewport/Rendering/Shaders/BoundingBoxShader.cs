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

		public void SetLineColour(Color4 colour)
		{
			int boxColourShaderVariableHandle = GL.GetUniformLocation(this.NativeShaderProgramID, ColourIdentifier);
			GL.Uniform4(boxColourShaderVariableHandle, colour);
		}

		protected override string VertexShaderResourceName => "BoundingBox.BoundingBoxVertex";
		protected override string FragmentShaderResourceName => "BoundingBox.BoundingBoxFragment";
	}
}