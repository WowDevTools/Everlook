//
//  DataLoadingDelegates.cs
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

using Everlook.Explorer;
using Everlook.Viewport.Rendering.Interfaces;

namespace Everlook.Utility
{
	/// <summary>
	/// A set of delegate signatures which can be used to load models.
	/// </summary>
	public static class DataLoadingDelegates
	{
		/// <summary>
		/// A delegate which will load the specified FileReference into a fully realized object of type T.
		/// </summary>
		/// <param name="fileReference">The <see cref="FileReference"/> to load.</param>
		/// <typeparam name="T">The type that should be returned by the loading.</typeparam>
		/// <returns>An object of type <typeparamref name="T"/>.</returns>
		public delegate T LoadReference<out T>(FileReference fileReference);

		/// <summary>
		/// A delegate which will create an <see cref="IRenderable"/> from the specified item.
		/// </summary>
		/// <param name="renderableItem">The item to encapsulate in a renderable version of it.</param>
		/// <param name="fileReference">The file reference associated with the object.</param>
		/// <typeparam name="T">A type which can be encapsulated in another type implementing <see cref="IRenderable"/>.</typeparam>
		/// <returns>A renderable object.</returns>
		public delegate IRenderable CreateRenderable<in T>(T renderableItem, FileReference fileReference);
	}
}
