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
using Everlook.Package;

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
		/// Contains a list of references that have this reference as a parent.
		/// </summary>
		public List<ItemReference> ChildReferences = new List<ItemReference>();

		/// <summary>
		/// Gets or sets the group this reference belongs to.
		/// </summary>
		/// <value>The group.</value>
		public PackageGroup Group
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
		/// Gets or sets a value indicating whether this reference has had its children enumerated.
		/// </summary>
		/// <value><c>true</c> if this instance is enumerated; otherwise, <c>false</c>.</value>
		public bool IsEnumerated
		{
			get;
			set;
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
						if (!childReference.IsEnumerated)
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
		/// Gets a value indicating whether this or not this reference is a package group.
		/// </summary>
		/// <value><c>true</c> if this reference is a package group; otherwise, <c>false</c>.</value>
		public bool IsGroup
		{
			get
			{
				return Group != null && !IsDirectory && !IsFile;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this or not this reference is a package.
		/// </summary>
		/// <value><c>true</c> if this reference is a package; otherwise, <c>false</c>.</value>
		public bool IsPackage
		{
			get
			{
				return !String.IsNullOrEmpty(ItemPath) && String.IsNullOrEmpty(ItemPath);
			}
		}

		/// <summary>
		/// Gets a value indicating whether this reference is a directory.
		/// </summary>
		/// <value><c>true</c> if this instance is directory; otherwise, <c>false</c>.</value>
		public bool IsDirectory
		{
			get
			{
				return !String.IsNullOrEmpty(ItemPath) && GetReferencedFileType() == WarcraftFileType.Directory;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this reference is a file.
		/// </summary>
		/// <value><c>true</c> if this instance is file; otherwise, <c>false</c>.</value>
		public bool IsFile
		{
			get
			{
				return !String.IsNullOrEmpty(ItemPath) && (GetReferencedFileType() != WarcraftFileType.Directory);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.ItemReference"/> class.
		/// </summary>
		/// <param name="InGroup">Group.</param>
		public ItemReference(PackageGroup InGroup)
		{
			this.Group = InGroup;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.ItemReference"/> class.
		/// </summary>
		/// <param name="InPackageName">In package name.</param>
		/// <param name="InFilePath">In file path.</param>
		public ItemReference(PackageGroup InGroup, ItemReference InReference, string InPackageName, string InFilePath)
		{
			this.Group = InGroup;
			this.ParentReference = InReference;
			this.PackageName = InPackageName;
			this.ItemPath = InFilePath;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.ItemReference"/> class by
		/// appending the provided subpath to the provided refererence's file path.
		/// </summary>
		/// <param name="InReference">In reference.</param>
		/// <param name="subPath">Sub directory.</param>
		public ItemReference(PackageGroup InGroup, ItemReference InReference, string subPath)
		{
			this.Group = InGroup;
			this.ParentReference = InReference;
			this.PackageName = InReference.PackageName;
			this.ItemPath = InReference.ItemPath + subPath;
		}

		/// <summary>
		/// Gets the name of the referenced item.
		/// </summary>
		/// <returns>The referenced item name.</returns>
		public string GetReferencedItemName()
		{
			string itemName;
			if (String.IsNullOrEmpty(ParentReference.ItemPath))
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
				int slashIndex = itemName.LastIndexOf("\\");
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
							return WarcraftFileType.WorldObjectModel;
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
			if (this.ParentReference != null)
			{
				return (this.ToString().GetHashCode() + this.ParentReference.GetHashCode()).GetHashCode();
			}
			else
			{
				return (this.ToString().GetHashCode() + 0).GetHashCode();			
			}
		}
	}
}

