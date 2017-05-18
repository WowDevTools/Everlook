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
using System.Drawing.Drawing2D;
using Warcraft.Core;
using System.IO;
using Everlook.Package;
using liblistfile.NodeTree;
using Warcraft.MPQ.FileInfo;
using FileNode = liblistfile.NodeTree.Node;

namespace Everlook.Explorer
{
	/// <summary>
	/// Represents a file stored in a game package. Holds the package name and path of the file
	/// inside the package.
	/// </summary>
	public class FileReference : GLib.Object, IEquatable<FileReference>
	{
		/// <summary>
		/// Gets the group this reference belongs to.
		/// </summary>
		/// <value>The group.</value>
		public PackageGroup PackageGroup { get; }

		/// <summary>
		/// Gets the node this reference maps to.
		/// </summary>
		public FileNode Node { get; }

		/// <summary>
		/// Gets the name of the package where the file is stored.
		/// </summary>
		/// <value>The name of the package.</value>
		public string PackageName { get; } = "";

		/// <summary>
		/// Gets the file path of the file inside the package.
		/// </summary>
		/// <value>The file path.</value>
		public string FilePath { get; } = "";

		/// <summary>
		/// Gets the file info of this reference.
		/// </summary>
		/// <value>The file info.</value>
		public MPQFileInfo ReferenceInfo
		{
			get
			{
				if (this.IsFile)
				{
					return this.PackageGroup.GetReferenceInfo(this);
				}

				return null;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this reference is deleted in package it is stored in.
		/// </summary>
		/// <value><c>true</c> if this instance is deleted in package; otherwise, <c>false</c>.</value>
		public bool IsDeletedInPackage => this.Node.Type.HasFlag(NodeType.Deleted);

		/// <summary>
		/// Gets a value indicating whether this or not this reference is a package reference.
		/// </summary>
		/// <value><c>true</c> if this reference is a package; otherwise, <c>false</c>.</value>
		public bool IsPackage => this.Node.Type.HasFlag(NodeType.Package);

		/// <summary>
		/// Gets a value indicating whether this reference is a directory.
		/// </summary>
		/// <value><c>true</c> if this instance is directory; otherwise, <c>false</c>.</value>
		public bool IsDirectory => this.Node.Type.HasFlag(NodeType.Directory);

		/// <summary>
		/// Gets a value indicating whether this reference is a file.
		/// </summary>
		/// <value><c>true</c> if this instance is file; otherwise, <c>false</c>.</value>
		public bool IsFile => this.Node.Type.HasFlag(NodeType.File);

		/// <summary>
		/// The name of the file or directory.
		/// </summary>
		public string Filename => this.IsDirectory ?
			Path.GetDirectoryName(this.FilePath.Replace('\\', Path.DirectorySeparatorChar)) :
			Path.GetFileName(this.FilePath.Replace('\\', Path.DirectorySeparatorChar));

		/// <summary>
		/// Initializes a new instance of the <see cref="FileReference"/> class.
		/// </summary>
		/// <param name="packageGroup">The package group this reference belongs to.</param>
		/// <param name="node">The node object in the tree that this reference points to.</param>
		/// <param name="packageName">The name of the package this reference belongs to.</param>
		/// <param name="filePath">The complete file path this reference points to.</param>
		public FileReference(PackageGroup packageGroup, Node node, string packageName, string filePath)
			: this(packageGroup)
		{
			this.PackageName = packageName;
			this.FilePath = filePath.Replace('/', '\\');
			this.Node = node;
		}
		/// <summary>
		/// Initializes a new instance of the <see cref="FileReference"/> class.
		/// </summary>
		/// <param name="packageGroup">PackageGroup.</param>
		public FileReference(PackageGroup packageGroup)
		{
			this.PackageGroup = packageGroup;
		}

		/// <summary>
		/// Extracts this instance from the package group it is associated with.
		/// </summary>
		public byte[] Extract()
		{
			return this.PackageGroup.ExtractVersionedReference(this);
		}

		/// <summary>
		/// Gets the type of the referenced file.
		/// </summary>
		/// <returns>The referenced file type.</returns>
		public WarcraftFileType GetReferencedFileType()
		{
			return FileInfoUtilities.GetFileType(this.FilePath);
		}

		#region IEquatable implementation

		/// <summary>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="FileReference"/>.
		/// </summary>
		/// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="FileReference"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="FileReference"/> is equal to the current
		/// <see cref="FileReference"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals(object obj)
		{
			FileReference other = obj as FileReference;
			return other != null && Equals(other);
		}

		/// <summary>
		/// Determines whether the specified <see cref="FileReference"/> is equal to the current <see cref="FileReference"/>.
		/// </summary>
		/// <param name="other">The <see cref="FileReference"/> to compare with the current <see cref="FileReference"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="FileReference"/> is equal to the current
		/// <see cref="FileReference"/>; otherwise, <c>false</c>.</returns>
		public bool Equals(FileReference other)
		{
			if (other != null)
			{
				return
					this.PackageGroup.Equals(other.PackageGroup) &&
					this.PackageName == other.PackageName &&
					this.FilePath == other.FilePath;
			}
			return false;
		}

		#endregion

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="FileReference"/>.
		/// </summary>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="FileReference"/>.</returns>
		public override string ToString()
		{
			return $"{this.PackageName}:{this.FilePath}";
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="FileReference"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (
				this.PackageName.GetHashCode() +
				this.FilePath.GetHashCode() +
				this.PackageGroup.GroupName.GetHashCode()
			).GetHashCode();

		}
	}
}