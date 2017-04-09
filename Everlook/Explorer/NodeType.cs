//
//  NodeType.cs
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

namespace Everlook.Explorer
{
	/// <summary>
	/// The type of node represented in the game explorer treeview. This is used for sorting nodes visible to the user.
	/// </summary>
	public enum NodeType
	{
		/// <summary>
		/// A package of distinct files.
		/// </summary>
		Package = 0,

		/// <summary>
		/// A directory that contains files.
		/// </summary>
		Directory = 1,

		/// <summary>
		/// A file.
		/// </summary>
		File = 2,

		/// <summary>
		/// A folder containing packages.
		/// </summary>
		PackageFolder = 3,

		/// <summary>
		/// A grouping of packages, acting as a unified file system.
		/// </summary>
		PackageGroup = 4,
	}
}

