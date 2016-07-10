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
using Warcraft.Core;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Everlook.Package;
using Warcraft.MPQ.FileInfo;

namespace Everlook.Explorer
{
	/// <summary>
	/// Represents a file stored in a game package. Holds the package name and path of the file
	/// inside the package.
	/// </summary>
	public class ItemReference : IEquatable<ItemReference>
	{
		/// <summary>
		/// Gets or sets the parent reference.
		/// </summary>
		/// <value>The parent reference.</value>
		public ItemReference ParentReference { get; set; }

		/// <summary>
		/// Contains a list of references that have this reference as a parent.
		/// </summary>
		public List<ItemReference> ChildReferences = new List<ItemReference>();

		/// <summary>
		/// Gets or sets the group this reference belongs to.
		/// </summary>
		/// <value>The group.</value>
		public PackageGroup PackageGroup { get; set; }

		/// <summary>
		/// Gets or sets the name of the package where the file is stored.
		/// </summary>
		/// <value>The name of the package.</value>
		public virtual string PackageName { get; set; } = "";

		/// <summary>
		/// Gets or sets the file path of the file inside the package.
		/// </summary>
		/// <value>The file path.</value>
		public virtual string ItemPath { get; set; } = "";

		public ReferenceState State
		{
			get;
			set;
		} = ReferenceState.NotEnumerated;

		/// <summary>
		/// Gets the file info of this reference.
		/// </summary>
		/// <value>The file info.</value>
		public virtual MPQFileInfo ReferenceInfo
		{
			get
			{
				if (this.IsFile)
				{
					return this.PackageGroup.GetReferenceInfo(this);
				}
				else
				{
					return null;
				}
			}
		}

		/// <summary>
		/// Walks through this reference's children and checks whether or not all of them have had their
		/// children enumerated. Depending on the depth of the item, this may be an expensive operation.
		/// </summary>
		/// <value><c>true</c> if this instance is fully enumerated; otherwise, <c>false</c>.</value>
		public bool IsFullyEnumerated
		{
			get
			{
				bool areChildrenEnumerated = true;
				foreach (ItemReference childReference in ChildReferences)
				{
					if (childReference.IsDirectory)
					{
						if (childReference.State != ReferenceState.Enumerated)
						{
							return false;
						}

						areChildrenEnumerated = areChildrenEnumerated & childReference.IsFullyEnumerated;
					}
				}

				return areChildrenEnumerated;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this reference is deleted in package it is stored in.
		/// </summary>
		/// <value><c>true</c> if this instance is deleted in package; otherwise, <c>false</c>.</value>
		public virtual bool IsDeletedInPackage
		{
			get
			{
				if (this.ReferenceInfo != null && this.IsFile)
				{
					return this.ReferenceInfo.IsDeleted;
				}
				else if (this.IsDirectory)
				{
					return false;
				}

				return false;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this or not this reference is a package reference.
		/// </summary>
		/// <value><c>true</c> if this reference is a package; otherwise, <c>false</c>.</value>
		public virtual bool IsPackage
		{
			get { return !string.IsNullOrEmpty(PackageName) && string.IsNullOrEmpty(ItemPath); }
		}

		/// <summary>
		/// Gets a value indicating whether this reference is a directory.
		/// </summary>
		/// <value><c>true</c> if this instance is directory; otherwise, <c>false</c>.</value>
		public bool IsDirectory
		{
			get { return !string.IsNullOrEmpty(ItemPath) && GetReferencedFileType() == WarcraftFileType.Directory; }
		}

		/// <summary>
		/// Gets a value indicating whether this reference is a file.
		/// </summary>
		/// <value><c>true</c> if this instance is file; otherwise, <c>false</c>.</value>
		public bool IsFile
		{
			get { return !string.IsNullOrEmpty(ItemPath) && (GetReferencedFileType() != WarcraftFileType.Directory); }
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Explorer.ItemReference"/> class.
		/// This creates a new, empty item reference.
		/// </summary>
		protected ItemReference()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Explorer.ItemReference"/> class.
		/// </summary>
		/// <param name="inPackageGroup">The package group this reference belongs to.</param>
		/// <param name="inParentReference">The parent of this item reference.</param>
		/// <param name="inPackageName">The name of the package this reference belongs to.</param>
		/// <param name="inFilePath">The complete file path this reference points to.</param>
		public ItemReference(PackageGroup inPackageGroup, ItemReference inParentReference, string inPackageName, string inFilePath)
			: this(inPackageGroup)
		{
			this.ParentReference = inParentReference;
			this.PackageName = inPackageName;
			this.ItemPath = inFilePath;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Explorer.ItemReference"/> class by
		/// appending the provided subpath to the provided refererence's file path.
		/// </summary>
		/// <param name="inPackageGroup">The package group this reference belongs to.</param>
		/// <param name="inParentReference">In reference.</param>
		/// <param name="subPath">Sub directory.</param>
		public ItemReference(PackageGroup inPackageGroup, ItemReference inParentReference, string subPath)
			: this(inPackageGroup)
		{
			this.ParentReference = inParentReference;
			this.PackageName = inParentReference.PackageName;
			this.ItemPath = inParentReference.ItemPath + subPath;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Explorer.ItemReference"/> class.
		/// </summary>
		/// <param name="inPackageGroup">PackageGroup.</param>
		public ItemReference(PackageGroup inPackageGroup)
		{
			this.PackageGroup = inPackageGroup;
		}

		/// <summary>
		/// Extracts this instance from the package group it is associated with.
		/// </summary>
		public virtual byte[] Extract()
		{
			return this.PackageGroup.ExtractUnversionedReference(this);
		}

		/// <summary>
		/// Gets the name of the referenced item.
		/// </summary>
		/// <returns>The referenced item name.</returns>
		public virtual string GetReferencedItemName()
		{
			string itemName;
			if (ParentReference == null || string.IsNullOrEmpty(ParentReference.ItemPath))
			{
				itemName = ItemPath;
			}
			else
			{
				itemName = ItemPath.Substring(ParentReference.ItemPath.Length);
			}

			if (IsDirectory)
			{
				// Remove the trailing slash from directory names.
				int slashIndex = itemName.LastIndexOf("\\", StringComparison.Ordinal);
				itemName = itemName.Substring(0, slashIndex);
			}

			return itemName;
		}

		/// <summary>
		/// Gets the type of the referenced file.
		/// </summary>
		/// <returns>The referenced file type.</returns>
		public WarcraftFileType GetReferencedFileType()
		{
			string itemPath = ItemPath.ToLower();
			if (!itemPath.EndsWith("\\"))
			{
				string fileExtension = Path.GetExtension(itemPath).Replace(".", "");

				switch (fileExtension)
				{
					case "mpq":
					{
						return WarcraftFileType.MoPaQArchive;
					}
					case "toc":
					{
						return WarcraftFileType.AddonManifest;
					}
					case "sig":
					{
						return WarcraftFileType.AddonManifestSignature;
					}
					case "wtf":
					{
						return WarcraftFileType.ConfigurationFile;
					}
					case "dbc":
					{
						return WarcraftFileType.DatabaseContainer;
					}
					case "bls":
					{
						return WarcraftFileType.Shader;
					}
					case "wlw":
					{
						return WarcraftFileType.TerrainWater;
					}
					case "wlq":
					{
						return WarcraftFileType.TerrainLiquid;
					}
					case "wdl":
					{
						return WarcraftFileType.TerrainLiquid;
					}
					case "wdt":
					{
						return WarcraftFileType.TerrainTable;
					}
					case "adt":
					{
						return WarcraftFileType.TerrainData;
					}
					case "blp":
					{
						return WarcraftFileType.BinaryImage;
					}
					case "trs":
					{
						return WarcraftFileType.Hashmap;
					}
					case "m2":
					case "mdx":
					{
						return WarcraftFileType.GameObjectModel;
					}
					case "wmo":
					{
						Regex groupDetectRegex = new Regex("(.+_[0-9]{3}.wmo)", RegexOptions.Multiline);

						if (groupDetectRegex.IsMatch(itemPath))
						{
							return WarcraftFileType.WorldObjectModelGroup;
						}
						else
						{
							return WarcraftFileType.WorldObjectModel;
						}
					}
					default:
					{
						return WarcraftFileType.Unknown;
					}
				}
			}
			else
			{
				return WarcraftFileType.Directory;
			}
		}

		#region IEquatable implementation

		/// <summary>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="Everlook.Explorer.ItemReference"/>.
		/// </summary>
		/// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="Everlook.Explorer.ItemReference"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="Everlook.Explorer.ItemReference"/> is equal to the current
		/// <see cref="Everlook.Explorer.ItemReference"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals(object obj)
		{
			ItemReference other = obj as ItemReference;
			return other != null && Equals(other);
		}

		/// <summary>
		/// Determines whether the specified <see cref="Everlook.Explorer.ItemReference"/> is equal to the current <see cref="Everlook.Explorer.ItemReference"/>.
		/// </summary>
		/// <param name="other">The <see cref="Everlook.Explorer.ItemReference"/> to compare with the current <see cref="Everlook.Explorer.ItemReference"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="Everlook.Explorer.ItemReference"/> is equal to the current
		/// <see cref="Everlook.Explorer.ItemReference"/>; otherwise, <c>false</c>.</returns>
		public bool Equals(ItemReference other)
		{
			if (other != null)
			{
				bool parentsEqual = false;
				if (this.ParentReference != null && other.ParentReference != null)
				{
					parentsEqual = this.ParentReference.Equals(other.ParentReference);
				}
				else if (this.ParentReference == null && other.ParentReference == null)
				{
					parentsEqual = true;
				}

				return
					parentsEqual &&
					this.PackageGroup == other.PackageGroup &&
					this.PackageName == other.PackageName &&
					this.ItemPath == other.ItemPath;
			}
			else
			{
				return false;
			}
		}

		#endregion

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="Everlook.Explorer.ItemReference"/>.
		/// </summary>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="Everlook.Explorer.ItemReference"/>.</returns>
		public override string ToString()
		{
			return $"{PackageName}:{ItemPath}";
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="Everlook.Explorer.ItemReference"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			if (this.ParentReference != null)
			{
				return (this.PackageName.GetHashCode() +
				        this.ItemPath.GetHashCode() +
				        this.ParentReference.GetHashCode() +
				        this.PackageGroup.GroupName.GetHashCode()
				       ).GetHashCode();
			}
			else
			{
				return (this.PackageName.GetHashCode() +
				        this.ItemPath.GetHashCode() +
				        0 +
				        this.PackageGroup.GroupName.GetHashCode()
				       ).GetHashCode();
			}
		}
	}
}