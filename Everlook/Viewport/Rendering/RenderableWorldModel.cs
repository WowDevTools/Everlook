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
using Everlook.Package;
using Everlook.Utility;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using log4net;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using SlimTK;
using Warcraft.BLP;
using Warcraft.Core;
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
	public sealed class RenderableWorldModel : ITickingActor
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
		public bool IsStatic
		{
			get { return false; }
		}


		/// <summary>
		/// The projection method to use for this renderable object. Typically, this is Orthographic
		/// or Perspective.
		/// </summary>
		public ProjectionType Projection
		{
			get { return ProjectionType.Perspective; }
		}

		/// <summary>
		/// The model contained by this renderable world object.
		/// </summary>
		/// <value>The model.</value>
		private readonly WMO Model;

		/// <summary>
		/// The transform of the actor.
		/// </summary>
		public Transform ActorTransform { get; set; }

		private readonly PackageGroup ModelPackageGroup;
		private readonly RenderCache Cache = RenderCache.Instance;

		/// <summary>
		/// Dictionary that maps texture paths to OpenGL texture IDs.
		/// </summary>
		private readonly Dictionary<string, int> textureLookup = new Dictionary<string, int>();

		// Actual model data
		private readonly Dictionary<ModelGroup, int> vertexBufferLookup = new Dictionary<ModelGroup, int>();
		private readonly Dictionary<ModelGroup, int> normalBufferLookup = new Dictionary<ModelGroup, int>();
		private readonly Dictionary<ModelGroup, int> textureCoordinateBufferLookup = new Dictionary<ModelGroup, int>();
		private readonly Dictionary<ModelGroup, int> vertexIndexBufferLookup = new Dictionary<ModelGroup, int>();

		// Bounding box data
		private readonly Dictionary<ModelGroup, int> boundingBoxVertexBufferLookup = new Dictionary<ModelGroup, int>();

		private int boundingBoxVertexIndexBufferID;

		private int SimpleShaderID;
		private int BoundingBoxShaderID;

		/// <summary>
		/// Returns a value which represents whether or not the current renderable has been initialized.
		/// </summary>
		public bool IsInitialized { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="RenderableWorldModel"/> class.
		/// </summary>
		public RenderableWorldModel(WMO inModel, PackageGroup inPackageGroup)
		{
			this.Model = inModel;
			this.ModelPackageGroup = inPackageGroup;

			this.ActorTransform = new Transform
			(
				new Vector3(0.0f, 0.0f, 0.0f),
				Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.Pi),
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
			// Load and cache a simple, unlit shader
			if (this.Cache.HasCachedShader(EverlookShader.UnlitWorldModel))
			{
				this.SimpleShaderID = this.Cache.GetCachedShader(EverlookShader.UnlitWorldModel);
			}
			else
			{
				this.SimpleShaderID = this.Cache.CreateCachedShader(EverlookShader.UnlitWorldModel);
			}

			if (this.Cache.HasCachedShader(EverlookShader.BoundingBox))
			{
				this.BoundingBoxShaderID = this.Cache.GetCachedShader(EverlookShader.BoundingBox);
			}
			else
			{
				this.BoundingBoxShaderID = this.Cache.CreateCachedShader(EverlookShader.BoundingBox);
			}

			// TODO: Load and cache doodads in their respective sets

			// TODO: Load and cache sound emitters

			// TODO: Upload visible block vertices

			// TODO: Upload portal vertices for debug rendering

			// TODO: Load lights into some sort of reasonable structure

			// TODO: Load fog as OpenGL fog

			// TODO: Implement antiportal handling. For now, skip them

			// TODO: Upload convex planes for debug rendering

			// TODO: Upload vertices, UVs and normals of groups in parallel buffers
			foreach (ModelGroup modelGroup in this.Model.Groups)
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

				int boundingBoxVertexBufferID;
				GL.GenBuffers(1, out boundingBoxVertexBufferID);

				// Upload all of the vertices in this group
				List<Vector3f> groupVertices = modelGroup.GetVertices();
				List<float> groupVertexValues = new List<float>();

				foreach (Vector3f vertex in groupVertices)
				{
					groupVertexValues.Add(vertex.X);
					groupVertexValues.Add(vertex.Y);
					groupVertexValues.Add(vertex.Z);
				}

				float[] groupVertexValuesArray = groupVertexValues.ToArray();
				GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferID);
				GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) (groupVertexValuesArray.Length * sizeof(float)),
					groupVertexValuesArray, BufferUsageHint.StaticDraw);

				this.vertexBufferLookup.Add(modelGroup, vertexBufferID);

				// Upload all of the normals in this group
				List<Vector3f> groupNormals = modelGroup.GetNormals();
				List<float> groupNormalValues = new List<float>();

				foreach (Vector3f vertex in groupNormals)
				{
					groupNormalValues.Add(vertex.X);
					groupNormalValues.Add(vertex.Y);
					groupNormalValues.Add(vertex.Z);
				}

				float[] groupNormalValuesArray = groupNormalValues.ToArray();
				GL.BindBuffer(BufferTarget.ArrayBuffer, normalBufferID);
				GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) (groupNormalValuesArray.Length * sizeof(float)),
					groupNormalValuesArray, BufferUsageHint.StaticDraw);

				this.normalBufferLookup.Add(modelGroup, normalBufferID);

				// Upload all of the UVs in this group
				List<Vector2f> groupTextureCoordinates = modelGroup.GetTextureCoordinates();
				List<float> groupTextureCoordinateValues = new List<float>();

				foreach (Vector2f coordinate in groupTextureCoordinates)
				{
					groupTextureCoordinateValues.Add(coordinate.X);
					groupTextureCoordinateValues.Add(coordinate.Y);
				}

				float[] groupTextureCoordinateValuesArray = groupTextureCoordinateValues.ToArray();
				GL.BindBuffer(BufferTarget.ArrayBuffer, coordinateBufferID);
				GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) (groupTextureCoordinateValuesArray.Length * sizeof(float)),
					groupTextureCoordinateValuesArray, BufferUsageHint.StaticDraw);

				this.textureCoordinateBufferLookup.Add(modelGroup, coordinateBufferID);

				// Upload vertex indices for this group
				ushort[] groupVertexIndexValuesArray = modelGroup.GetVertexIndices().ToArray();
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, vertexIndicesID);
				GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr) (groupVertexIndexValuesArray.Length * sizeof(ushort)),
					groupVertexIndexValuesArray, BufferUsageHint.StaticDraw);

				this.vertexIndexBufferLookup.Add(modelGroup, vertexIndicesID);

				// Upload the corners of the bounding box
				List<Vector3> boundingBoxCorners = modelGroup.GetBoundingBox().ToOpenGLBoundingBox().GetCorners().ToList();
				List<float> boundingBoxCornerValues = new List<float>();
				foreach (Vector3 vertex in boundingBoxCorners)
				{
					boundingBoxCornerValues.Add(vertex.X);
					boundingBoxCornerValues.Add(vertex.Y);
					boundingBoxCornerValues.Add(vertex.Z);
				}

				float[] boundingBoxVertexArray = boundingBoxCornerValues.ToArray();
				GL.BindBuffer(BufferTarget.ArrayBuffer, boundingBoxVertexBufferID);
				GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(boundingBoxVertexArray.Length * sizeof(float)),
					boundingBoxVertexArray, BufferUsageHint.StaticDraw);

				this.boundingBoxVertexBufferLookup.Add(modelGroup, boundingBoxVertexBufferID);
			}

			// The bounding box indices never differ for objects, so we'll initialize that here
			int boundingBoxVertexIndicesBufferID;
			GL.GenBuffers(1, out boundingBoxVertexIndicesBufferID);
			this.boundingBoxVertexIndexBufferID = boundingBoxVertexIndicesBufferID;

			byte[] boundingBoxIndexValuesArray =
			{
				0, 1, 1, 2,
				2, 3, 3, 0,
				0, 4, 4, 7,
				7, 3, 2, 6,
				6, 7, 6, 5,
				5, 4, 5, 1
			};

			GL.BindBuffer(BufferTarget.ArrayBuffer, boundingBoxVertexIndicesBufferID);
			GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(boundingBoxIndexValuesArray.Length * sizeof(byte)),
				boundingBoxIndexValuesArray, BufferUsageHint.StaticDraw);

			this.IsInitialized = true;
		}

		/// <summary>
		/// Ticks this actor, advancing or performing any time-based actions.
		/// </summary>
		public void Tick(float deltaTime)
		{
			// TODO: Tick the animations of all referenced doodads.
			// Doodads that are not rendered should still have their states advanced.
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
OpenTK.
			Matrix4 modelViewProjection = this.ActorTransform.GetModelMatrix() * viewMatrix * projectionMatrix;

			// TODO: Fix frustum culling
			foreach (ModelGroup modelGroup in this.Model.Groups
				.OrderByDescending(modelGroup => VectorMath.Distance(camera.Position, modelGroup.GetPosition().AsOpenTKVector())))
			{

				RenderGroup(modelGroup, modelViewProjection);

				// Now, draw the model's bounding box
				BoundingBox groupBoundingBox = modelGroup.GetBoundingBox().ToOpenGLBoundingBox().Transform(ref modelViewProjection);
				if (camera.CanSee(groupBoundingBox))
				{
					//continue;
					RenderBoundingBox(modelGroup, modelViewProjection, Color4.LimeGreen);
				}
				else
				{
					RenderBoundingBox(modelGroup, modelViewProjection, Color4.Red);
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
			GL.UseProgram(this.SimpleShaderID);

			// Render the object
			// Send the vertices to the shader
			GL.EnableVertexAttribArray(0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertexBufferLookup[modelGroup]);
			GL.VertexAttribPointer(
				0,
				3,
				VertexAttribPointerType.Float,
				false,
				0,
				0);

			// Send the normals to the shader
			GL.EnableVertexAttribArray(1);
			GL.BindBuffer(BufferTarget.ArrayBuffer, this.normalBufferLookup[modelGroup]);
			GL.VertexAttribPointer(
				1,
				3,
				VertexAttribPointerType.Float,
				false,
				0,
				0);

			// Send the texture coordinates to the shader
			GL.EnableVertexAttribArray(2);
			GL.BindBuffer(BufferTarget.ArrayBuffer, this.textureCoordinateBufferLookup[modelGroup]);
			GL.VertexAttribPointer(
				2,
				2,
				VertexAttribPointerType.Float,
				false,
				0,
				0);

			// Bind the index buffer
			int indexBufferID = this.vertexIndexBufferLookup[modelGroup];
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBufferID);

			// Send the model matrix to the shader
			int projectionShaderVariableHandle = GL.GetUniformLocation(this.SimpleShaderID, "ModelViewProjection");
			GL.UniformMatrix4(projectionShaderVariableHandle, false, ref modelViewProjection);

			// Render all the different materials (opaque first, transparent after)
			foreach (RenderBatch renderBatch in modelGroup.GetRenderBatches()
				.OrderBy(batch => batch.MaterialIndex)
				.ThenBy(batch => this.Model.GetMaterial(batch.MaterialIndex).BlendMode))
			{
				// TODO: Render based on the shader. For now, simple rendering of diffuse texture
				ModelMaterial modelMaterial = this.Model.GetMaterial(renderBatch.MaterialIndex);
				EnableMaterial(modelMaterial);

				int textureID = this.Cache.GetCachedTexture(modelMaterial.Texture0);

				// Set the texture ID as a uniform sampler in unit 0
				GL.ActiveTexture(TextureUnit.Texture0);
				GL.BindTexture(TextureTarget.Texture2D, textureID);
				int textureVariableHandle = GL.GetUniformLocation(this.SimpleShaderID, "texture0");
				int textureUnit = 0;
				GL.Uniform1(textureVariableHandle, 1, ref textureUnit);

				// Finally, draw the model
				GL.DrawRangeElements(PrimitiveType.Triangles, renderBatch.FirstPolygonIndex,
					renderBatch.FirstPolygonIndex + renderBatch.PolygonIndexCount - 1, renderBatch.PolygonIndexCount,
					DrawElementsType.UnsignedShort, new IntPtr(renderBatch.FirstPolygonIndex * 2));
			}

			// Release the attribute arrays
			GL.DisableVertexAttribArray(0);
			GL.DisableVertexAttribArray(1);
			GL.DisableVertexAttribArray(2);
		}

		private void RenderBoundingBox(ModelGroup modelGroup, Matrix4 modelViewProjection, Color4 colour)
		{
			GL.UseProgram(this.BoundingBoxShaderID);
			GL.Disable(EnableCap.CullFace);

			// Render the object
			// Send the vertices to the shader
			GL.EnableVertexAttribArray(0);
			GL.BindBuffer(BufferTarget.ArrayBuffer, this.boundingBoxVertexBufferLookup[modelGroup]);
			GL.VertexAttribPointer(
				0,
				3,
				VertexAttribPointerType.Float,
				false,
				0,
				0);

			// Bind the index buffer
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.boundingBoxVertexIndexBufferID);

			// Send the model matrix to the shader
			int projectionShaderVariableHandle = GL.GetUniformLocation(this.BoundingBoxShaderID, "ModelViewProjection");
			GL.UniformMatrix4(projectionShaderVariableHandle, false, ref modelViewProjection);

			// Send the box colour to the shader
			int boxColourShaderVariableHandle = GL.GetUniformLocation(this.BoundingBoxShaderID, "boxColour");
			GL.Uniform4(boxColourShaderVariableHandle, colour);

			// Now draw the box
			GL.DrawRangeElements(PrimitiveType.LineLoop, 0,
				23, 24,
				DrawElementsType.UnsignedByte, new IntPtr(0));

			GL.DisableVertexAttribArray(0);
		}

		private void EnableMaterial(ModelMaterial modelMaterial)
		{
			// Load the textures used in this material
			if (!string.IsNullOrEmpty(modelMaterial.Texture0))
			{
				if (modelMaterial.Flags.HasFlag(MaterialFlags.TextureWrappingClamp))
				{
					CacheTexture(modelMaterial.Texture0, TextureWrapMode.ClampToBorder);
				}
				else
				{
					CacheTexture(modelMaterial.Texture0);
				}
			}

			if (!string.IsNullOrEmpty(modelMaterial.Texture1))
			{
				if (modelMaterial.Flags.HasFlag(MaterialFlags.TextureWrappingClamp))
				{
					CacheTexture(modelMaterial.Texture1, TextureWrapMode.ClampToBorder);
				}
				else
				{
					CacheTexture(modelMaterial.Texture1);
				}
			}

			if (!string.IsNullOrEmpty(modelMaterial.Texture2))
			{
				if (modelMaterial.Flags.HasFlag(MaterialFlags.TextureWrappingClamp))
				{
					CacheTexture(modelMaterial.Texture2, TextureWrapMode.ClampToBorder);
				}
				else
				{
					CacheTexture(modelMaterial.Texture2);
				}
			}

			// Set two-sided rendering
			if (modelMaterial.Flags.HasFlag(MaterialFlags.TwoSided))
			{
				GL.Disable(EnableCap.CullFace);
			}
			else
			{
				GL.Enable(EnableCap.CullFace);
			}

			if (modelMaterial.BlendMode == BlendingMode.Transparent)
			{
				GL.Enable(EnableCap.Blend);
			}
			else
			{
				GL.Disable(EnableCap.Blend);
			}
		}

		private void CacheTexture(string texturePath, TextureWrapMode textureWrapMode = TextureWrapMode.Repeat)
		{
			if (this.Cache.HasCachedTextureForPath(texturePath))
			{
				if (!this.textureLookup.ContainsKey(texturePath))
				{
					this.textureLookup.Add(texturePath, this.Cache.GetCachedTexture(texturePath));
				}
			}
			else
			{
				try
				{
					BLP texture = new BLP(this.ModelPackageGroup.ExtractFile(texturePath));
					this.textureLookup.Add(texturePath, this.Cache.CreateCachedTexture(texture, texturePath, textureWrapMode));
				}
				catch (InvalidFileSectorTableException fex)
				{
					Log.Warn($"Failed to load the texture \"{texturePath}\" due to an invalid sector table (\"{fex.Message}\").\n" +
					         $" A fallback texture has been loaded instead.");
					this.textureLookup.Add(texturePath, this.Cache.FallbackTexture);
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

			foreach (var vertexBuffer in this.vertexBufferLookup)
			{
				int bufferID = vertexBuffer.Value;
				GL.DeleteBuffer(bufferID);
			}

			foreach (var normalBuffer in this.normalBufferLookup)
			{
				int bufferID = normalBuffer.Value;
				GL.DeleteBuffer(bufferID);
			}

			foreach (var coordinateBuffer in this.textureCoordinateBufferLookup)
			{
				int bufferID = coordinateBuffer.Value;
				GL.DeleteBuffer(bufferID);
			}

			foreach (var indexBuffer in this.vertexIndexBufferLookup)
			{
				int bufferID = indexBuffer.Value;
				GL.DeleteBuffer(bufferID);
			}

			foreach (var boundingBoxVertexBuffer in this.boundingBoxVertexBufferLookup)
			{
				int bufferID = boundingBoxVertexBuffer.Value;
				GL.DeleteBuffer(bufferID);
			}
			GL.DeleteBuffer(this.boundingBoxVertexIndexBufferID);
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