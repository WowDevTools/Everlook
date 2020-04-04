//
//  Buffer.cs
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
using System.Runtime.InteropServices;
using Everlook.Viewport.Rendering.Interfaces;
using OpenTK.Graphics.OpenGL;

namespace Everlook.Viewport.Rendering.Core
{
    /// <summary>
    /// Represents a native OpenGL data buffer.
    /// </summary>
    /// <typeparam name="T">Any structure.</typeparam>
    public sealed class Buffer<T> : IDisposable, IBuffer where T : struct
    {
        private readonly int _nativeBufferID;

        /// <inheritdoc />
        public BufferTarget Target { get; }

        /// <inheritdoc />
        public BufferUsageHint Usage { get; }

        /// <inheritdoc />
        public int Length { get; private set; }

        /// <summary>
        /// Gets the attribute pointers of the buffer.
        /// </summary>
        private ICollection<VertexAttributePointer> Attributes { get; }

        /// <summary>
        /// Gets or sets the data contained in the buffer. This is an expensive operation.
        /// </summary>
        public T[] Data
        {
            get
            {
                Bind();

                var bufferData = new T[this.Length / Marshal.SizeOf<T>()];
                GL.GetBufferSubData(this.Target, IntPtr.Zero, this.Length, bufferData);

                return bufferData;
            }

            set
            {
                Bind();

                this.Length = value.Length * Marshal.SizeOf<T>();
                GL.BufferData(this.Target, this.Length, value, this.Usage);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer{T}"/> class.
        /// </summary>
        /// <param name="target">The intended use of the buffer.</param>
        /// <param name="usage">A hint as to how the buffer's data might be read or written.</param>
        public Buffer(BufferTarget target, BufferUsageHint usage)
        {
            this.Target = target;
            this.Usage = usage;
            this.Attributes = new List<VertexAttributePointer>();

            this._nativeBufferID = GL.GenBuffer();
        }

        /// <inheritdoc />
        public void AttachAttributePointer(VertexAttributePointer attributePointer)
        {
            if (attributePointer == null)
            {
                throw new ArgumentNullException(nameof(attributePointer));
            }

            Bind();
            this.Attributes.Add(attributePointer);
        }

        /// <inheritdoc />
        public void AttachAttributePointers(IEnumerable<VertexAttributePointer> attributePointers)
        {
            if (attributePointers == null)
            {
                throw new ArgumentNullException(nameof(attributePointers));
            }

            Bind();
            foreach (var attributePointer in attributePointers)
            {
                this.Attributes.Add(attributePointer);
            }
        }

        /// <inheritdoc />
        public void EnableAttributes()
        {
            foreach (var attribute in this.Attributes)
            {
                attribute.Enable();
            }
        }

        /// <inheritdoc />
        public void DisableAttributes()
        {
            foreach (var attribute in this.Attributes)
            {
                attribute.Disable();
            }
        }

        /// <inheritdoc />
        public void Bind()
        {
            GL.BindBuffer(this.Target, this._nativeBufferID);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            GL.DeleteBuffer(this._nativeBufferID);
        }
    }
}
