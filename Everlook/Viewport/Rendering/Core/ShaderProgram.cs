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
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Everlook.Exceptions.Shader;
using Everlook.Utility;
using Everlook.Viewport.Rendering.Shaders.GLSLExtended;
using log4net;
using Silk.NET.OpenGL;

namespace Everlook.Viewport.Rendering.Core
{
    /// <summary>
    /// Wraps basic OpenGL functionality for a shader program. This class is made to be extended out into
    /// more advanced shaders with specific functionality.
    /// </summary>
    public abstract class ShaderProgram : GraphicsObject, IDisposable
    {
        private const string MVPIdentifier = "ModelViewProjection";

        /// <summary>
        /// Logger instance for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(ShaderProgram));

        /// <summary>
        /// Gets the native OpenGL ID of the shader program.
        /// </summary>
        protected uint NativeShaderProgramID { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ShaderProgram"/> class, compiling and linking its associated
        /// shader sources into a shader program on the GPU.
        /// </summary>
        /// <param name="gl">The OpenGL API.</param>
        [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor", Justification = "Required.")]
        protected ShaderProgram(GL gl)
            : base(gl)
        {
            var vertexShaderSource = GetShaderSource(this.VertexShaderResourceName);
            var fragmentShaderSource = GetShaderSource(this.FragmentShaderResourceName);

            if (vertexShaderSource is null || fragmentShaderSource is null)
            {
                throw new InvalidOperationException();
            }

            var vertexShaderID = CompileShader(ShaderType.VertexShader, vertexShaderSource);
            var fragmentShaderID = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

            if (!string.IsNullOrEmpty(this.GeometryShaderResourceName))
            {
                var geometryShaderSource = GetShaderSource(this.GeometryShaderResourceName!);

                if (geometryShaderSource is null)
                {
                    throw new InvalidOperationException();
                }

                var geometryShaderID = CompileShader(ShaderType.GeometryShader, geometryShaderSource);
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
        public void SetMVPMatrix(Matrix4x4 mvpMatrix)
        {
            SetMatrix(mvpMatrix, MVPIdentifier);
        }

        /// <summary>
        /// Sets the uniform named by <paramref name="uniformName"/> to <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The boolean.</param>
        /// <param name="uniformName">The name of the uniform variable.</param>
        protected void SetBoolean(bool value, string uniformName)
        {
            Enable();

            var variableHandle = this.GL.GetUniformLocation(this.NativeShaderProgramID, uniformName);
            this.GL.Uniform1(variableHandle, value ? 1 : 0);
        }

        /// <summary>
        /// Sets the uniform named by <paramref name="uniformName"/> to <paramref name="matrix"/>.
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <param name="uniformName">The name of the uniform variable.</param>
        /// <param name="shouldTranspose">Whether or not the matrix should be transposed.</param>
        protected void SetMatrix(Matrix4x4 matrix, string uniformName, bool shouldTranspose = false)
        {
            Enable();

            var variableHandle = this.GL.GetUniformLocation(this.NativeShaderProgramID, uniformName);
            unsafe
            {
                this.GL.UniformMatrix4(variableHandle, 1, shouldTranspose, &matrix.M11);
            }
        }

        /// <summary>
        /// Sets the uniform named by <paramref name="uniformName"/> to <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="uniformName">The name of the uniform variable.</param>
        protected void SetInteger(int value, string uniformName)
        {
            Enable();

            var variableHandle = this.GL.GetUniformLocation(this.NativeShaderProgramID, uniformName);
            this.GL.Uniform1(variableHandle, value);
        }

        /// <summary>
        /// Sets the uniform named by <paramref name="uniformName"/> to <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="uniformName">The name of the uniform variable.</param>
        protected void SetVector4(Vector4 value, string uniformName)
        {
            Enable();

            var variableHandle = this.GL.GetUniformLocation(this.NativeShaderProgramID, uniformName);
            this.GL.Uniform4(variableHandle, value);
        }

        /// <summary>
        /// Sets the uniform named by <paramref name="uniformName"/> to <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="uniformName">The name of the uniform variable.</param>
        protected void SetFloat(float value, string uniformName)
        {
            Enable();

            var variableHandle = this.GL.GetUniformLocation(this.NativeShaderProgramID, uniformName);
            this.GL.Uniform1(variableHandle, value);
        }

        /// <summary>
        /// Binds a texture to a sampler in the shader. The name of the sampler must match one of the values in
        /// <see cref="TextureUniform"/>.
        /// </summary>
        /// <param name="textureUnit">The texture unit to bind the texture to.</param>
        /// <param name="uniform">The uniform where the texture should be bound.</param>
        /// <param name="texture">The texture to bind.</param>
        public void BindTexture2D(TextureUnit textureUnit, TextureUniform uniform, Texture2D texture)
        {
            if (texture is null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            Enable();

            var textureVariableHandle = this.GL.GetUniformLocation(this.NativeShaderProgramID, uniform.ToString());
            this.GL.Uniform1(textureVariableHandle, (int)uniform);

            this.GL.ActiveTexture(textureUnit);
            texture.Bind();
        }

        /// <summary>
        /// Gets the name of vertex shader in the resources. This will be used with the resource path to load the
        /// source. Do not include any extensions. Folder prefixes are optional.
        ///
        /// Valid: WorldModelVertex, Plain2D.Plain2DVertex
        /// Invalid: Resources.Content.Shaders.WorldModelVertex.glsl.
        /// </summary>
        protected abstract string VertexShaderResourceName { get; }

        /// <summary>
        /// Gets the name of the fragment shader in the resources. This will be used with the resource path to load the
        /// source. Do not include any extensions. Folder prefixes are optional.
        ///
        /// Valid: WorldModelFragment, Plain2D.Plain2DFragment
        /// Invalid: Resources.Content.Shaders.WorldModelFragment.glsl.
        /// </summary>
        protected abstract string FragmentShaderResourceName { get; }

        /// <summary>
        /// Gets the name of the fragment shader in the resources. This value is optional, and will be used with the
        /// resource path to load the source. Do not include any extensions. Folder prefixes are optional.
        ///
        /// Valid: WorldModelGeometry, Plain2D.Plain2DGeometry
        /// Invalid: Resources.Content.Shaders.WorldModelGeometry.glsl.
        /// </summary>
        protected abstract string? GeometryShaderResourceName { get; }

        /// <summary>
        /// Links a vertex shader and a fragment shader into a complete shader program.
        /// </summary>
        /// <param name="vertexShaderID">The native handle to the object code of the vertex shader.</param>
        /// <param name="fragmentShaderID">The native handle to the object code of the fragment shader.</param>
        /// <param name="geometryShaderID">
        /// Optional. The native handle to the object code of the geometry shader.
        /// </param>
        /// <returns>A native handle to a shader program.</returns>
        /// <exception cref="ShaderLinkingException">Thrown if the linking fails.</exception>
        private uint LinkShader(uint vertexShaderID, uint fragmentShaderID, uint geometryShaderID = 0)
        {
            Log.Info("Linking shader program...");
            var program = this.GL.CreateProgram();

            this.GL.AttachShader(program, vertexShaderID);
            this.GL.AttachShader(program, fragmentShaderID);

            if (geometryShaderID > 0)
            {
                this.GL.AttachShader(program, geometryShaderID);
            }

            this.GL.LinkProgram(program);

            this.GL.GetProgram(program, ProgramPropertyARB.LinkStatus, out var result);
            var linkingSucceeded = result > 0;

            this.GL.GetProgram(program, ProgramPropertyARB.InfoLogLength, out var linkingLogLength);
            this.GL.GetProgramInfoLog(program, out var linkingLog);

            // Clean up the shader source code and unlinked object files from graphics memory
            this.GL.DetachShader(program, vertexShaderID);
            this.GL.DetachShader(program, fragmentShaderID);

            this.GL.DeleteShader(vertexShaderID);
            this.GL.DeleteShader(fragmentShaderID);

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
        private uint CompileShader(ShaderType shaderType, string shaderSource)
        {
            if (string.IsNullOrEmpty(shaderSource))
            {
                throw new ArgumentNullException
                (
                    nameof(shaderSource),
                    "No shader source was given. Check that the names are correct."
                );
            }

            var shader = this.GL.CreateShader(shaderType);

            Log.Info("Compiling shader...");
            this.GL.ShaderSource(shader, shaderSource);
            this.GL.CompileShader(shader);

            this.GL.GetShader(shader, ShaderParameterName.CompileStatus, out var result);
            var compilationSucceeded = result > 0;

            this.GL.GetShader(shader, ShaderParameterName.InfoLogLength, out var compilationLogLength);
            this.GL.GetShaderInfoLog(shader, out var compilationLog);

            if (!compilationSucceeded)
            {
                this.GL.DeleteShader(shader);

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
        private static string? GetShaderSource(string shaderResourceName)
        {
            var baseDirectory = "Everlook.Content.Shaders";
            var shaderSource = ResourceManager.LoadStringResource($"{baseDirectory}.{shaderResourceName}.glsl");

            if (shaderSource is null)
            {
                return null;
            }

            var shaderSourceWithResolvedIncludes = GLSLPreprocessor.ProcessIncludes(shaderSource, baseDirectory);

            return shaderSourceWithResolvedIncludes;
        }

        /// <summary>
        /// Enables the shader, making it the current one.
        /// </summary>
        public void Enable()
        {
            this.GL.UseProgram(this.NativeShaderProgramID);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.GL.DeleteProgram(this.NativeShaderProgramID);
            this.NativeShaderProgramID = 0;
        }
    }
}
