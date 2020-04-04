//
//  IInstancedRenderable.cs
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

using System.Numerics;
using Everlook.Viewport.Camera;

namespace Everlook.Viewport.Rendering.Interfaces
{
    /// <summary>
    /// Describes the functionality of an object that can be rendered as a set of instances instead of a single object.
    /// </summary>
    public interface IInstancedRenderable : IRenderable
    {
        /// <summary>
        /// Renders a set of instances of the object.
        /// </summary>
        /// <param name="viewMatrix">The view matrix.</param>
        /// <param name="projectionMatrix">The projection matrix.</param>
        /// <param name="camera">The user camera.</param>
        /// <param name="count">The number of instances to render.</param>
        void RenderInstances(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, ViewportCamera camera, int count);
    }
}
