//
//  VertexAttributePointer.cs
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

using OpenTK.Graphics.OpenGL;

namespace Everlook.Viewport.Rendering.Core
{
	/// <summary>
	/// Represents a vertex attribute pointer.
	/// </summary>
	public class VertexAttributePointer
	{
		/// <summary>
		/// Gets the attribute array index that the pointer modifies.
		/// </summary>
		public int Index { get; }

		/// <summary>
		/// Gets the number of components per attribute.
		/// </summary>
		public int Size { get; }

		/// <summary>
		/// Gets the data type of each component in the array.
		/// </summary>
		public VertexAttribPointerType Type { get; }

		/// <summary>
		/// Gets a value indicating whether the values should be normalized.
		/// </summary>
		public bool IsNormalized { get; }

		/// <summary>
		/// Gets the byte offset between consecutive vertex attributes.
		/// </summary>
		public int Stride { get; }

		/// <summary>
		/// Gets the offset to the first component of the first attribute in the array.
		/// </summary>
		public int Offset { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="VertexAttributePointer"/> class.
		/// </summary>
		/// <param name="index">The attribute array layout index.</param>
		/// <param name="size">The number of components in the attribute.</param>
		/// <param name="type">The data type of each component in the attribute.</param>
		/// <param name="stride">The byte offset between consecutive attributes.</param>
		/// <param name="offset">The offset to the first component of the first attribute.</param>
		/// <param name="isNormalized">Whether or not the data should be normalized.</param>
		public VertexAttributePointer(int index, int size, VertexAttribPointerType type, int stride, int offset, bool isNormalized = false)
		{
			this.Index = index;
			this.Size = size;
			this.Type = type;

			this.Stride = stride;
			this.Offset = offset;

			this.IsNormalized = isNormalized;
		}

		/// <summary>
		/// Enables the attribute pointer.
		/// </summary>
		public void Enable()
		{
			GL.EnableVertexAttribArray(this.Index);
			GL.VertexAttribPointer
			(
				this.Index,
				this.Size,
				this.Type,
				this.IsNormalized,
				this.Stride,
				this.Offset
			);
		}

		/// <summary>
		/// Disables the array attribute modified by this pointer.
		/// </summary>
		public void Disable()
		{
			GL.DisableVertexAttribArray(this.Index);
		}
	}
}
