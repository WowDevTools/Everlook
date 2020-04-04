//
//  RenderableImage.cs
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
using System.Collections.Generic;
using System.Numerics;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using Everlook.Viewport.Rendering.Shaders;
using JetBrains.Annotations;
using Silk.NET.OpenGL;
using Warcraft.Core.Extensions;
using Warcraft.Core.Structures;

namespace Everlook.Viewport.Rendering
{
    /// <summary>
    /// Represents a renderable 2D image, and contains common functionality required to render one.
    /// </summary>
    public abstract class RenderableImage : GraphicsObject, IRenderable, IActor, IDefaultCameraPositionProvider
    {
        /// <summary>
        /// Gets a reference to the global shader cache.
        /// </summary>
        protected RenderCache RenderCache { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this object has been disposed.
        /// </summary>
        private bool IsDisposed { get; set; }

        /// <inheritdoc />
        public bool IsStatic => true;

        /// <inheritdoc />
        public Transform ActorTransform { get; set; } = new Transform();

        /// <inheritdoc />
        public bool IsInitialized { get; set; }

        /// <summary>
        /// Gets the native OpenGL ID for the vertex buffer.
        /// </summary>
        protected Buffer<Vector2>? VertexBuffer { get; private set; }

        /// <summary>
        /// Gets the native OpenGL ID for the UV coordinate buffer.
        /// </summary>
        protected Buffer<Vector2>? UVBuffer { get; private set; }

        /// <summary>
        /// Gets the native OpenGL ID for the vertex index buffer.
        /// </summary>
        protected Buffer<ushort>? VertexIndexBuffer { get; private set; }

        /// <summary>
        /// Gets or sets the native OpenGL ID for the image on the GPU.
        /// </summary>
        protected Texture2D? Texture { get; set; }

        /// <summary>
        /// Gets or sets the native OpenGL ID for the unlit 2D shader.
        /// </summary>
        protected Plain2DShader? Shader { get; set; }

        /// <summary>
        /// Gets or sets the path to the encapsulated texture in the package group.
        /// </summary>
        protected string? TexturePath { get; set; }

        /// <inheritdoc />
        public ProjectionType Projection => ProjectionType.Orthographic;

        /// <inheritdoc />
        public Vector3 DefaultCameraPosition => new Vector3(0.0f, 0.0f, 1.0f);

        /// <summary>
        /// Gets or sets a value indicating whether or not the red channel should be rendered.
        /// </summary>
        public bool RenderRedChannel { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not the green channel should be rendered.
        /// </summary>
        public bool RenderGreenChannel { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not the blue channel should be rendered.
        /// </summary>
        public bool RenderBlueChannel { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not the alpha channel should be rendered.
        /// </summary>
        public bool RenderAlphaChannel { get; set; } = true;

        /// <summary>
        /// Gets a vector based on <see cref="RenderRedChannel"/>, <see cref="RenderGreenChannel"/>,
        /// <see cref="RenderBlueChannel"/> and <see cref="RenderAlphaChannel"/> that is multiplied with the final
        /// texture sampling to mask out channels.
        /// TODO: Investigate whether or not this is a problem memory-wise, new vector created every frame.
        /// </summary>
        public Vector4 ChannelMask => new Vector4
        (
            this.RenderRedChannel ? 1.0f : 0.0f,
            this.RenderGreenChannel ? 1.0f : 0.0f,
            this.RenderBlueChannel ? 1.0f : 0.0f,
            this.RenderAlphaChannel ? 1.0f : 0.0f
        );

        /// <summary>
        /// Gets the number of mipmap levels for this image.
        /// </summary>
        public uint MipCount => GetNumReasonableMipLevels();

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderableImage"/> class.
        /// </summary>
        /// <param name="gl">The OpenGL API.</param>
        /// <param name="renderCache">The rendering cache.</param>
        protected RenderableImage([NotNull] GL gl, RenderCache renderCache)
            : base(gl)
        {
            this.RenderCache = renderCache;
        }

        /// <summary>
        /// TODO: Put this in Warcraft.Core instead.
        /// </summary>
        /// <returns>The number of reasonable mipmap levels.</returns>
        private uint GetNumReasonableMipLevels()
        {
            var smallestXRes = GetResolution().X;
            var smallestYRes = GetResolution().Y;

            uint mipLevels = 0;
            while (smallestXRes > 1 && smallestYRes > 1)
            {
                // Bisect the resolution using the current number of mip levels.
                smallestXRes = smallestXRes / (uint)Math.Pow(2, mipLevels);
                smallestYRes = smallestYRes / (uint)Math.Pow(2, mipLevels);

                ++mipLevels;
            }

            return mipLevels.Clamp<uint>(0, 15);
        }

        /// <inheritdoc />
        public void Initialize()
        {
            ThrowIfDisposed();

            if (this.IsInitialized)
            {
                return;
            }

            this.VertexBuffer = GenerateVertices();
            this.VertexIndexBuffer = GenerateVertexIndexes();
            this.UVBuffer = GenerateTextureCoordinates();

            // Use cached textures whenever possible
            this.Texture = LoadTexture();

            // Use cached shaders whenever possible
            this.Shader = this.RenderCache.GetShader(EverlookShader.Plain2D) as Plain2DShader;

            this.ActorTransform = new Transform();

            this.GL.Enable(EnableCap.Blend);
            this.GL.BlendEquation(BlendEquationModeEXT.FuncAdd);
            this.GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            this.IsInitialized = true;
        }

        /// <summary>
        /// Loads or creates a cached texture from the global texture cache using the path the image
        /// was constructed with as a key.
        /// </summary>
        /// <returns>A texture object.</returns>
        protected abstract Texture2D LoadTexture();

        /// <inheritdoc />
        public void Render(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, ViewportCamera camera)
        {
            ThrowIfDisposed();

            if (!this.IsInitialized)
            {
                return;
            }

            if
            (
                this.Shader is null ||
                this.VertexBuffer is null ||
                this.VertexIndexBuffer is null ||
                this.UVBuffer is null ||
                this.Texture is null
            )
            {
                return;
            }

            this.Shader.Enable();

            // Render the object
            // Send the vertices to the shader
            this.GL.EnableVertexAttribArray(0);
            this.VertexBuffer.Bind();

            unsafe
            {
                this.GL.VertexAttribPointer
                (
                    0u,
                    2,
                    VertexAttribPointerType.Float,
                    false,
                    0u,
                    (void*)0
                );
            }

            // Send the UV coordinates to the shader
            this.GL.EnableVertexAttribArray(1);
            this.UVBuffer.Bind();
            unsafe
            {
                this.GL.VertexAttribPointer
                (
                    1u,
                    2,
                    VertexAttribPointerType.Float,
                    false,
                    0u,
                    (void*)0
                );
            }

            // Set the channel mask
            this.Shader.SetChannelMask(this.ChannelMask);

            // Set the texture ID as a uniform sampler in unit 0
            this.Shader.SetTexture(this.Texture);

            this.GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // Set the model view matrix
            var modelViewProjection = this.ActorTransform.GetModelMatrix() * viewMatrix * projectionMatrix;

            // Send the model matrix to the shader
            this.Shader.SetMVPMatrix(modelViewProjection);

            // Finally, draw the image
            this.VertexIndexBuffer.Bind();
            unsafe
            {
                this.GL.DrawElements(PrimitiveType.Triangles, 6u, DrawElementsType.UnsignedShort, (void*)0);
            }

            // Release the attribute arrays
            this.GL.DisableVertexAttribArray(0);
            this.GL.DisableVertexAttribArray(1);
        }

        /// <summary>
        /// Gets the resolution of the encapsulated image.
        /// </summary>
        /// <returns>The resolution of the image.</returns>
        protected abstract Resolution GetResolution();

        /// <summary>
        /// Generates the four corner vertices of the encapsulated image.
        /// </summary>
        /// <returns>The vertex buffer.</returns>
        protected Buffer<Vector2> GenerateVertices()
        {
            // Generate vertex positions
            var halfWidth = GetResolution().X / 2;
            var halfHeight = GetResolution().Y / 2;

            var vertexPositions = new List<Vector2>
            {
                new Vector2(-halfWidth, halfHeight),
                new Vector2(halfWidth, halfHeight),
                new Vector2(-halfWidth, -halfHeight),
                new Vector2(halfWidth, -halfHeight)
            };

            // Buffer the generated vertices in the GPU
            return new Buffer<Vector2>(this.GL, BufferTargetARB.ArrayBuffer, BufferUsageARB.StaticDraw)
            {
                Data = vertexPositions.ToArray()
            };
        }

        /// <summary>
        /// Generates a vertex index buffer for the four corner vertices.
        /// </summary>
        /// <returns>The vertex index buffer.</returns>
        protected Buffer<ushort> GenerateVertexIndexes()
        {
            // Generate vertex indexes
            var vertexIndexes = new List<ushort> { 1, 0, 2, 2, 3, 1 };

            return new Buffer<ushort>(this.GL, BufferTargetARB.ElementArrayBuffer, BufferUsageARB.StaticDraw)
            {
                Data = vertexIndexes.ToArray()
            };
        }

        /// <summary>
        /// Generates a UV coordinate buffer for the four corner vertices.
        /// </summary>
        /// <returns>The native OpenGL ID of the buffer.</returns>
        protected Buffer<Vector2> GenerateTextureCoordinates()
        {
            // Generate UV coordinates
            var textureCoordinates = new List<Vector2>
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };

            // Buffer the generated UV coordinates in the GPU
            return new Buffer<Vector2>(this.GL, BufferTargetARB.ArrayBuffer, BufferUsageARB.StaticDraw)
            {
                Data = textureCoordinates.ToArray()
            };
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the object has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        private void ThrowIfDisposed()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(ToString() ?? nameof(RenderableImage));
            }
        }

        /// <inheritdoc />
        public virtual void Dispose()
        {
            this.IsDisposed = true;

            this.VertexBuffer?.Dispose();
            this.VertexIndexBuffer?.Dispose();
            this.UVBuffer?.Dispose();
        }
    }
}
