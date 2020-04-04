//
//  GraphicsObject.cs
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

using JetBrains.Annotations;
using Silk.NET.OpenGL;

namespace Everlook.Viewport.Rendering.Core
{
    /// <summary>
    /// Represents the base class of any graphics-aware object.
    /// </summary>
    [PublicAPI]
    public abstract class GraphicsObject
    {
        /// <summary>
        /// Gets the OpenGL API available to this object.
        /// </summary>
        protected GL GL { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsObject"/> class.
        /// </summary>
        /// <param name="gl">The OpenGL API.</param>
        public GraphicsObject(GL gl)
        {
            this.GL = gl;
        }
    }
}
