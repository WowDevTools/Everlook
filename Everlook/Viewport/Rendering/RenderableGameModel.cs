//
//  RenderableGameModel.cs
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
using System.Linq;
using Everlook.Package;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using Everlook.Viewport.Rendering.Shaders;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SlimTK;
using Warcraft.Core.Extensions;
using Warcraft.MDX;
using Warcraft.MDX.Geometry.Skin;

namespace Everlook.Viewport.Rendering
{
	/// <summary>
	/// Represents a renderable Game Object Model.
	/// </summary>
	public sealed class RenderableGameModel : IRenderable, IDefaultCameraPositionProvider
	{
		/// <summary>
		/// Gets a value indicating whether this instance uses static rendering; that is,
		/// a single frame is rendered and then reused. Useful as an optimization for images.
		/// </summary>
		/// <value>true</value>
		/// <c>false</c>
		public bool IsStatic => false;

		/// <summary>
		/// Gets the projection method to use for this renderable object. Typically, this is Orthographic
		/// or Perspective.
		/// </summary>
		public ProjectionType Projection => ProjectionType.Perspective;

		/// <summary>
		/// Gets the default camera position for this renderable.
		/// </summary>
		public Vector3 DefaultCameraPosition
		{
			get
			{
				if (!this.IsInitialized)
				{
					return Vector3.Zero;
				}

				return
				(
					this.ActorTransform.GetModelMatrix() *
					new Vector4
					(
						this.Model.BoundingBox.GetCenterCoordinates().AsOpenTKVector(),
						1.0f
					)
				)
				.Xyz;
			}
		}

		/// <summary>
		/// The model contained by this renderable game object.
		/// </summary>
		private readonly MDX Model;

		/// <summary>
		/// Gets or sets the transform of the actor.
		/// </summary>
		public Transform ActorTransform { get; set; }

		private readonly PackageGroup ModelPackageGroup;
		private readonly RenderCache Cache = RenderCache.Instance;

		/// <summary>
		/// Dictionary that maps texture paths to OpenGL texture IDs.
		/// </summary>
		private readonly Dictionary<string, int> TextureLookup = new Dictionary<string, int>();

		private readonly Dictionary<MDXSkin, int> SkinIndexArrayLookup = new Dictionary<MDXSkin, int>();

		private int VertexBufferID;

		private GameModelShader Shader;

		/// <summary>
		/// Gets or sets a value indicating whether the current renderable has been initialized.
		/// </summary>
		public bool IsInitialized { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the bounding box of the model should be rendered.
		/// </summary>
		public bool ShouldRenderBounds { get; set; }

		/// <summary>
		/// Initializes the required data for rendering.
		/// </summary>
		public void Initialize()
		{
			this.Shader = this.Cache.GetShader(EverlookShader.GameModel) as GameModelShader;

			this.VertexBufferID = GL.GenBuffer();

			byte[] vertexBufferData = this.Model.Vertices
				.Select(v => v.PackForOpenGL())
				.SelectMany(b => b)
				.ToArray();

			GL.BindBuffer(BufferTarget.ArrayBuffer, this.VertexBufferID);
			GL.BufferData
			(
				BufferTarget.ArrayBuffer,
				(IntPtr)vertexBufferData.Length,
				vertexBufferData,
				BufferUsageHint.StaticDraw
			);

			// TODO: Textures

			// TODO: Per-skin array index buffers
			foreach (MDXSkin skin in this.Model.Skins)
			{
				int skinIndexBuffer = GL.GenBuffer();
				ushort[] absoluteTriangleVertexIndices = skin.Triangles.Select(relativeIndex => skin.VertexIndices[relativeIndex]).ToArray();

				GL.BindBuffer(BufferTarget.ElementArrayBuffer, skinIndexBuffer);
				GL.BufferData
				(
					BufferTarget.ElementArrayBuffer,
					absoluteTriangleVertexIndices.Length * sizeof(ushort),
					absoluteTriangleVertexIndices,
					BufferUsageHint.StaticDraw
				);

				this.SkinIndexArrayLookup.Add(skin, skinIndexBuffer);
			}

			this.IsInitialized = true;
		}

		/// <inheritdoc />
		public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera)
		{
			if (!this.IsInitialized)
			{
				return;
			}

			Matrix4 modelViewProjection = this.ActorTransform.GetModelMatrix() * viewMatrix * projectionMatrix;

			this.Shader.Enable();

			GL.BindBuffer(BufferTarget.ArrayBuffer, this.VertexBufferID);
			GL.EnableVertexAttribArray(0);

			// Position pointer
			GL.VertexAttribPointer
			(
				0,
				3,
				VertexAttribPointerType.Float,
				false,
				0,
				0
			);

			// Bone weight pointer
			GL.VertexAttribPointer
			(
				1,
				4,
				VertexAttribPointerType.Byte,
				false,
				12,
				12
			);

			// Bone index pointer
			GL.VertexAttribPointer
			(
				2,
				4,
				VertexAttribPointerType.Byte,
				false,
				16,
				16
			);

			// Normal pointer
			GL.VertexAttribPointer
			(
				3,
				3,
				VertexAttribPointerType.Float,
				false,
				20,
				20
			);

			// UV1 pointer
			GL.VertexAttribPointer
			(
				4,
				2,
				VertexAttribPointerType.Float,
				false,
				32,
				32
			);

			// UV2 pointer
			GL.VertexAttribPointer
			(
				5,
				2,
				VertexAttribPointerType.Float,
				false,
				40,
				40
			);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="RenderableGameModel"/> class.
		/// </summary>
		/// <param name="inModel">The model to render.</param>
		/// <param name="inPackageGroup">The package group the model belongs to.</param>
		/// <param name="shouldInitialize">Whether or not the model should be initialized right away.</param>
		public RenderableGameModel(MDX inModel, PackageGroup inPackageGroup, bool shouldInitialize)
		{
			this.Model = inModel;
			this.ModelPackageGroup = inPackageGroup;

			this.IsInitialized = false;
			if (shouldInitialize)
			{
				Initialize();
			}
		}

		/// <summary>
		/// Releases all resource used by the <see cref="RenderableGameModel"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="RenderableGameModel"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="RenderableGameModel"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="RenderableGameModel"/> so the garbage collector can reclaim the memory that the
		/// <see cref="RenderableGameModel"/> was occupying.</remarks>
		public void Dispose()
		{
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="RenderableGameModel"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (this.IsStatic.GetHashCode() + this.Model.GetHashCode()).GetHashCode();
		}
	}
}
