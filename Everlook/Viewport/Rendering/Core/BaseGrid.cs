//
//  BaseGrid.cs
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
using System.Collections.Generic;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Interfaces;
using Everlook.Viewport.Rendering.Shaders;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace Everlook.Viewport.Rendering.Core
{
	/// <summary>
	/// Represents a base grid in the viewport, centered at the world and spanning 100x100 world units, subdivided
	/// into squares.
	/// </summary>
	public class BaseGrid : IActor, IRenderable
	{
		/// <summary>
		/// The size of one side of the grid.
		/// </summary>
		private const float GridSize = 10.0f;

		/// <summary>
		/// The number of quads on one edge of the grid.
		/// </summary>
		private const int Quads = 5;

		/// <inheritdoc />
		public Transform ActorTransform { get; set; }

		/// <inheritdoc />
		public bool IsStatic => false;

		/// <inheritdoc />
		public bool IsInitialized { get; set; }

		/// <inheritdoc />
		public ProjectionType Projection => ProjectionType.Perspective;

		private Buffer<float> Vertices;
		private Buffer<ushort> VertexIndexes;

		/// <summary>
		/// Gets or sets a value indicating whether this object has been disposed.
		/// </summary>
		private bool IsDisposed { get; set; }

		private readonly BaseGridShader Shader;

		/// <summary>
		/// Initializes a new instance of the <see cref="BaseGrid"/> class.
		/// </summary>
		public BaseGrid()
		{
			this.Shader = RenderCache.Instance.GetShader(EverlookShader.BaseGrid) as BaseGridShader;
			this.ActorTransform = new Transform();

			this.IsInitialized = false;
		}

		/// <inheritdoc />
		public void Initialize()
		{
			List<float> vertices = new List<float>();
			List<ushort> vertexIndexes = new List<ushort>();
			float quadSize = GridSize / Quads;

			// Generate opposing edges of vertices on the X/Z plane
			for (int x = 0; x <= Quads; ++x)
			{
				float offsetX = (-GridSize / 2) + (x * quadSize);

				vertices.AddRange(new[] { offsetX, 0.0f, GridSize / 2 });
				vertices.AddRange(new[] { offsetX, 0.0f, -GridSize / 2 });

				vertexIndexes.AddRange(new[] { (ushort)(((vertices.Count - 3) / 3) - 1), (ushort)((vertices.Count / 3) - 1) });
			}

			// Fill in the missing opposing vertices on the Z axis
			for (int z = 1; z < Quads; ++z)
			{
				float offsetZ = (-GridSize / 2) + (z * quadSize);

				vertices.AddRange(new[] { -GridSize / 2, 0.0f, offsetZ });
				vertices.AddRange(new[] { GridSize / 2, 0.0f, offsetZ });

				vertexIndexes.AddRange(new[] { (ushort)(((vertices.Count - 3) / 3) - 1), (ushort)((vertices.Count / 3) - 1) });
			}

			// Manually link the outer edges on the Z axis
			vertexIndexes.AddRange(new ushort[] { 0, Quads * 2 });
			vertexIndexes.AddRange(new ushort[] { 1, (Quads * 2) + 1 });

			this.Vertices = new Buffer<float>(BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw)
			{
				Data = vertices.ToArray()
			};

			// Attach the vertex pointer
			this.Vertices.AttachAttributePointer
			(
				new VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 0, 0)
			);

			this.VertexIndexes = new Buffer<ushort>(BufferTarget.ElementArrayBuffer, BufferUsageHint.StaticDraw)
			{
				Data = vertexIndexes.ToArray()
			};
		}

		/// <inheritdoc />
		public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera)
		{
			ThrowIfDisposed();

			GL.Enable(EnableCap.Blend);
			GL.Enable(EnableCap.DepthTest);
			GL.Disable(EnableCap.CullFace);

			this.Vertices.Bind();
			this.Vertices.EnableAttributes();
			this.VertexIndexes.Bind();

			Matrix4 modelViewProjection = this.ActorTransform.GetModelMatrix() * viewMatrix * projectionMatrix;

			// Set the default line colour (a light gray)
			this.Shader.SetLineColour(new Color4(64, 64, 64, 255));
			this.Shader.SetMVPMatrix(modelViewProjection);

			int lineCount = ((Quads * 2) + 2) * 2;
			GL.DrawElements
			(
				PrimitiveType.Lines,
				lineCount,
				DrawElementsType.UnsignedShort,
				IntPtr.Zero
			);

			this.Vertices.DisableAttributes();
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

		/// <inheritdoc />
		public void Dispose()
		{
			this.IsDisposed = true;
			this.Vertices.Dispose();
			this.VertexIndexes.Dispose();
		}
	}
}
