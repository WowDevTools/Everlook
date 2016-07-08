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
using Everlook.Package;
using Everlook.Viewport.Rendering.Interfaces;
using OpenTK;
using Warcraft.BLP;
using Warcraft.WMO;

namespace Everlook.Viewport.Rendering
{
	/// <summary>
	/// Represents a renderable World Model Object
	/// </summary>
	public sealed class RenderableWorldModel : IRenderable
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
				return false;
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
				return ProjectionType.Perspective;
			}
		}

		/// <summary>
		/// The model contained by this renderable world object.
		/// </summary>
		/// <value>The model.</value>
		public WMO Model
		{
			get;
			private set;
		}

		private readonly PackageGroup ModelPackageGroup;
		private readonly RenderCache Cache = RenderCache.Instance;

		/// <summary>
		/// Dictionary that maps texture paths to OpenGL texture IDs.
		/// </summary>
		private readonly Dictionary<string, int> textureLookup = new Dictionary<string, int>();

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

			this.IsInitialized = false;

			Initialize();
		}

		/// <summary>
		/// Initializes the required data for rendering.
		/// </summary>
		public void Initialize()
		{
			// Load and cache the textures used in this model
			foreach (string texturePath in Model.GetUsedTextures())
			{
				if (Cache.HasCachedTextureForPath(texturePath))
				{
					this.textureLookup.Add(texturePath, Cache.GetCachedTexture(texturePath));
				}
				else
				{
					BLP texture = new BLP(ModelPackageGroup.ExtractFile(texturePath));
					this.textureLookup.Add(texturePath, Cache.CreateCachedTexture(texture, texturePath));
				}
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
		}

		/// <summary>
		/// Renders the current object in the current OpenGL context.
		/// </summary>
		public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix)
		{

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
			Model.Dispose();
			Model = null;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="RenderableWorldModel"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (IsStatic.GetHashCode() + Model.GetHashCode()).GetHashCode();
		}
	}
}

