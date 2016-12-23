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
using System.Drawing;
using Warcraft.Core;

namespace Everlook.Viewport.Rendering
{
	/// <summary>
	/// Encapsulates a standard bitmap as a renderable object.
	/// </summary>
	public sealed class RenderableBitmap : RenderableImage
	{
		/// <summary>
		/// The encapsulated image.
		/// </summary>
		/// <value>The image.</value>
		private Bitmap Image
		{
			get;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> class.
		/// </summary>
		/// <param name="inImage">In image.</param>
		public RenderableBitmap(Bitmap inImage, string texturePath)
		{
			this.Image = inImage;
			this.IsInitialized = false;
			this.TexturePath = texturePath;

			Initialize();
		}

		/// <summary>
		/// Loads or creates a cached texture from the global texture cache using the path the image
		/// was constructed with as a key.
		/// </summary>
		/// <returns></returns>
		protected override int LoadCachedTexture()
		{
			if (this.Cache.HasCachedTextureForPath(this.TexturePath))
			{
				return this.Cache.GetCachedTexture(this.TexturePath);
			}

			return this.Cache.CreateCachedTexture(this.Image, this.TexturePath);
		}

		/// <summary>
		/// Gets the resolution of the encapsulated image.
		/// </summary>
		/// <returns></returns>
		protected override Resolution GetResolution()
		{
			return new Resolution((uint) this.Image.Width, (uint) this.Image.Height);
		}

		/// <summary>
		/// Releases all resource used by the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> so the garbage collector can reclaim the memory that the
		/// <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> was occupying.</remarks>
		public override void Dispose()
		{
			base.Dispose();

			this.Image.Dispose();
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="Everlook.Viewport.Rendering.RenderableBitmap"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (this.IsStatic.GetHashCode() + this.Image.GetHashCode()).GetHashCode();
		}
	}
}

