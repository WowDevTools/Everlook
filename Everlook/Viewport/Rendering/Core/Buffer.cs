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
using Silk.NET.OpenGL;

namespace Everlook.Viewport.Rendering.Core
{
    /// <summary>
    /// Represents a native OpenGL data buffer.
    /// </summary>
    /// <typeparam name="T">Any structure.</typeparam>
    public sealed class Buffer<T> : GraphicsObject, IDisposable, IBuffer where T : unmanaged
    {
        private readonly uint _nativeBufferID;

        /// <inheritdoc />
        public BufferTargetARB Target { get; }

        /// <inheritdoc />
        public BufferUsageARB Usage { get; }

        /// <inheritdoc />
        public ulong Length { get; private set; }

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

                var bufferData = new T[this.Length / (ulong)Marshal.SizeOf<T>()];
                unsafe
                {
                    fixed (void* ptr = bufferData)
                    {
                        this.GL.GetBufferSubData
                        (
                            this.Target,
                            IntPtr.Zero,
                            new UIntPtr(this.Length),
                            ptr
                        );
                    }
                }

                return bufferData;
            }

            set
            {
                Bind();

                this.Length = (ulong)value.Length * (ulong)Marshal.SizeOf<T>();

                unsafe
                {
                    fixed (void* ptr = value)
                    {
                        this.GL.BufferData(this.Target, new UIntPtr(this.Length), ptr, this.Usage);
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer{T}"/> class.
        /// </summary>
        /// <param name="gl">The OpenGL API.</param>
        /// <param name="target">The intended use of the buffer.</param>
        /// <param name="usage">A hint as to how the buffer's data might be read or written.</param>
        public Buffer(GL gl, BufferTargetARB target, BufferUsageARB usage)
            : base(gl)
        {
            this.Target = target;
            this.Usage = usage;
            this.Attributes = new List<VertexAttributePointer>();

            _nativeBufferID = this.GL.GenBuffer();
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
            this.GL.BindBuffer(this.Target, _nativeBufferID);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.GL.DeleteBuffer(_nativeBufferID);
        }
    }
}
