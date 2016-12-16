//
//  RenderableBLP.cs
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
using Warcraft.BLP;
using Warcraft.Core;

namespace Everlook.Viewport.Rendering
{
	/// <summary>
	/// Represents a renderable BLP image.
	/// </summary>
	public sealed class RenderableBLP : RenderableImage
	{
		/// <summary>
		/// The image contained by this instance.
		/// </summary>
		private readonly BLP Image;

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Viewport.Rendering.RenderableBLP"/> class.
		/// </summary>
		/// <param name="inImage">An image object with populated data.</param>
		/// <param name="texturePath">The path to the texture inside the package group.</param>
		public RenderableBLP(BLP inImage, string texturePath)
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
			if (Cache.HasCachedTextureForPath(this.TexturePath))
			{
				return Cache.GetCachedTexture(this.TexturePath);
			}

			return Cache.CreateCachedTexture(this.Image, this.TexturePath);
		}

		/// <summary>
		/// Gets the resolution of the encapsulated image.
		/// </summary>
		/// <returns></returns>
		protected override Resolution GetResolution()
		{
			return Image.GetResolution();
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="Everlook.Viewport.Rendering.RenderableBLP"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (IsStatic.GetHashCode() + Image.GetHashCode()).GetHashCode();
		}
	}
}

