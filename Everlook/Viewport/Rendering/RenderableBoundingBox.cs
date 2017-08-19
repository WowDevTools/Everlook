//
//  RenderableBoundingBox.cs
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
using System.Linq;
using Everlook.Exceptions.Shader;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using Everlook.Viewport.Rendering.Shaders;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using SlimTK;
using Warcraft.Core.Extensions;

namespace Everlook.Viewport.Rendering
{
	/// <summary>
	/// Wraps a <see cref="BoundingBox"/> as a renderable in-world actor.
	/// </summary>
	public sealed class RenderableBoundingBox : IActor
	{
		private readonly BoundingBoxShader BoxShader;

		/// <inheritdoc />
		public Transform ActorTransform { get; set; }

		/// <inheritdoc />
		public bool IsStatic => false;

		/// <inheritdoc />
		public bool IsInitialized { get; set; }

		/// <inheritdoc />
		public ProjectionType Projection => ProjectionType.Perspective;

		/// <summary>
		/// Gets or sets the colour of the bounding box's lines.
		/// </summary>
		public Color4 LineColour { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether this object has been disposed.
		/// </summary>
		private bool IsDisposed { get; set; }

		private BoundingBox BoundingBoxData;
		private Buffer<Vector3> VertexBuffer;
		private Buffer<byte> VertexIndexesBuffer;

		/// <summary>
		/// Initializes a new instance of the <see cref="RenderableBoundingBox"/> class. The bounds data is taken from
		/// the given <see cref="BoundingBox"/>, and the world translation is set to <paramref name="transform"/>.
		/// </summary>
		/// <param name="boundingBox">The BoundingBox to get data from.</param>
		/// <param name="transform">The world transform of the box.</param>
		public RenderableBoundingBox(BoundingBox boundingBox, Transform transform)
		{
			this.BoundingBoxData = boundingBox;
			this.LineColour = Color4.LimeGreen;

			this.ActorTransform = transform;
			this.BoxShader = RenderCache.Instance.GetShader(EverlookShader.BoundingBox) as BoundingBoxShader;
		}

		/// <inheritdoc />
		public void Initialize()
		{
			ThrowIfDisposed();

			if (this.BoxShader == null)
			{
				throw new ShaderNullException(typeof(BoundingBoxShader));
			}

			this.VertexBuffer = new Buffer<Vector3>(BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw)
			{
				Data = this.BoundingBoxData.GetCorners().ToArray()
			};

			byte[] boundingBoxIndexValues =
			{
				0, 1, 1, 2,
				2, 3, 3, 0,
				0, 4, 4, 7,
				7, 3, 2, 6,
				6, 7, 6, 5,
				5, 4, 5, 1
			};

			this.VertexIndexesBuffer = new Buffer<byte>(BufferTarget.ElementArrayBuffer, BufferUsageHint.StaticDraw)
			{
				Data = boundingBoxIndexValues
			};

			this.IsInitialized = true;
		}

		/// <inheritdoc />
		public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera)
		{
			ThrowIfDisposed();

			Matrix4 modelViewProjection = this.ActorTransform.GetModelMatrix() * viewMatrix * projectionMatrix;

			this.BoxShader.Enable();
			this.BoxShader.SetMVPMatrix(modelViewProjection);
			this.BoxShader.SetLineColour(this.LineColour);

			GL.Disable(EnableCap.CullFace);
			GL.Disable(EnableCap.DepthTest);

			// Render the object
			// Send the vertices to the shader
			GL.EnableVertexAttribArray(0);
			this.VertexBuffer.Bind();
			GL.VertexAttribPointer
			(
				0,
				3,
				VertexAttribPointerType.Float,
				false,
				0,
				0
			);

			this.VertexIndexesBuffer.Bind();

			// Now draw the box
			GL.DrawRangeElements
			(
				PrimitiveType.LineLoop,
				0,
				23,
				24,
				DrawElementsType.UnsignedByte,
				new IntPtr(0)
			);

			GL.DisableVertexAttribArray(0);
		}

		/// <summary>
		/// Throws an <see cref="ObjectDisposedException"/> if the object has been disposed.
		/// </summary>
		/// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
		private void ThrowIfDisposed()
		{
			if (this.IsDisposed)
			{
				throw new ObjectDisposedException(ToString());
			}
		}

		/// <summary>
		/// Releases the underlying data buffers for this bounding box.
		/// </summary>
		public void Dispose()
		{
			this.VertexBuffer.Dispose();
			this.VertexIndexesBuffer.Dispose();
		}
	}
}
