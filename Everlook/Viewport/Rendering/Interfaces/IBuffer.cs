//
//  IBuffer.cs
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
using Everlook.Viewport.Rendering.Core;
using OpenTK.Graphics.OpenGL;

namespace Everlook.Viewport.Rendering.Interfaces
{
	/// <summary>
	/// Represents the public API of an OpenGL buffer.
	/// </summary>
	public interface IBuffer
	{
		/// <summary>
		/// Gets the intended use of the buffer.
		/// </summary>
		BufferTarget Target { get; }

		/// <summary>
		/// Gets a hinting value as to how the buffer's data might be read or written.
		/// </summary>
		BufferUsageHint Usage { get; }

		/// <summary>
		/// Gets the byte count of the data in the buffer.
		/// </summary>
		int Length { get; }

		/// <summary>
		/// Attaches the specified attribute pointer to the buffer.
		/// </summary>
		/// <param name="attributePointer">An attribute pointer.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="attributePointer"/> is null.</exception>
		void AttachAttributePointer(VertexAttributePointer attributePointer);

		/// <summary>
		/// Attaches the specified set of attribute pointers to the buffer.
		/// </summary>
		/// <param name="attributePointers">A set of attribute pointers.</param>
		/// <exception cref="ArgumentNullException">Thrown if <paramref name="attributePointers"/> is null.</exception>
		void AttachAttributePointers(IEnumerable<VertexAttributePointer> attributePointers);

		/// <summary>
		/// Enables the attribute arrays that are relevant for this buffer, as specified by its attached attribute
		/// pointers.
		/// </summary>
		void EnableAttributes();

		/// <summary>
		/// Disables the attribute arrays that are relevant for this buffer, as specified by its attached attribute
		/// pointers.
		/// </summary>
		void DisableAttributes();

		/// <summary>
		/// Binds the buffer as the current OpenGL object, making it available for use.
		/// </summary>
		void Bind();
	}
}
