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
using Everlook.Exceptions.Shader;
using Everlook.Utility;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using Everlook.Viewport.Rendering.Shaders;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using Warcraft.Core.Structures;

namespace Everlook.Viewport.Rendering
{
    /// <summary>
    /// Wraps a <see cref="Box"/> as a renderable in-world actor.
    /// </summary>
    public sealed class RenderableBoundingBox : IInstancedRenderable, IActor
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
        public Color4 LineColour { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this object has been disposed.
        /// </summary>
        private bool IsDisposed { get; set; }

        private Box _boundingBoxData;
        private Buffer<Vector3> _vertexBuffer;
        private Buffer<byte> _vertexIndexesBuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderableBoundingBox"/> class. The bounds data is taken from
        /// the given <see cref="Box"/>, and the world translation is set to <paramref name="transform"/>.
        /// </summary>
        /// <param name="boundingBox">The BoundingBox to get data from.</param>
        /// <param name="transform">The world transform of the box.</param>
        public RenderableBoundingBox(Box boundingBox, Transform transform)
        {
            this._boundingBoxData = boundingBox;
            this.LineColour = Color4.LimeGreen;

            this.ActorTransform = transform;
            this._boxShader = RenderCache.Instance.GetShader(EverlookShader.BoundingBox) as BoundingBoxShader;

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

            if (this._boxShader == null)
            {
                throw new ShaderNullException(typeof(BoundingBoxShader));
            }

            this._vertexBuffer = new Buffer<Vector3>(BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw)
            {
                Data = this._boundingBoxData.GetCorners().ToArray()
            };

            this._vertexBuffer.AttachAttributePointer(new VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 0, 0));

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

            this._vertexIndexesBuffer = new Buffer<byte>(BufferTarget.ElementArrayBuffer, BufferUsageHint.StaticDraw)
            {
                Data = boundingBoxIndexValues
            };

            this.IsInitialized = true;
        }

        /// <inheritdoc />
        public void RenderInstances(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera, int count)
        {
            ThrowIfDisposed();

            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);

            // Send the vertices to the shader
            this._vertexBuffer.Bind();
            this._vertexBuffer.EnableAttributes();

            this._vertexIndexesBuffer.Bind();

            Matrix4 modelViewProjection = this.ActorTransform.GetModelMatrix() * viewMatrix * projectionMatrix;

            this._boxShader.Enable();
            this._boxShader.SetIsInstance(true);
            this._boxShader.SetMVPMatrix(modelViewProjection);
            this._boxShader.SetLineColour(this.LineColour);
            this._boxShader.SetViewMatrix(viewMatrix);
            this._boxShader.SetProjectionMatrix(projectionMatrix);

            // Now draw the box
            GL.DrawElementsInstanced
            (
                PrimitiveType.LineLoop,
                24,
                DrawElementsType.UnsignedByte,
                new IntPtr(0),
                count
            );

            this._vertexBuffer.DisableAttributes();
        }

        /// <inheritdoc />
        public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera)
        {
            ThrowIfDisposed();

            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);

            // Send the vertices to the shader
            this._vertexBuffer.Bind();
            this._vertexBuffer.EnableAttributes();

            this._vertexIndexesBuffer.Bind();

            Matrix4 modelViewProjection = this.ActorTransform.GetModelMatrix() * viewMatrix * projectionMatrix;

            this._boxShader.Enable();
            this._boxShader.SetMVPMatrix(modelViewProjection);
            this._boxShader.SetLineColour(this.LineColour);

            // Now draw the box
            GL.DrawElements
            (
                PrimitiveType.LineLoop,
                24,
                DrawElementsType.UnsignedByte,
                new IntPtr(0)
            );

            this._vertexBuffer.DisableAttributes();
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the object has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        private void ThrowIfDisposed()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(ToString());
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.IsDisposed = true;

            this._vertexBuffer.Dispose();
            this._vertexIndexesBuffer.Dispose();
        }
    }
}
