//
//  RenderableBoundingBox.cs
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
using System.Linq;
using System.Numerics;
using Everlook.Exceptions.Shader;
using Everlook.Utility;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using Everlook.Viewport.Rendering.Shaders;
using Silk.NET.OpenGL;
using Warcraft.Core.Structures;

namespace Everlook.Viewport.Rendering
{
    /// <summary>
    /// Wraps a <see cref="Box"/> as a renderable in-world actor.
    /// </summary>
    public sealed class RenderableBoundingBox : GraphicsObject, IInstancedRenderable, IActor
    {
        private readonly BoundingBoxShader _boxShader;

        /// <inheritdoc />
        public Transform ActorTransform { get; set; }

        /// <inheritdoc />
        public bool IsStatic => false;

        /// <inheritdoc />
        public bool IsInitialized { get; set; }

        /// <inheritdoc />
        public ProjectionType Projection => ProjectionType.Perspective;

        /// <summary>
        /// Gets or sets the colour of the bounding box's lines.
        /// </summary>
        public Vector4 LineColour { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this object has been disposed.
        /// </summary>
        private bool IsDisposed { get; set; }

        private Box _boundingBoxData;
        private Buffer<Vector3>? _vertexBuffer;
        private Buffer<byte>? _vertexIndexesBuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderableBoundingBox"/> class. The bounds data is taken from
        /// the given <see cref="Box"/>, and the world translation is set to <paramref name="transform"/>.
        /// </summary>
        /// <param name="gl">The OpenGL API.</param>
        /// <param name="renderCache">The rendering cache.</param>
        /// <param name="boundingBox">The BoundingBox to get data from.</param>
        /// <param name="transform">The world transform of the box.</param>
        public RenderableBoundingBox(GL gl, RenderCache renderCache, Box boundingBox, Transform transform)
            : base(gl)
        {
            _boundingBoxData = boundingBox;
            this.LineColour = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);

            this.ActorTransform = transform;
            _boxShader = (BoundingBoxShader)renderCache.GetShader(EverlookShader.BoundingBox);

            this.IsInitialized = false;
        }

        /// <inheritdoc />
        public void Initialize()
        {
            ThrowIfDisposed();

            if (this.IsInitialized)
            {
                return;
            }

            if (_boxShader is null)
            {
                throw new ShaderNullException(typeof(BoundingBoxShader));
            }

            _vertexBuffer = new Buffer<Vector3>(this.GL, BufferTargetARB.ArrayBuffer, BufferUsageARB.StaticDraw)
            {
                Data = _boundingBoxData.GetCorners().ToArray()
            };

            _vertexBuffer.AttachAttributePointer
            (
                new VertexAttributePointer
                (
                    this.GL,
                    0,
                    3,
                    VertexAttribPointerType.Float,
                    0,
                    0
                )
            );

            byte[] boundingBoxIndexValues =
            {
                0, 1,
                1, 2,
                2, 3,
                3, 0,

                0, 6,
                6, 7,
                7, 1,

                2, 4,
                4, 7,

                4, 5,
                5, 6,
                5, 3
            };

            _vertexIndexesBuffer = new Buffer<byte>
            (
                this.GL,
                BufferTargetARB.ElementArrayBuffer,
                BufferUsageARB.StaticDraw
            )
            {
                Data = boundingBoxIndexValues
            };

            this.IsInitialized = true;
        }

        /// <inheritdoc />
        public void RenderInstances(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, ViewportCamera camera, int count)
        {
            ThrowIfDisposed();

            if (_vertexBuffer is null || _vertexIndexesBuffer is null)
            {
                return;
            }

            this.GL.Disable(EnableCap.CullFace);
            this.GL.Disable(EnableCap.DepthTest);

            // Send the vertices to the shader
            _vertexBuffer.Bind();
            _vertexBuffer.EnableAttributes();

            _vertexIndexesBuffer.Bind();

            var modelViewProjection = this.ActorTransform.GetModelMatrix() * viewMatrix * projectionMatrix;

            _boxShader.Enable();
            _boxShader.SetIsInstance(true);
            _boxShader.SetMVPMatrix(modelViewProjection);
            _boxShader.SetLineColour(this.LineColour);
            _boxShader.SetViewMatrix(viewMatrix);
            _boxShader.SetProjectionMatrix(projectionMatrix);

            // Now draw the box
            unsafe
            {
                this.GL.DrawElementsInstanced
                (
                    PrimitiveType.LineLoop,
                    24,
                    DrawElementsType.UnsignedByte,
                    (void*)0,
                    (uint)count
                );
            }

            _vertexBuffer.DisableAttributes();
        }

        /// <inheritdoc />
        public void Render(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, ViewportCamera camera)
        {
            ThrowIfDisposed();

            if (_vertexBuffer is null || _vertexIndexesBuffer is null)
            {
                return;
            }

            this.GL.Disable(EnableCap.CullFace);
            this.GL.Disable(EnableCap.DepthTest);

            // Send the vertices to the shader
            _vertexBuffer.Bind();
            _vertexBuffer.EnableAttributes();

            _vertexIndexesBuffer.Bind();

            var modelViewProjection = this.ActorTransform.GetModelMatrix() * viewMatrix * projectionMatrix;

            _boxShader.Enable();
            _boxShader.SetMVPMatrix(modelViewProjection);
            _boxShader.SetLineColour(this.LineColour);

            // Now draw the box
            unsafe
            {
                this.GL.DrawElements
                (
                    PrimitiveType.LineLoop,
                    24,
                    DrawElementsType.UnsignedByte,
                    (void*)0
                );
            }

            _vertexBuffer.DisableAttributes();
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the object has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        private void ThrowIfDisposed()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(ToString() ?? nameof(RenderableBoundingBox));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.IsDisposed = true;

            _vertexBuffer?.Dispose();
            _vertexIndexesBuffer?.Dispose();
        }
    }
}
