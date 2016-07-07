//
//  RenderableBitmap.cs
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
using System.Drawing;
using Everlook.Viewport.Rendering.Interfaces;
using OpenTK;

namespace Everlook.Viewport.Rendering
{
	/// <summary>
	/// Encapsulated a standard bitmap as a renderable object.
	/// </summary>
	public sealed class RenderableBitmap : IRenderable
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
				return true;
			}
		}

		/// <summary>
		/// Returns a value which represents whether or not the current renderable has been initialized.
		/// </summary>
		public bool IsInitialized { get; set; }

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
		public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// The encapsulated image.
		/// </summary>
		/// <value>The image.</value>
		public Bitmap Image
		{
			get;
			private set;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> class.
		/// </summary>
		/// <param name="inImage">In image.</param>
		public RenderableBitmap(Bitmap inImage)
		{
			this.Image = inImage;
		}

		/// <summary>
		/// Releases all resource used by the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> so the garbage collector can reclaim the memory that the
		/// <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> was occupying.</remarks>
		public void Dispose()
		{
			Image.Dispose();
			Image = null;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (IsStatic.GetHashCode() + Image.GetHashCode()).GetHashCode();
		}
	}
}

