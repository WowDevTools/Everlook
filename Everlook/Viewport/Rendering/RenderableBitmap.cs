//
//  RenderableBitmap.cs
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
using System;
using System.Collections.Generic;
using System.Drawing;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SlimTK;

namespace Everlook.Viewport.Rendering
{
	// TODO: Major refactoring required - merge most functionality of RenderableBLP and RenderableBitmap into new class RenderableImage
	/// <summary>
	/// Encapsulated a standard bitmap as a renderable object.
	/// </summary>
	public sealed class RenderableBitmap : IRenderable
	{
		/// <summary>
		/// Gets a value indicating whether this instance uses static rendering; that is,
		/// a single frame is rendered and then reused. Useful as an optimization for images.
		/// </summary>
		/// <value>true</value>
		/// <c>false</c>
		public bool IsStatic
		{
			get
			{
				return true;
			}
		}

		/// <summary>
		/// Returns a value which represents whether or not the current renderable has been initialized.
		/// </summary>
		public bool IsInitialized { get; set; }

		private int VertexBufferID;
		private int UVBufferID;
		private int VertexIndexBufferID;

		private int GLTextureID;
		private int ImageShaderID;

		private readonly string TexturePath;

		private readonly RenderCache Cache = RenderCache.Instance;

		/// <summary>
		/// The projection method to use for this renderable object. Typically, this is Orthographic
		/// or Perspective.
		/// </summary>
		public ProjectionType Projection
		{
			get
			{
				return ProjectionType.Orthographic;
			}
		}

		/// <summary>
		/// Initializes the required data for rendering.
		/// </summary>
		public void Initialize()
		{
			this.VertexBufferID = GenerateVertices();
			this.VertexIndexBufferID = GenerateVertexIndices();
			this.UVBufferID = GenerateTextureCoordinates();

			// Use cached textures whenever possible
			if (Cache.HasCachedTextureForPath(this.TexturePath))
			{
				this.GLTextureID = Cache.GetCachedTexture(this.TexturePath);
			}
			else
			{
				this.GLTextureID = Cache.CreateCachedTexture(this.Image, this.TexturePath);
			}

			// Use cached shaders whenever possible
			if (Cache.HasCachedShader(EverlookShader.Plain2D))
			{
				this.ImageShaderID = Cache.GetCachedShader(EverlookShader.Plain2D);
			}
			else
			{
				this.ImageShaderID = Cache.CreateCachedShader(EverlookShader.Plain2D);
			}

			IsInitialized = true;
		}

		/// <summary>
		/// Renders the current object in the current OpenGL context.
		/// </summary>
		public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera)
		{
			if (!IsInitialized)
			{
				return;
			}

			GL.UseProgram(this.ImageShaderID);

			// Render the object
			// Send the vertices to the shader
			GL.EnableVertexAttribArray(0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, this.VertexBufferID);
			GL.VertexAttribPointer(
				0,
				2,
				VertexAttribPointerType.Float,
				false,
				0,
				0);

			// Send the UV coordinates to the shader
			GL.EnableVertexAttribArray(1);
			GL.BindBuffer(BufferTarget.ArrayBuffer, this.UVBufferID);
			GL.VertexAttribPointer(
				1,
				2,
				VertexAttribPointerType.Float,
				false,
				0,
				0);

			// Set the texture ID as a uniform sampler in unit 0
			GL.ActiveTexture(TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2D, this.GLTextureID);
			int textureVariableHandle = GL.GetUniformLocation(this.ImageShaderID, "imageTextureSampler");
			int textureUnit = 0;
			GL.Uniform1(textureVariableHandle, 1, ref textureUnit);

			// Set the model view matrix
			Matrix4 modelTranslation = Matrix4.CreateTranslation(new Vector3(0.0f, 0.0f, 0.0f));
			Matrix4 modelScale = Matrix4.CreateScale(new Vector3(1.0f, 1.0f, 1.0f));
			Matrix4 modelViewProjection = modelScale * modelTranslation * viewMatrix * projectionMatrix;

			// Send the model matrix to the shader
			int projectionShaderVariableHandle = GL.GetUniformLocation(this.ImageShaderID, "ModelViewProjection");
			GL.UniformMatrix4(projectionShaderVariableHandle, false, ref modelViewProjection);

			// Finally, draw the image
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.VertexIndexBufferID);
			GL.DrawElements(BeginMode.Triangles, 6, DrawElementsType.UnsignedShort, 0);

			// Release the attribute arrays
			GL.DisableVertexAttribArray(0);
			GL.DisableVertexAttribArray(1);
		}

		/// <summary>
		/// The encapsulated image.
		/// </summary>
		/// <value>The image.</value>
		public Bitmap Image
		{
			get;
			private set;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> class.
		/// </summary>
		/// <param name="inImage">In image.</param>
		public RenderableBitmap(Bitmap inImage, string texturePath)
		{
			this.Image = inImage;
			this.IsInitialized = false;
			this.TexturePath = texturePath;

			Initialize();
		}

		private int GenerateVertices()
		{
			// Generate vertex positions
			uint halfWidth = (uint) (Image.Width / 2);
			uint halfHeight = (uint) (Image.Height / 2);

			List<float> vertexPositions = new List<float>
			{
				-halfWidth, halfHeight,
				halfWidth, halfHeight,
				-halfWidth, -halfHeight,
				halfWidth, -halfHeight
			};

			// Buffer the generated vertices in the GPU
			int bufferID;
			GL.GenBuffers(1, out bufferID);

			GL.BindBuffer(BufferTarget.ArrayBuffer, bufferID);
			GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(vertexPositions.Count * sizeof(float)), vertexPositions.ToArray(), BufferUsageHint.StaticDraw);

			return bufferID;
		}

		private static int GenerateVertexIndices()
		{
			// Generate vertex indices
			List<ushort> vertexIndices = new List<ushort> {1, 0, 2, 2, 3, 1};

			int vertexIndicesID;
			GL.GenBuffers(1, out vertexIndicesID);

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, vertexIndicesID);
			GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(vertexIndices.Count * sizeof(ushort)), vertexIndices.ToArray(), BufferUsageHint.StaticDraw);

			return vertexIndicesID;
		}

		private static int GenerateTextureCoordinates()
		{
			// Generate UV coordinates
			List<float> textureCoordinates = new List<float>
			{
				0, 0,
				1, 0,
				0, 1,
				1, 1
			};

			// Buffer the generated UV coordinates in the GPU
			int bufferID;
			GL.GenBuffers(1, out bufferID);

			GL.BindBuffer(BufferTarget.ArrayBuffer, bufferID);
			GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(textureCoordinates.Count * sizeof(float)), textureCoordinates.ToArray(), BufferUsageHint.StaticDraw);

			return bufferID;
		}

		/// <summary>
		/// Releases all resource used by the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> so the garbage collector can reclaim the memory that the
		/// <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> was occupying.</remarks>
		public void Dispose()
		{
			Image.Dispose();
			Image = null;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (IsStatic.GetHashCode() + Image.GetHashCode()).GetHashCode();
		}
	}
}

