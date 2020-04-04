//
//  RenderableBitmap.cs
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
using System.Drawing;
using Everlook.Viewport.Rendering.Core;
using Warcraft.Core.Structures;

namespace Everlook.Viewport.Rendering
{
    /// <summary>
    /// Encapsulates a standard bitmap as a renderable object.
    /// </summary>
    public sealed class RenderableBitmap : RenderableImage
    {
        /// <summary>
        /// Gets the encapsulated image.
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
        /// <param name="inTexturePath">The path under which this renderable texture is stored in the archives.</param>
        public RenderableBitmap(Bitmap inImage, string inTexturePath)
        {
            this.Image = inImage;
            this.TexturePath = inTexturePath;

            this.IsInitialized = false;
        }

        /// <inheritdoc />
        protected override Texture2D LoadTexture()
        {
            if (Cache.HasCachedTextureForPath(this.TexturePath))
            {
                return Cache.GetCachedTexture(this.TexturePath);
            }

            return Cache.CreateCachedTexture(this.Image, this.TexturePath);
        }

        /// <inheritdoc />
        protected override Resolution GetResolution()
        {
            return new Resolution((uint)this.Image.Width, (uint)this.Image.Height);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            base.Dispose();

            this.Image.Dispose();
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            var otherImage = obj as RenderableBitmap;
            if (otherImage == null)
            {
                return false;
            }

            return otherImage.Image == this.Image && otherImage.IsStatic == this.IsStatic;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (this.IsStatic.GetHashCode() + this.Image.GetHashCode()).GetHashCode();
        }
    }
}
