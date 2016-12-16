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
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Interfaces;
using OpenTK;
using SlimTK;
using Warcraft.MDX;

namespace Everlook.Viewport.Rendering
{
	/// <summary>
	/// Represents a renderable Game Object Model.
	/// </summary>
	public sealed class RenderableGameModel : IRenderable
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
		/// Returns a value which represents whether or not the current renderable has been initialized.
		/// </summary>
		public bool IsInitialized { get; set; }

		/// <summary>
		/// Initializes the required data for rendering.
		/// </summary>
		public void Initialize()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Renders the current object in the current OpenGL context.
		/// </summary>
		public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// The model contained by this renderable game object.
		/// </summary>
		/// <value>The model.</value>
		public MDX Model
		{
			get;
			private set;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="RenderableGameModel"/> class.
		/// </summary>
		public RenderableGameModel(MDX inModel)
		{
			this.Model = inModel;
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
			Model.Dispose();
			Model = null;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="RenderableGameModel"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (IsStatic.GetHashCode() + Model.GetHashCode()).GetHashCode();
		}
	}
}

