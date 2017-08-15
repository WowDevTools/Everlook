//
//  ShaderCompilationException.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2016 Jarl Gullberg
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
using OpenTK.Graphics.OpenGL;

namespace Everlook.Exceptions.Shader
{
	/// <summary>
	/// An exception thrown when a shader fails to compile.
	/// </summary>
	public class ShaderCompilationException : Exception
	{
		/// <summary>
		/// Gets the type of shader which was being compiled.
		/// </summary>
		public ShaderType Type { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ShaderCompilationException"/> class.
		/// </summary>
		/// <param name="type">The shader type.</param>
		public ShaderCompilationException(ShaderType type)
		{
			this.Type = type;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ShaderCompilationException"/> class.
		/// </summary>
		/// <param name="type">The shader type.</param>
		/// <param name="message">The message to include with the exception.</param>
		public ShaderCompilationException(ShaderType type, string message)
			: base(message)
		{
			this.Type = type;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ShaderCompilationException"/> class.
		/// </summary>
		/// <param name="type">The shader type.</param>
		/// <param name="message">The message to include with the exception.</param>
		/// <param name="inner">The exception which caused this exception.</param>
		public ShaderCompilationException(ShaderType type, string message, Exception inner)
			: base(message, inner)
		{
			this.Type = type;
		}
	}
}
