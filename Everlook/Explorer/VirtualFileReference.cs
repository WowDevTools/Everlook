//
//  VirtualFileReference.cs
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
using System.Collections.Generic;
using Everlook.Package;
using Warcraft.MPQ.FileInfo;

namespace Everlook.Explorer
{
	/// <summary>
	/// A virtual item reference. This type of item reference does not point to a specific file in a package, but
	/// rather acts as a shimmy on top of a number of underlying item references. It has a main "hard" item reference
	/// which is its primary target, and a number of other item references (0 or more) that are overridden by this
	/// hard reference.
	/// </summary>
	public class VirtualFileReference : FileReference
	{
		/// <summary>
		/// Gets the hard reference. The hard reference is the primary underlying package-specific
		/// reference to which this virtual reference points.
		/// </summary>
		/// <value>The hard reference.</value>
		public FileReference HardReference
		{
			get;
		}

		/// <summary>
		/// Gets the overridden hard references. This is a list of hard references that are overridden by
		/// the primary hard reference. See: (<see cref="HardReference"/>)
		/// </summary>
		/// <value>The overridden hard references.</value>
		public List<FileReference> OverriddenHardReferences
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets or sets the name of the package where the file is stored.
		/// </summary>
		/// <value>The name of the package.</value>
		public override string PackageName
		{
			get
			{
				return "";
			}
			set
			{
				throw new InvalidOperationException("The package name may not be set on a virtual reference.");
			}
		}

		/// <summary>
		/// Gets or sets the file path of the file inside the package.
		/// </summary>
		/// <value>The file path.</value>
		public override string FilePath
		{
			get
			{
				return this.HardReference.FilePath;
			}
			set
			{
				if (value != this.HardReference.FilePath)
				{
					throw new InvalidOperationException("The item path may not point to a file other than the one the base reference points to.");
				}

				base.FilePath = value;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this or not this reference is a package reference.
		/// </summary>
		/// <value>true</value>
		/// <c>false</c>
		public override bool IsPackage => false;

		/// <summary>
		/// Initializes a new instance of the <see cref="VirtualFileReference"/> class.
		/// </summary>
		/// <param name="inHardReference">The primary hard reference this virtual reference points to.</param>
		/// <param name="inPackageGroup">The package group this reference is a part of.</param>
		public VirtualFileReference(PackageGroup inPackageGroup, FileReference inHardReference)
		{
			this.OverriddenHardReferences = new List<FileReference>();

			this.PackageGroup = inPackageGroup;
			this.HardReference = inHardReference;
			this.FilePath = this.HardReference.FilePath;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="VirtualFileReference"/> class,
		/// where the reference has a parent <see cref="VirtualFileReference"/>.
		/// </summary>
		/// <param name="parentVirtualReference">Parent virtual reference.</param>
		/// <param name="inPackageGroup">In group.</param>
		/// <param name="inHardReference">In hard reference.</param>
		public VirtualFileReference(VirtualFileReference parentVirtualReference, PackageGroup inPackageGroup, FileReference inHardReference)
			: this(inPackageGroup, inHardReference)
		{
			this.ParentReference = parentVirtualReference;
		}

		/// <summary>
		/// Gets the file info of this reference.
		/// </summary>
		/// <value>The file info.</value>
		public override MPQFileInfo ReferenceInfo => this.HardReference.ReferenceInfo;

		/// <summary>
		/// Extracts this instance from the package group it is associated with.
		/// </summary>
		public override byte[] Extract()
		{
			return this.HardReference.Extract();
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="VirtualFileReference"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (base.GetHashCode() + this.HardReference.GetHashCode()).GetHashCode();
		}
	}
}

