﻿//
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
using Everlook.Configuration;
using Everlook.Database;
using Everlook.Exceptions.Shader;
using Everlook.Package;
using Everlook.Utility;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using Everlook.Viewport.Rendering.Shaders;
using Gtk;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SlimTK;
using Warcraft.Core;
using Warcraft.Core.Extensions;
using Warcraft.MDX;
using Warcraft.MDX.Geometry;
using Warcraft.MDX.Geometry.Skin;
using Warcraft.MDX.Visual;

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
		/// Gets or sets a value indicating whether this object has been disposed.
		/// </summary>
		private bool IsDisposed { get; set; }

		/// <summary>
		/// Gets or sets the transform of the actor.
		/// </summary>
		public Transform ActorTransform { get; set; }

		private readonly PackageGroup ModelPackageGroup;
		private readonly RenderCache Cache = RenderCache.Instance;
		private readonly ClientDatabaseProvider DatabaseProvider;

		/// <summary>
		/// Dictionary that maps texture paths to OpenGL textures.
		/// </summary>
		private readonly Dictionary<string, Texture2D> TextureLookup = new Dictionary<string, Texture2D>();

		private readonly Dictionary<MDXSkin, Buffer<ushort>> SkinIndexArrayBuffers = new Dictionary<MDXSkin, Buffer<ushort>>();

		private Buffer<byte> VertexBuffer;

		private GameModelShader Shader;

		private RenderableBoundingBox BoundingBox;

		/// <summary>
		/// Gets or sets a value indicating whether the current renderable has been initialized.
		/// </summary>
		public bool IsInitialized { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether or not the bounding box of the model should be rendered.
		/// </summary
		public bool ShouldRenderBounds { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether or not the wireframe of the object should be rendered.
		/// </summary>
		public bool ShouldRenderWireframe { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="RenderableGameModel"/> class.
		/// </summary>
		/// <param name="inModel">The model to render.</param>
		/// <param name="inPackageGroup">The package group the model belongs to.</param>
		/// <param name="inVersion">The game version of the package group.</param>
		public RenderableGameModel(MDX inModel, PackageGroup inPackageGroup, WarcraftVersion inVersion)
		{
			this.Model = inModel;
			this.ModelPackageGroup = inPackageGroup;

			this.DatabaseProvider = new ClientDatabaseProvider(inVersion, this.ModelPackageGroup);

			this.ActorTransform = new Transform
			(
				new Vector3(0.0f, 0.0f, 0.0f),
				Quaternion.Identity,
				new Vector3(1.0f, 1.0f, 1.0f)
			);

			this.IsInitialized = false;

			Initialize();
		}

		/// <summary>
		/// Initializes the required data for rendering.
		/// </summary>
		public void Initialize()
		{
			ThrowIfDisposed();

			this.Shader = this.Cache.GetShader(EverlookShader.GameModel) as GameModelShader;

			if (this.Shader == null)
			{
				throw new ShaderNullException(typeof(GameModelShader));
			}

			this.VertexBuffer = new Buffer<byte>(BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw)
			{
				Data = this.Model.Vertices.Select(v => v.PackForOpenGL()).SelectMany(b => b).ToArray()
			};

			this.BoundingBox = new RenderableBoundingBox(this.Model.BoundingBox.ToOpenGLBoundingBox(), this.ActorTransform);
			this.BoundingBox.Initialize();

			foreach (MDXTexture texture in this.Model.Textures)
			{
				if (!this.TextureLookup.ContainsKey(texture.Filename))
				{
					if (!string.IsNullOrEmpty(texture.Filename))
					{
						var wrapS = texture.Flags.HasFlag(EMDXTextureFlags.TextureWrapX)
							? TextureWrapMode.Repeat
							: TextureWrapMode.Clamp;

						var wrapT = texture.Flags.HasFlag(EMDXTextureFlags.TextureWrapY)
							? TextureWrapMode.Repeat
							: TextureWrapMode.Clamp;

						this.TextureLookup.Add
						(
							texture.Filename,
							this.Cache.GetTexture(texture.Filename, this.ModelPackageGroup, wrapS, wrapT)
						);
					}
					else
					{
						this.TextureLookup.Add
						(
							texture.Filename,
							this.Cache.FallbackTexture
						);
					}
				}
			}

			foreach (MDXSkin skin in this.Model.Skins)
			{
				ushort[] absoluteTriangleVertexIndexes = skin.Triangles.Select(relativeIndex => skin.VertexIndices[relativeIndex]).ToArray();
				var skinIndexBuffer = new Buffer<ushort>(BufferTarget.ElementArrayBuffer, BufferUsageHint.StaticDraw)
				{
					Data = absoluteTriangleVertexIndexes
				};

				this.SkinIndexArrayBuffers.Add(skin, skinIndexBuffer);
			}

			this.IsInitialized = true;
		}

		/// <inheritdoc />
		public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera)
		{
			ThrowIfDisposed();

			if (!this.IsInitialized)
			{
				return;
			}

			Matrix4 modelViewProjection = this.ActorTransform.GetModelMatrix() * viewMatrix * projectionMatrix;

			this.VertexBuffer.Bind();

			this.Shader.Enable();
			this.Shader.SetMVPMatrix(modelViewProjection);

			// Position pointer
			GL.EnableVertexAttribArray(0);
			GL.VertexAttribPointer
			(
				0,
				3,
				VertexAttribPointerType.Float,
				false,
				MDXVertex.GetSize(),
				0
			);

			// Bone weight pointer
			GL.EnableVertexAttribArray(1);
			GL.VertexAttribPointer
			(
				1,
				4,
				VertexAttribPointerType.UnsignedByte,
				false,
				MDXVertex.GetSize(),
				12
			);

			// Bone index pointer
			GL.EnableVertexAttribArray(2);
			GL.VertexAttribPointer
			(
				2,
				4,
				VertexAttribPointerType.UnsignedByte,
				false,
				MDXVertex.GetSize(),
				16
			);

			// Normal pointer
			GL.EnableVertexAttribArray(3);
			GL.VertexAttribPointer
			(
				3,
				3,
				VertexAttribPointerType.Float,
				false,
				MDXVertex.GetSize(),
				20
			);

			// UV1 pointer
			GL.EnableVertexAttribArray(4);
			GL.VertexAttribPointer
			(
				4,
				2,
				VertexAttribPointerType.Float,
				false,
				MDXVertex.GetSize(),
				32
			);

			// UV2 pointer
			GL.EnableVertexAttribArray(5);
			GL.VertexAttribPointer
			(
				5,
				2,
				VertexAttribPointerType.Float,
				false,
				MDXVertex.GetSize(),
				40
			);

			this.Shader.Wireframe.Enabled = this.ShouldRenderWireframe;
			if (this.ShouldRenderWireframe)
			{
				this.Shader.Wireframe.SetWireframeColour(EverlookConfiguration.Instance.WireframeColour);
				this.Shader.Wireframe.SetViewportMatrix(camera.GetViewportMatrix());

				// Override blend setting
				GL.Enable(EnableCap.Blend);
			}

			GL.Enable(EnableCap.DepthTest);

			foreach (MDXSkin skin in this.Model.Skins)
			{
				this.SkinIndexArrayBuffers[skin].Bind();
				this.Shader.Enable();

				if (this.ShouldRenderWireframe)
				{
					// Override blend setting
					GL.Enable(EnableCap.Blend);
				}

				foreach
				(
					MDXRenderBatch renderBatch in skin.RenderBatches
					.OrderByDescending
					(
						renderBatch => VectorMath.Distance
						(
							camera.Position,
							skin.Sections[renderBatch.SkinSectionIndex].SortCenterPosition.AsOpenTKVector()
						)
					)
				)
				{
					var skinSection = skin.Sections[renderBatch.SkinSectionIndex];

					var textureIndexes = this.Model.TextureLookupTable.Skip(renderBatch.TextureLookupTableIndex).Take(renderBatch.TextureCount);
					var textureNames = this.Model.Textures.Where((t, i) => textureIndexes.Contains((short)i)).Select(t => t.Filename);
					var textures = this.TextureLookup.Where(entry => textureNames.Contains(entry.Key)).Select(entry => entry.Value);

					var textureUnit = this.Model.TextureSlotLookupTable[renderBatch.TextureSlotLookupTableIndex];

					this.Shader.BindTexture2D(TranslateModelTextureUnit(textureUnit), TextureUniform.Diffuse0, textures.First());

					GL.DrawRangeElements
					(
						PrimitiveType.Triangles,
						skinSection.StartTriangleIndex,
						skinSection.StartTriangleIndex + skinSection.TriangleCount - 1,
						skinSection.TriangleCount,
						DrawElementsType.UnsignedShort,
						new IntPtr(skinSection.StartTriangleIndex * 2)
					);
				}
			}

			// Render bounding boxes
			if (this.ShouldRenderBounds)
			{
				this.BoundingBox.Render(viewMatrix, projectionMatrix, camera);
			}

			// Release the attribute arrays
			GL.DisableVertexAttribArray(0);
			GL.DisableVertexAttribArray(1);
			GL.DisableVertexAttribArray(2);
			GL.DisableVertexAttribArray(3);
			GL.DisableVertexAttribArray(4);
			GL.DisableVertexAttribArray(5);
		}

		/// <summary>
		/// Translates the stored model texture unit to an OpenGL texture unit.
		/// </summary>
		/// <param name="textureUnit">The model texture unit.</param>
		/// <returns>An OpenGL texture unit.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Thrown if <paramref name="textureUnit"/> is not -1, 0, or 1.
		/// </exception>
		private TextureUnit TranslateModelTextureUnit(short textureUnit)
		{
			switch (textureUnit)
			{
				case 0: return TextureUnit.Texture0;
				case 1: return TextureUnit.Texture1;
				case -1: return TextureUnit.Texture2;
				default: throw new ArgumentOutOfRangeException(nameof(textureUnit));
			}
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
		/// Releases all resource used by the <see cref="RenderableGameModel"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="RenderableGameModel"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="RenderableGameModel"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="RenderableGameModel"/> so the garbage collector can reclaim the memory that the
		/// <see cref="RenderableGameModel"/> was occupying.</remarks>
		public void Dispose()
		{
			this.VertexBuffer.Dispose();

			foreach (var skinIndexArrayBuffer in this.SkinIndexArrayBuffers)
			{
				skinIndexArrayBuffer.Value.Dispose();
			}
		}

		/// <summary>
		/// Determines whether or not this object is equal to another object.
		/// </summary>
		/// <param name="obj">The other object</param>
		/// <returns>true if the objects are equal; false otherwise.</returns>
		public override bool Equals(object obj)
		{
			var otherModel = obj as RenderableGameModel;
			if (otherModel == null)
			{
				return false;
			}

			return (otherModel.Model == this.Model) &&
					(otherModel.ModelPackageGroup == this.ModelPackageGroup) &&
					(otherModel.IsStatic == this.IsStatic);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="RenderableGameModel"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (this.IsStatic.GetHashCode() + this.Model.GetHashCode() + this.ModelPackageGroup.GetHashCode()).GetHashCode();
		}
	}
}
