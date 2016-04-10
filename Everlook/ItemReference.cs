//
//  FileReference.cs
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

namespace Everlook
{
	/// <summary>
	/// Represents a file stored in a game package. Holds the package name and path of the file
	/// inside the package.
	/// </summary>
	public class ItemReference: IEquatable<ItemReference>
	{
		/// <summary>
		/// Gets or sets the parent reference.
		/// </summary>
		/// <value>The parent reference.</value>
		public ItemReference ParentReference
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the name of the package where the file is stored.
		/// </summary>
		/// <value>The name of the package.</value>
		public string PackageName
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the file path of the file inside the package.
		/// </summary>
		/// <value>The file path.</value>
		public string ItemPath
		{
			get;
			set;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.ItemReference"/> class.
		/// </summary>
		/// <param name="InPackageName">In package name.</param>
		/// <param name="InFilePath">In file path.</param>
		public ItemReference(string InPackageName, string InFilePath)
		{
			this.PackageName = InPackageName;
			this.ItemPath = InFilePath;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.ItemReference"/> class by
		/// appending the provided subpath to the provided refererence's file path.
		/// </summary>
		/// <param name="InReference">In reference.</param>
		/// <param name="subPath">Sub directory.</param>
		public ItemReference(ItemReference InReference, string subPath)
		{
			this.ParentReference = InReference;
			this.PackageName = InReference.PackageName;
			this.ItemPath = InReference.ItemPath + subPath;
		}

		/// <summary>
		/// Determines whether the provided path is a file or not.
		/// </summary>
		/// <returns><c>true</c> if the path is a file; otherwise, <c>false</c>.</returns>
		public bool IsFile()
		{	
			if (ItemPath.EndsWith("\\"))
			{
				return false;
			}

			string[] parts = ItemPath.Split('.');
			return parts.Length > 1;
		}

		#region IEquatable implementation

		/// <summary>
		/// Determines whether the specified <see cref="Everlook.ItemReference"/> is equal to the current <see cref="Everlook.ItemReference"/>.
		/// </summary>
		/// <param name="other">The <see cref="Everlook.ItemReference"/> to compare with the current <see cref="Everlook.ItemReference"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="Everlook.ItemReference"/> is equal to the current
		/// <see cref="Everlook.ItemReference"/>; otherwise, <c>false</c>.</returns>
		public bool Equals(ItemReference other)
		{
			return 
			this.PackageName == other.PackageName &&
			this.ItemPath == other.ItemPath;
		}

		#endregion

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="Everlook.ItemReference"/>.
		/// </summary>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="Everlook.ItemReference"/>.</returns>
		public override string ToString()
		{
			return String.Format("{0}:{1}", PackageName, ItemPath);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="Everlook.ItemReference"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return this.ToString().GetHashCode();
		}
	}
}

