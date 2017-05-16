using System;
using System.Collections.Generic;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Warcraft.Core.Extensions;
using Warcraft.Core.Structures;

namespace Everlook.Viewport.Rendering
{
	/// <summary>
	/// Represents a renderable 2D image, and contains common functionality required to render one.
	/// </summary>
	public abstract class RenderableImage : IRenderable
	{
		/// <summary>
		/// Gets a value indicating whether this instance uses static rendering; that is,
		/// a single frame is rendered and then reused. Useful as an optimization for images.
		/// </summary>
		/// <value>true</value>
		/// <c>false</c>
		public bool IsStatic => true;

		/// <summary>
		/// The model transformation of the image. Used for moving and zooming.
		/// </summary>
		protected Transform ImageTransform { get; set; }

		/// <summary>
		/// Returns a value which represents whether or not the current renderable has been initialized.
		/// </summary>
		public bool IsInitialized { get; set; }

		/// <summary>
		/// The native OpenGL ID for the vertex buffer.
		/// </summary>
		protected int VertexBufferID;

		/// <summary>
		/// The native OpenGL ID for the UV coordinate buffer.
		/// </summary>
		protected int UVBufferID;

		/// <summary>
		/// The native OpenGL ID for the vertex index buffer.
		/// </summary>
		protected int VertexIndexBufferID;

		/// <summary>
		/// The native OpenGL ID for the image on the GPU.
		/// </summary>
		protected int GLTextureID;

		/// <summary>
		/// The native OpenGL ID for the unlit 2D shader.
		/// </summary>
		protected int ImageShaderID;

		/// <summary>
		/// The path to the encapsulated texture in the package group.
		/// </summary>
		protected string TexturePath;

		/// <summary>
		/// A reference to the global shader cache.
		/// </summary>
		protected readonly RenderCache Cache = RenderCache.Instance;

		/// <summary>
		/// The projection method to use for this renderable object. Typically, this is Orthographic
		/// or Perspective.
		/// </summary>
		public ProjectionType Projection => ProjectionType.Orthographic;

		/// <summary>
		/// A vector that is multiplied with the final texture sampling.
		/// </summary>
		public Vector4 ChannelMask;

		public uint MipCount => GetNumReasonableMipLevels();

		/// <summary>
		/// TODO: Put this in Warcraft.Core instead
		/// </summary>
		/// <returns></returns>
		private uint GetNumReasonableMipLevels()
		{
			uint smallestXRes = GetResolution().X;
            uint smallestYRes = GetResolution().Y;

            uint mipLevels = 0;
            while (smallestXRes > 1 && smallestYRes > 1)
            {
                // Bisect the resolution using the current number of mip levels.
                smallestXRes = smallestXRes / (uint)Math.Pow(2, mipLevels);
                smallestYRes = smallestYRes / (uint)Math.Pow(2, mipLevels);

                ++mipLevels;
            }

            return mipLevels.Clamp<uint>(0, 15);
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
			this.GLTextureID = LoadCachedTexture();

			// Use cached shaders whenever possible
			this.ImageShaderID = LoadCachedShader();

			this.ImageTransform = new Transform(
				new Vector3(0.0f, 0.0f, 0.0f),
				Quaternion.FromAxisAngle(Vector3.UnitX, 0.0f),
				new Vector3(1.0f, 1.0f, 1.0f));

			this.ChannelMask = Vector4.One;

			this.IsInitialized = true;
		}

		/// <summary>
		/// Loads or creates a cached texture from the global texture cache using the path the image
		/// was constructed with as a key.
		/// </summary>
		/// <returns></returns>
		protected abstract int LoadCachedTexture();

		/// <summary>
		/// Loads or creates a cached unlit 2D shader.
		/// </summary>
		/// <returns></returns>
		protected int LoadCachedShader()
		{
			if (this.Cache.HasCachedShader(EverlookShader.Plain2D))
			{
				return this.Cache.GetCachedShader(EverlookShader.Plain2D);
			}

			return this.Cache.CreateCachedShader(EverlookShader.Plain2D);
		}

		/// <summary>
		/// Renders the current object in the current OpenGL context.
		/// </summary>
		public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera)
		{
			if (!this.IsInitialized)
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

			// Set the channel mask
			int channelMaskVariableHandle = GL.GetUniformLocation(this.ImageShaderID, "channelMask");
			GL.Uniform4(channelMaskVariableHandle, this.ChannelMask);

			// Set the texture ID as a uniform sampler in unit 0
			GL.ActiveTexture(TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2D, this.GLTextureID);
			int textureVariableHandle = GL.GetUniformLocation(this.ImageShaderID, "imageTextureSampler");
			int textureUnit = 0;
			GL.Uniform1(textureVariableHandle, 1, ref textureUnit);

			// Set the model view matrix
			Matrix4 modelViewProjection = this.ImageTransform.GetModelMatrix() * viewMatrix * projectionMatrix;

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
		/// Gets the resolution of the encapsulated image.
		/// </summary>
		/// <returns></returns>
		protected abstract Resolution GetResolution();

		/// <summary>
		/// Generates the four corner vertices of the encapsulated image.
		/// </summary>
		/// <returns></returns>
		protected int GenerateVertices()
		{
			// Generate vertex positions
			uint halfWidth = (uint) (GetResolution().X / 2);
			uint halfHeight = (uint) (GetResolution().Y / 2);

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

		/// <summary>
		/// Generates a vertex index buffer for the four corner vertices.
		/// </summary>
		/// <returns></returns>
		protected static int GenerateVertexIndices()
		{
			// Generate vertex indices
			List<ushort> vertexIndices = new List<ushort> {1, 0, 2, 2, 3, 1};

			int vertexIndicesID;
			GL.GenBuffers(1, out vertexIndicesID);

			GL.BindBuffer(BufferTarget.ElementArrayBuffer, vertexIndicesID);
			GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(vertexIndices.Count * sizeof(ushort)), vertexIndices.ToArray(), BufferUsageHint.StaticDraw);

			return vertexIndicesID;
		}

		/// <summary>
		/// Generates a UV coordinate buffer for the four corner vertices.
		/// </summary>
		/// <returns></returns>
		protected static int GenerateTextureCoordinates()
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
		/// Releases all resources used by the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> so the garbage collector can reclaim the memory that the
		/// <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> was occupying.</remarks>
		public virtual void Dispose()
		{
			GL.DeleteBuffer(this.VertexBufferID);
			GL.DeleteBuffer(this.VertexIndexBufferID);
			GL.DeleteBuffer(this.UVBufferID);
		}
	}
}