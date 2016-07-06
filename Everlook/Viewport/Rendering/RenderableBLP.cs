//
//  RenderableBLP.cs
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
using System.IO;
using System.Reflection;
using Everlook.Rendering.Interfaces;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Warcraft.BLP;
using Warcraft.Core;

namespace Everlook.Rendering
{
	/// <summary>
	/// Represents a renderable BLP image.
	/// </summary>
	public sealed class RenderableBLP : IRenderable
	{
		/// <summary>
		/// The image contained by this instance.
		/// </summary>
		private readonly BLP Image;

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

		public bool IsInitialized
		{
			get;
			set;
		}

		private int VertexBufferID;
		private int UVBufferID;
		private int VertexIndexBufferID;

		private int GLTextureID;
		private int ImageShaderID;

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Rendering.RenderableBLP"/> class.
		/// </summary>
		/// <param name="inImage">In image.</param>
		public RenderableBLP(BLP inImage)
		{
			this.Image = inImage;
			this.IsInitialized = false;

			Initialize();
		}

		public void Initialize()
		{
			this.VertexBufferID = GenerateVertices();
			this.VertexIndexBufferID = GenerateVertexIndices();
			this.UVBufferID = GenerateTextureCoordinates();
			this.GLTextureID = GenerateImageTexture();
			this.ImageShaderID = LoadPlainImageShader();

			IsInitialized = true;
		}

		public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix)
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
			Matrix4 modelScale = Matrix4.Scale(new Vector3(1.0f, 1.0f, 1.0f));
			//Matrix4 modelViewProjection = modelScale * modelTranslation * viewMatrix * projectionMatrix;
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

		private int GenerateVertices()
		{
			// Generate vertex positions
			uint halfWidth = Image.GetResolution().X / 2;
			uint halfHeight = Image.GetResolution().Y / 2;

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
				1, 0,
				1, 1,
				0, 0,
				0, 1
			};

			// Buffer the generated UV coordinates in the GPU
			int bufferID;
			GL.GenBuffers(1, out bufferID);

			GL.BindBuffer(BufferTarget.ArrayBuffer, bufferID);
			GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(textureCoordinates.Count * sizeof(float)), textureCoordinates.ToArray(), BufferUsageHint.StaticDraw);

			return bufferID;
		}

		private int GenerateImageTexture()
		{
			// Generate texture data
			int textureID = GL.GenTexture();
			GL.BindTexture(TextureTarget.Texture2D, textureID);

			if (Image.GetCompressionType() == TextureCompressionType.DXTC)
			{
				// Load the set of raw compressed mipmaps
				for (uint i = 0; i < Image.GetMipMapCount(); ++i)
				{
					byte[] compressedMipMap = Image.GetRawMipMap(i);
					Resolution mipResolution = Image.GetMipLevelResolution(i);

					PixelInternalFormat compressionFormat;
					switch (Image.GetPixelFormat())
					{
						case BLPPixelFormat.Pixel_DXT1:
						{
							compressionFormat = PixelInternalFormat.CompressedRgbaS3tcDxt1Ext;
							break;
						}
						case BLPPixelFormat.Pixel_DXT3:
						{
							compressionFormat = PixelInternalFormat.CompressedRgbaS3tcDxt3Ext;
							break;
						}
						case BLPPixelFormat.Pixel_DXT5:
						{
							compressionFormat = PixelInternalFormat.CompressedRgbaS3tcDxt5Ext;
							break;
						}
						default:
						{
							throw new ArgumentException($"Image format (DXTC) did not match pixel format: {Image.GetPixelFormat()}", nameof(Image));
						}
					}

					// Load the mipmap into the texture
					GL.CompressedTexImage2D(TextureTarget.Texture2D, (int)i,
						compressionFormat,
						(int)mipResolution.X,
						(int)mipResolution.Y,
						0,
						compressedMipMap.Length,
						compressedMipMap);
				}
			}
			else
			{
				// Extract raw RGB data from the largest bitmap
				Bitmap pixels = Image.GetMipMap(0);
				byte[] pixelData = new byte[pixels.Height * pixels.Width * 4];

				for (int y = 0; y < pixels.Height; ++y)
				{
					for (int x = 0; x < pixels.Width; ++x)
					{
						pixelData[(pixels.Height * y) * x] = pixels.GetPixel(x, y).R;
						pixelData[(pixels.Height * y) * x + 1] = pixels.GetPixel(x, y).G;
						pixelData[(pixels.Height * y) * x + 2] = pixels.GetPixel(x, y).B;
						pixelData[(pixels.Height * y) * x + 3] = pixels.GetPixel(x, y).A;
					}
				}

				GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, pixels.Width, pixels.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixelData);
				GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
			}

			// Use linear mipmapped filtering
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);

			// Repeat the texture by tiling it
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

			int maximalMipLevel = Image.GetMipMapCount() == 0 ? 0 : Image.GetMipMapCount() - 1;
			GL.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, ref maximalMipLevel);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);

			return textureID;
		}

		private static int LoadPlainImageShader()
		{
			int vertexShaderID = GL.CreateShader(ShaderType.VertexShader);
			int fragmentShaderID = GL.CreateShader(ShaderType.FragmentShader);

			string vertexShaderSourceCode;
			using (Stream shaderStream =
					Assembly.GetExecutingAssembly().GetManifestResourceStream("Everlook.Content.Shaders.Adapted.PlainImage.PlainImageVertex.glsl"))
			{
				if (shaderStream == null)
				{
					return -1;
				}

				using (StreamReader sr = new StreamReader(shaderStream))
				{
					vertexShaderSourceCode = sr.ReadToEnd();
				}
			}

			string fragmentShaderSourceCode;
			using (Stream shaderStream =
					Assembly.GetExecutingAssembly().GetManifestResourceStream("Everlook.Content.Shaders.Adapted.PlainImage.PlainImageFragment.glsl"))
			{
				if (shaderStream == null)
				{
					return -1;
				}

				using (StreamReader sr = new StreamReader(shaderStream))
				{
					fragmentShaderSourceCode = sr.ReadToEnd();
				}
			}

			int result = 0;
			int compilationLogLength;

			Console.WriteLine("Compiling vertex shader...");
			GL.ShaderSource(vertexShaderID, vertexShaderSourceCode);
			GL.CompileShader(vertexShaderID);

			GL.GetShader(vertexShaderID, ShaderParameter.CompileStatus, out result);
			GL.GetShader(vertexShaderID, ShaderParameter.InfoLogLength, out compilationLogLength);

			if (compilationLogLength > 0)
			{
				string compilationLog;
				GL.GetShaderInfoLog(vertexShaderID, out compilationLog);

				Console.WriteLine(compilationLog);
			}

			Console.WriteLine("Compiling fragment shader...");
			GL.ShaderSource(fragmentShaderID, fragmentShaderSourceCode);
			GL.CompileShader(fragmentShaderID);

			GL.GetShader(fragmentShaderID, ShaderParameter.CompileStatus, out result);
			GL.GetShader(fragmentShaderID, ShaderParameter.InfoLogLength, out compilationLogLength);

			if (compilationLogLength > 0)
			{
				string compilationLog;
				GL.GetShaderInfoLog(fragmentShaderID, out compilationLog);

				Console.WriteLine(compilationLog);
			}


			Console.WriteLine("Linking shader program...");
			int shaderProgramID = GL.CreateProgram();

			GL.AttachShader(shaderProgramID, vertexShaderID);
			GL.AttachShader(shaderProgramID, fragmentShaderID);
			GL.LinkProgram(shaderProgramID);

			GL.GetProgram(shaderProgramID, ProgramParameter.LinkStatus, out result);
			GL.GetProgram(shaderProgramID, ProgramParameter.InfoLogLength, out compilationLogLength);

			if (compilationLogLength > 0)
			{
				string compilationLog;
				GL.GetProgramInfoLog(shaderProgramID, out compilationLog);

				Console.WriteLine(compilationLog);
			}

			// Clean up the shader source code and unlinked object files from graphics memory
			GL.DetachShader(shaderProgramID, vertexShaderID);
			GL.DetachShader(shaderProgramID, fragmentShaderID);

			GL.DeleteShader(vertexShaderID);
			GL.DeleteShader(fragmentShaderID);

			return shaderProgramID;
		}

		/// <summary>
		/// Releases all resource used by the <see cref="Everlook.Rendering.RenderableBLP"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Everlook.Rendering.RenderableBLP"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="Everlook.Rendering.RenderableBLP"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="Everlook.Rendering.RenderableBLP"/> so the garbage collector can reclaim the memory that the
		/// <see cref="Everlook.Rendering.RenderableBLP"/> was occupying.</remarks>
		public void Dispose()
		{
			GL.DeleteBuffers(1, ref this.VertexBufferID);
			GL.DeleteBuffers(1, ref this.VertexIndexBufferID);
			GL.DeleteBuffers(1, ref this.UVBufferID);

			GL.DeleteTexture(this.GLTextureID);

			GL.DeleteProgram(this.ImageShaderID);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="Everlook.Rendering.RenderableBLP"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (IsStatic.GetHashCode() + Image.GetHashCode()).GetHashCode();
		}
	}
}

