using Everlook.Viewport.Rendering.Core;

namespace Everlook.Viewport.Rendering.Shaders
{
	/// <summary>
	/// A game model shader.
	/// </summary>
	public class GameModelShader : ShaderProgram
	{
		protected override string VertexShaderResourceName => "GameModel.GameModelVertex";
		protected override string FragmentShaderResourceName => "GameModel.GameModelFragment";
		protected override string GeometryShaderResourceName => null;
	}
}