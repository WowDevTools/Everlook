//
//  ShaderLinkingException.cs
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

namespace Everlook.Exceptions.Shader
{
	/// <summary>
	/// An exception thrown when a shader fails to link.
	/// </summary>
	public class ShaderLinkingException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ShaderLinkingException"/> class.
		/// </summary>
		public ShaderLinkingException()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ShaderLinkingException"/> class.
		/// </summary>
		/// <param name="message">The message to include with the exception.</param>
		public ShaderLinkingException(string message)
			: base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ShaderLinkingException"/> class.
		/// </summary>
		/// <param name="message">The message to include with the exception.</param>
		/// <param name="inner">The exception which caused this exception.</param>
		public ShaderLinkingException(string message, Exception inner)
			: base(message, inner)
		{
		}
	}
}
