//
//  RenderableWorldModel.cs
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
using Everlook.Viewport.Rendering.Core.Lights;
using Everlook.Viewport.Rendering.Interfaces;
using Everlook.Viewport.Rendering.Shaders;
using log4net;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using SlimTK;
using Warcraft.BLP;
using Warcraft.Core;
using Warcraft.Core.Extensions;
using Warcraft.WMO;
using Warcraft.WMO.GroupFile;
using Warcraft.WMO.GroupFile.Chunks;
using Warcraft.WMO.RootFile.Chunks;
using Quaternion = OpenTK.Quaternion;

namespace Everlook.Viewport.Rendering
{
	/// <summary>
	/// Represents a renderable World Model Object
	/// </summary>
	public sealed class RenderableWorldModel : ITickingActor, IDefaultCameraPositionProvider
	{
		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(RenderableWorldModel));

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

				if (this.Model.Groups.Count == 0)
				{
					return Vector3.Zero;
				}

				return
				(
					this.ActorTransform.GetModelMatrix() *
					new Vector4
					(
						this.Model.Groups
						.First()
						.GetBoundingBox()
						.GetCenterCoordinates()
						.AsOpenTKVector(),
						1.0f
					)
				)
				.Xyz;
			}
		}

		/// <summary>
		/// The model contained by this renderable world object.
		/// </summary>
		/// <value>The model.</value>
		private readonly WMO Model;

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

		// Actual model data
		private readonly Dictionary<ModelGroup, int> VertexBufferLookup = new Dictionary<ModelGroup, int>();
		private readonly Dictionary<ModelGroup, int> NormalBufferLookup = new Dictionary<ModelGroup, int>();
		private readonly Dictionary<ModelGroup, int> TextureCoordinateBufferLookup = new Dictionary<ModelGroup, int>();
		private readonly Dictionary<ModelGroup, int> VertexIndexBufferLookup = new Dictionary<ModelGroup, int>();

		// Bounding boxes
		private readonly Dictionary<ModelGroup, RenderableBoundingBox> BoundingBoxLookup = new Dictionary<ModelGroup, RenderableBoundingBox>();

		/// <summary>
		/// Gets or sets a value indicating whether or not the current renderable has been initialized.
		/// </summary>
		public bool IsInitialized { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether or not the bounding boxes of the model groups should be rendered.
		/// </summary>
		public bool ShouldRenderBounds { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether or not the wireframe of the object should be rendered.
		/// </summary>
		public bool ShouldRenderWireframe { get; set; }

		private WorldModelShader Shader;

		/// <summary>
		/// Initializes a new instance of the <see cref="RenderableWorldModel"/> class.
		/// </summary>
		/// <param name="inModel">The model to render.</param>
		/// <param name="inPackageGroup">The package group the model belongs to.</param>
		/// <param name="inVersion">The game version of the package group.</param>
		public RenderableWorldModel(WMO inModel, PackageGroup inPackageGroup, WarcraftVersion inVersion)
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
			this.Shader = this.Cache.GetShader(EverlookShader.WorldModel) as WorldModelShader;

			if (this.Shader == null)
			{
				throw new ShaderNullException(typeof(WorldModelShader));
			}

			this.Shader.Wireframe.SetWireframeColour(EverlookConfiguration.Instance.GetWireframeColour());

			// TODO: Load and cache doodads in their respective sets

			// TODO: Load and cache sound emitters

			// Load the textures used in this model
			foreach (string texture in this.Model.GetTextures())
			{
				if (!string.IsNullOrEmpty(texture))
				{
					CacheTexture(texture);
				}
			}

			// TODO: Upload visible block vertices

			// TODO: Upload portal vertices for debug rendering

			// TODO: Load lights into some sort of reasonable structure

			// TODO: Load fog as OpenGL fog

			// TODO: Implement antiportal handling. For now, skip them

			// TODO: Upload convex planes for debug rendering

			// TODO: Upload vertices, UVs and normals of groups in parallel buffers
			foreach (ModelGroup modelGroup in this.Model.Groups)
			{
				InitializeModelGroup(modelGroup);
			}

			this.IsInitialized = true;
		}

		private void InitializeModelGroup(ModelGroup modelGroup)
		{
			/*
				Buffers
			*/

			int vertexBufferID;
			GL.GenBuffers(1, out vertexBufferID);

			int normalBufferID;
			GL.GenBuffers(1, out normalBufferID);

			int coordinateBufferID;
			GL.GenBuffers(1, out coordinateBufferID);

			int vertexIndicesID;
			GL.GenBuffers(1, out vertexIndicesID);

			// Upload all of the vertices in this group
			float[] groupVertexValues = modelGroup
				.GetVertices()
				.Select(v => v.Flatten())
				.SelectMany(f => f)
				.ToArray();

			GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferID);
			GL.BufferData
			(
				BufferTarget.ArrayBuffer,
				(IntPtr) (groupVertexValues.Length * sizeof(float)),
				groupVertexValues,
				BufferUsageHint.StaticDraw
			);

			this.VertexBufferLookup.Add(modelGroup, vertexBufferID);

			// Upload all of the normals in this group
			float[] groupNormalValues = modelGroup
				.GetNormals()
				.Select(v => v.Flatten())
				.SelectMany(f => f)
				.ToArray();

			GL.BindBuffer(BufferTarget.ArrayBuffer, normalBufferID);
			GL.BufferData
			(
				BufferTarget.ArrayBuffer,
				(IntPtr) (groupNormalValues.Length * sizeof(float)),
				groupNormalValues,
				BufferUsageHint.StaticDraw
			);

			this.NormalBufferLookup.Add(modelGroup, normalBufferID);

			// Upload all of the UVs in this group
			float[] groupTextureCoordinateValues = modelGroup
				.GetTextureCoordinates()
				.Select(v => v.Flatten())
				.SelectMany(f => f)
				.ToArray();

			GL.BindBuffer(BufferTarget.ArrayBuffer, coordinateBufferID);
			GL.BufferData
			(
				BufferTarget.ArrayBuffer,
				(IntPtr) (groupTextureCoordinateValues.Length * sizeof(float)),
				groupTextureCoordinateValues,
				BufferUsageHint.StaticDraw
			);

			this.TextureCoordinateBufferLookup.Add(modelGroup, coordinateBufferID);

			// Upload vertex indices for this group
			ushort[] groupVertexIndexValuesArray = modelGroup.GetVertexIndices().ToArray();
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, vertexIndicesID);
			GL.BufferData
			(
				BufferTarget.ElementArrayBuffer,
				(IntPtr) (groupVertexIndexValuesArray.Length * sizeof(ushort)),
				groupVertexIndexValuesArray,
				BufferUsageHint.StaticDraw
			);

			this.VertexIndexBufferLookup.Add(modelGroup, vertexIndicesID);

			RenderableBoundingBox boundingBox = new RenderableBoundingBox
			(
				modelGroup.GetBoundingBox().ToOpenGLBoundingBox(),
				this.ActorTransform
			);

			boundingBox.Initialize();

			this.BoundingBoxLookup.Add(modelGroup, boundingBox);
		}

		/// <summary>
		/// Ticks this actor, advancing or performing any time-based actions.
		/// </summary>
		/// <param name="deltaTime">The time delta, in seconds.</param>
		public void Tick(float deltaTime)
		{
			// TODO: Tick the animations of all referenced doodads.
			// Doodads that are not rendered should still have their states advanced.
		}

		/// <inheritdoc />
		public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera)
		{
			if (!this.IsInitialized)
			{
				return;
			}

			this.Shader.Wireframe.Enabled = this.ShouldRenderWireframe;
			if (this.Shader.Wireframe.Enabled)
			{
				this.Shader.Wireframe.SetViewportMatrix(camera.GetViewportMatrix());
			}

			Matrix4 modelView = this.ActorTransform.GetModelMatrix() * viewMatrix;
			Matrix4 modelViewProjection = modelView * projectionMatrix;

			// TODO: Fix frustum culling
			foreach (ModelGroup modelGroup in this.Model.Groups
				.OrderByDescending(modelGroup => VectorMath.Distance(camera.Position, modelGroup.GetPosition().AsOpenTKVector())))
			{
				RenderGroup(modelGroup, modelViewProjection);

				if (this.ShouldRenderBounds)
				{
					this.BoundingBoxLookup[modelGroup].Render(viewMatrix, projectionMatrix, camera);
				}
			}

			// TODO: Summarize the render batches from each group that has the same material ID

			// TODO: Render each block of batches with the same material ID

			// TODO: Shade light effects and vertex colours

			// TODO: Render each doodad in the currently selected doodad set

			// TODO: Play sound emitters here?
		}

		/// <summary>
		/// Renders the specified model group on a batch basis.
		/// </summary>
		private void RenderGroup(ModelGroup modelGroup, Matrix4 modelViewProjection)
		{
			// Render the object
			// Send the vertices to the shader
			GL.EnableVertexAttribArray(0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, this.VertexBufferLookup[modelGroup]);
			GL.VertexAttribPointer(
				0,
				3,
				VertexAttribPointerType.Float,
				false,
				0,
				0);

			// Send the normals to the shader
			GL.EnableVertexAttribArray(1);
			GL.BindBuffer(BufferTarget.ArrayBuffer, this.NormalBufferLookup[modelGroup]);
			GL.VertexAttribPointer(
				1,
				3,
				VertexAttribPointerType.Float,
				false,
				0,
				0);

			// Send the texture coordinates to the shader
			GL.EnableVertexAttribArray(2);
			GL.BindBuffer(BufferTarget.ArrayBuffer, this.TextureCoordinateBufferLookup[modelGroup]);
			GL.VertexAttribPointer(
				2,
				2,
				VertexAttribPointerType.Float,
				false,
				0,
				0);

			// Bind the index buffer
			int indexBufferID = this.VertexIndexBufferLookup[modelGroup];
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBufferID);

			// Render all the different materials (opaque first, transparent after)
			foreach (RenderBatch renderBatch in modelGroup.GetRenderBatches()
				.OrderBy(batch => batch.MaterialIndex)
				.ThenBy(batch => this.Model.GetMaterial(batch.MaterialIndex).BlendMode))
			{
				this.Shader.Enable();

				ModelMaterial modelMaterial = this.Model.GetMaterial(renderBatch.MaterialIndex);

				this.Shader.SetMaterial(modelMaterial);
				this.Shader.SetMVPMatrix(modelViewProjection);

				// Set the texture as the first diffuse texture in unit 0
				Texture2D texture = this.Cache.GetCachedTexture(modelMaterial.Texture0);
				if (modelMaterial.Flags.HasFlag(MaterialFlags.TextureWrappingClamp))
				{
					texture.WrappingMode = TextureWrapMode.Clamp;
				}
				else
				{
					texture.WrappingMode = TextureWrapMode.Repeat;
				}

				this.Shader.BindTexture2D(TextureUnit.Texture0, TextureUniform.Diffuse0, texture);

				// Finally, draw the model
				GL.DrawRangeElements
				(
					PrimitiveType.Triangles,
					renderBatch.FirstPolygonIndex,
					renderBatch.FirstPolygonIndex + renderBatch.PolygonIndexCount - 1,
					renderBatch.PolygonIndexCount,
					DrawElementsType.UnsignedShort,
					new IntPtr(renderBatch.FirstPolygonIndex * 2)
				);
			}

			// Release the attribute arrays
			GL.DisableVertexAttribArray(0);
			GL.DisableVertexAttribArray(1);
			GL.DisableVertexAttribArray(2);
		}

		// TODO: This has to go
		private void CacheTexture(string texturePath, TextureWrapMode textureWrapMode = TextureWrapMode.Repeat)
		{
			if (this.Cache.HasCachedTextureForPath(texturePath))
			{
				if (!this.TextureLookup.ContainsKey(texturePath))
				{
					this.TextureLookup.Add(texturePath, this.Cache.GetCachedTexture(texturePath));
				}
			}
			else
			{
				try
				{
					BLP texture = new BLP(this.ModelPackageGroup.ExtractFile(texturePath));
					this.TextureLookup.Add(texturePath, this.Cache.CreateCachedTexture(texture, texturePath, textureWrapMode));
				}
				catch (InvalidFileSectorTableException fex)
				{
					Log.Warn($"Failed to load the texture \"{texturePath}\" due to an invalid sector table (\"{fex.Message}\").\n" +
							 $"A fallback texture has been loaded instead.");
					this.TextureLookup.Add(texturePath, this.Cache.FallbackTexture);
				}
			}
		}

		/// <summary>
		/// Releases all resource used by the <see cref="RenderableWorldModel"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="RenderableWorldModel"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="RenderableWorldModel"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="RenderableWorldModel"/> so the garbage collector can reclaim the memory that the
		/// <see cref="RenderableWorldModel"/> was occupying.</remarks>
		public void Dispose()
		{
			this.Model.Dispose();

			foreach (var vertexBuffer in this.VertexBufferLookup)
			{
				int bufferID = vertexBuffer.Value;
				GL.DeleteBuffer(bufferID);
			}

			foreach (var normalBuffer in this.NormalBufferLookup)
			{
				int bufferID = normalBuffer.Value;
				GL.DeleteBuffer(bufferID);
			}

			foreach (var coordinateBuffer in this.TextureCoordinateBufferLookup)
			{
				int bufferID = coordinateBuffer.Value;
				GL.DeleteBuffer(bufferID);
			}

			foreach (var indexBuffer in this.VertexIndexBufferLookup)
			{
				int bufferID = indexBuffer.Value;
				GL.DeleteBuffer(bufferID);
			}
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="RenderableWorldModel"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (this.IsStatic.GetHashCode() + this.Model.GetHashCode()).GetHashCode();
		}
	}
}
