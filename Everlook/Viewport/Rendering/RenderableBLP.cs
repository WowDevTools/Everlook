//
//  RenderableBLP.cs
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

using Everlook.Viewport.Rendering.Core;
using Warcraft.BLP;
using Warcraft.Core.Structures;

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
        private readonly BLP _image;

        /// <summary>
        /// Initializes a new instance of the <see cref="Everlook.Viewport.Rendering.RenderableBLP"/> class.
        /// </summary>
        /// <param name="inImage">An image object with populated data.</param>
        /// <param name="inTexturePath">The path under which this renderable texture is stored in the archives.</param>
        public RenderableBLP(BLP inImage, string inTexturePath)
        {
            _image = inImage;
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

            return Cache.CreateCachedTexture(_image, this.TexturePath);
        }

        /// <inheritdoc />
        protected override Resolution GetResolution()
        {
            return _image.GetResolution();
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            var otherImage = obj as RenderableBLP;
            if (otherImage == null)
            {
                return false;
            }

            return otherImage._image == _image;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (this.IsStatic.GetHashCode() + _image.GetHashCode()).GetHashCode();
        }
    }
}
