//
//  ShaderProgram.cs
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
using Everlook.Exceptions.Shader;
using Everlook.Utility;
using log4net;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Everlook.Viewport.Rendering.Core
{
	/// <summary>
	/// Wraps basic OpenGL functionality for a shader program. This class is made to be extended out into
	/// more advanced shaders with specific functionality.
	/// </summary>
	public abstract class ShaderProgram : IDisposable
	{
		private const string MVPIdentifier = "ModelViewProjection";

		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(ShaderProgram));

		/// <summary>
		/// Gets the native OpenGL ID of the shader program.
		/// </summary>
		protected int NativeShaderProgramID { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ShaderProgram"/> class, compiling and linking its associated shader sources into
		/// a shader program on the GPU.
		/// </summary>
		protected ShaderProgram()
		{
			string vertexShaderSource = GetShaderSource(this.VertexShaderResourceName);
			string fragmentShaderSource = GetShaderSource(this.FragmentShaderResourceName);

			int vertexShaderID = CompileShader(ShaderType.VertexShader, vertexShaderSource);
			int fragmentShaderID = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

			if (!string.IsNullOrEmpty(this.GeometryShaderResourceName))
			{
				string geometryShaderSource = GetShaderSource(this.GeometryShaderResourceName);
				int geometryShaderID = CompileShader(ShaderType.GeometryShader, geometryShaderSource);
				this.NativeShaderProgramID = LinkShader(vertexShaderID, fragmentShaderID, geometryShaderID);
			}
			else
			{
				this.NativeShaderProgramID = LinkShader(vertexShaderID, fragmentShaderID);
			}
		}

		/// <summary>
		/// Sets the Model-View-Projection matrix of this shader.
		/// </summary>
		/// <param name="mvpMatrix">The ModelViewProjection matrix.</param>
		public void SetMVPMatrix(Matrix4 mvpMatrix)
		{
			int projectionShaderVariableHandle = GL.GetUniformLocation(this.NativeShaderProgramID, MVPIdentifier);
			GL.UniformMatrix4(projectionShaderVariableHandle, false, ref mvpMatrix);
		}

		/// <summary>
		/// Gets the name of vertex shader in the resources. This will be used with the resource path to load the source.
		/// Do not include any extensions. Folder prefixes are optional.
		///
		/// Valid: WorldModelVertex, Plain2D.Plain2DVertex
		/// Invalid: Resources.Content.Shaders.WorldModelVertex.glsl
		/// </summary>
		protected abstract string VertexShaderResourceName { get; }

		/// <summary>
		/// Gets the name of the fragment shader in the resources. This will be used with the resource path to load the source.
		/// Do not include any extensions. Folder prefixes are optional.
		///
		/// Valid: WorldModelFragment, Plain2D.Plain2DFragment
		/// Invalid: Resources.Content.Shaders.WorldModelFragment.glsl
		/// </summary>
		protected abstract string FragmentShaderResourceName { get; }

		/// <summary>
		/// Gets the name of the fragment shader in the resources. This value is optional, and will be used with the
		/// resource path to load the source. Do not include any extensions. Folder prefixes are optional.
		///
		/// Valid: WorldModelGeometry, Plain2D.Plain2DGeometry
		/// Invalid: Resources.Content.Shaders.WorldModelGeometry.glsl
		/// </summary>
		protected abstract string GeometryShaderResourceName { get; }

		/// <summary>
		/// Links a vertex shader and a fragment shader into a complete shader program.
		/// </summary>
		/// <param name="vertexShaderID">The native handle to the object code of the vertex shader.</param>
		/// <param name="fragmentShaderID">The native handle to the object code of the fragment shader.</param>
		/// <param name="geometryShaderID">Optional. The native handle to the object code of the geometry shader.</param>
		/// <returns>A native handle to a shader program.</returns>
		/// <exception cref="ShaderLinkingException">Thrown if the linking fails.</exception>
		private static int LinkShader(int vertexShaderID, int fragmentShaderID, int geometryShaderID = -1)
		{
			Log.Info("Linking shader program...");
			int program = GL.CreateProgram();

			GL.AttachShader(program, vertexShaderID);
			GL.AttachShader(program, fragmentShaderID);

			if (geometryShaderID > -1)
			{
				GL.AttachShader(program, geometryShaderID);
			}

			GL.LinkProgram(program);

			GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int result);
			bool linkingSucceeded = result > 0;

			GL.GetProgram(program, GetProgramParameterName.InfoLogLength, out int linkingLogLength);
			GL.GetProgramInfoLog(program, out string linkingLog);

			// Clean up the shader source code and unlinked object files from graphics memory
			GL.DetachShader(program, vertexShaderID);
			GL.DetachShader(program, fragmentShaderID);

			GL.DeleteShader(vertexShaderID);
			GL.DeleteShader(fragmentShaderID);

			if (!linkingSucceeded)
			{
				throw new ShaderLinkingException(linkingLog);
			}

			if (linkingLogLength > 0)
			{
				Log.Warn($"Warnings were raised during shader liGL.Programnking. Please review the following log: \n" +
						 $"{linkingLog}");
			}

			return program;
		}

		/// <summary>
		/// Compiles a portion of the shader program into object code on the GPU.
		/// </summary>
		/// <param name="shaderType">The type of shader to compile.</param>
		/// <param name="shaderSource">The source code of the shader.</param>
		/// <returns>A native handle to the shader object code.</returns>
		/// <exception cref="ShaderCompilationException">Thrown if the compilation fails.</exception>
		private static int CompileShader(ShaderType shaderType, string shaderSource)
		{
			if (string.IsNullOrEmpty(shaderSource))
			{
				throw new ArgumentNullException
				(
					nameof(shaderSource),
					"No shader source was given. Check that the names are correct."
				);
			}

			int shader = GL.CreateShader(shaderType);

			Log.Info("Compiling shader...");
			GL.ShaderSource(shader, shaderSource);
			GL.CompileShader(shader);

			GL.GetShader(shader, ShaderParameter.CompileStatus, out int result);
			bool compilationSucceeded = result > 0;

			GL.GetShader(shader, ShaderParameter.InfoLogLength, out int compilationLogLength);
			GL.GetShaderInfoLog(shader, out string compilationLog);

			if (!compilationSucceeded)
			{
				GL.DeleteShader(shader);

				throw new ShaderCompilationException(ShaderType.VertexShader, compilationLog);
			}

			if (compilationLogLength > 0)
			{
				Log.Warn($"Warnings were raised during shader compilation. Please review the following log: \n" +
						 $"{compilationLog}");
			}

			return shader;
		}

		/// <summary>
		/// Gets the GLSL source of a shader.
		/// </summary>
		/// <param name="shaderResourceName">The name of the shader.</param>
		/// <returns>The source of the shader.</returns>
		private static string GetShaderSource(string shaderResourceName)
		{
			return ResourceManager.LoadStringResource($"Everlook.Content.Shaders.{shaderResourceName}.glsl");
		}

		/// <summary>
		/// Enables the shader, making it the current one.
		/// </summary>
		public void Enable()
		{
			GL.UseProgram(this.NativeShaderProgramID);
		}

		/// <summary>
		/// Disposes the <see cref="ShaderProgram"/>, deleting it and all its native resources.
		/// </summary>
		public void Dispose()
		{
			GL.DeleteProgram(this.NativeShaderProgramID);
			this.NativeShaderProgramID = -1;
		}
	}
}
