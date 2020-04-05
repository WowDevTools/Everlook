//
//  RenderableActorReference.cs
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

using System;
using System.Numerics;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Interfaces;

namespace Everlook.Viewport.Rendering.Core
{
    /// <summary>
    /// Represents a reference to a renderable. This class acts as a proxied instance of a given actor with its own
    /// transform, but does not utilize any OpenGL instancing functions.
    /// </summary>
    /// <typeparam name="T">The renderable type that is encapsulated.</typeparam>
    public class RenderableActorReference<T> : IRenderable, IActor where T : class, IRenderable, IActor
    {
        /// <inheritdoc />
        public bool IsStatic => _target.IsStatic;

        /// <inheritdoc />
        public bool IsInitialized { get; set; }

        /// <inheritdoc />
        public ProjectionType Projection => _target.Projection;

        /// <inheritdoc />
        public Transform ActorTransform { get; set; }

        private readonly T _target;
        private readonly Transform _defaultTransform;

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderableActorReference{T}"/> class.
        /// </summary>
        /// <param name="target">The target actor to act as an instance of.</param>
        public RenderableActorReference(T target)
            : this(target, target.ActorTransform)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderableActorReference{T}"/> class.
        /// </summary>
        /// <param name="target">The target actor to act as an instance of.</param>
        /// <param name="transform">The transform of the instance.</param>
        public RenderableActorReference(T target, Transform transform)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (transform is null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            _target = target;
            _defaultTransform = target.ActorTransform;

            this.ActorTransform = transform;
        }

        /// <inheritdoc />
        public void Initialize()
        {
            this.IsInitialized = true;
        }

        /// <inheritdoc />
        public void Render(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, ViewportCamera camera)
        {
            _target.ActorTransform = this.ActorTransform;
            _target.Render(viewMatrix, projectionMatrix, camera);
            _target.ActorTransform = _defaultTransform;
        }

        /// <summary>
        /// This method does nothing, and should not be called. The source actor should be disposed instead.
        /// </summary>
        public void Dispose()
        {
            throw new NotSupportedException
            (
                "A renderable instance should not be disposed. Dispose the source actor instead."
            );
        }
    }
}
