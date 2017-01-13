//
//  PackageGroup.cs
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
using System.IO;
using System.Linq;
using Warcraft.MPQ.FileInfo;
using Warcraft.MPQ;
using Everlook.Configuration;
using Everlook.Explorer;

namespace Everlook.Package
{
	/// <summary>
	/// Package group. Handles a group of packages as one cohesive unit, where files residing in the
	/// same path in different packages can override one another in order to provide updates.
	///
	/// Files and packages are registered on an alphabetical first-in last-out basis.
	/// </summary>
	public sealed class PackageGroup : IDisposable, IPackage
	{
		/// <summary>
		/// Gets the name of the package group.
		/// </summary>
		/// <value>The name of the group.</value>
		public readonly string GroupName;

		/// <summary>
		/// The root package directory.
		/// </summary>
		private readonly string RootPackageDirectory;

		/// <summary>
		/// The packages handled by this package group.
		/// </summary>
		private readonly List<PackageInteractionHandler> Packages = new List<PackageInteractionHandler>();

		/// <summary>
		/// The package listfiles.
		/// Key: The package name.
		/// Value: A list of all files present in the package.
		/// </summary>
		public readonly Dictionary<string, List<string>> PackageListfiles = new Dictionary<string, List<string>>();

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Package.PackageGroup"/> class.
		/// </summary>
		public PackageGroup(string inGroupName, string inRootPackageDirectory)
		{
			if (string.IsNullOrEmpty(inGroupName))
			{
				throw new ArgumentNullException(inGroupName, "A package group must be provided with a name.");
			}

			this.RootPackageDirectory = inRootPackageDirectory;

			this.GroupName = inGroupName;

			// Grab all packages in the game directory
			List<string> packagePaths = Directory.EnumerateFiles(this.RootPackageDirectory, "*.*", SearchOption.AllDirectories)
				.OrderBy(a => a)
				.Where(s => s.EndsWith(".mpq") || s.EndsWith(".MPQ"))
				.ToList();

			foreach (string packagePath in packagePaths)
			{
				try
				{
					this.Packages.Add(new PackageInteractionHandler(packagePath));
				}
				catch (FileLoadException fex)
				{
					Console.WriteLine($"FileLoadException for package \"{packagePath}\": {fex.Message}");
				}
				catch (NotImplementedException nex)
				{
					Console.WriteLine($"NotImplementedException for package \"{packagePath}\": {nex.Message}");
				}
			}

			foreach (PackageInteractionHandler package in this.Packages)
			{
				if (BundledListfiles.Instance.HasListfileForPackage(package))
				{
					this.PackageListfiles.Add(package.PackageName, BundledListfiles.Instance.GetBundledListfile(package));
				}
				else
				{
					// Try lazy loading the listfile, since we may be the first package to request it.
					if (BundledListfiles.Instance.LoadListfileByPackage(package))
					{
						this.PackageListfiles.Add(package.PackageName, BundledListfiles.Instance.GetBundledListfile(package));
					}
					else
					{
						this.PackageListfiles.Add(package.PackageName, package.GetFileList());
					}
				}
			}
		}

		/// <summary>
		/// Gets the reference info for the specified reference. This method gets the most recent info for the file from
		/// overriding packages.
		/// </summary>
		/// <returns>The reference info.</returns>
		/// <param name="fileReference">Reference reference.</param>
		public MPQFileInfo GetReferenceInfo(FileReference fileReference)
		{
			for (int i = this.Packages.Count - 1; i >= 0; --i)
			{
				if (this.Packages[i].ContainsFile(fileReference))
				{
					return this.Packages[i].GetReferenceInfo(fileReference);
				}
			}
			return null;
		}


		/// <summary>
		/// Gets the file info for the specified reference in its specific package. If the file does not exist in
		/// the package referenced in <paramref name="fileReference"/>, this method returned will return null.
		/// </summary>
		/// <returns>The reference info.</returns>
		/// <param name="fileReference">Reference reference.</param>
		public MPQFileInfo GetUnversionedReferenceInfo(FileReference fileReference)
		{
			PackageInteractionHandler package = GetPackageByName(fileReference.PackageName);
			if (package != null)
			{
				return package.GetReferenceInfo(fileReference);
			}

			return null;
		}

		/// <summary>
		/// Extracts a file from a specific package in the package group. If the file does not exist in
		/// the package referenced in <paramref name="fileReference"/>, this method returned will return null.
		/// </summary>
		/// <returns>The unversioned file or null.</returns>
		/// <param name="fileReference">Reference reference.</param>
		public byte[] ExtractUnversionedReference(FileReference fileReference)
		{
			PackageInteractionHandler package = GetPackageByName(fileReference.PackageName);
			if (package != null)
			{
				return package.ExtractReference(fileReference);
			}

			return null;
		}

		/// <summary>
		/// Extracts a file from the package group. This method returns the most recently overridden version
		/// of the specified file with no regard for the origin package. The returned file may originate from the
		/// package referenced in the <paramref name="fileReference"/>, or it may originate from a patch package.
		///
		/// If the file does not exist in any package, this method will return null.
		/// </summary>
		/// <returns>The file or null.</returns>
		/// <param name="fileReference">Reference reference.</param>
		public byte[] ExtractReference(FileReference fileReference)
		{
			for (int i = this.Packages.Count - 1; i >= 0; --i)
			{
				if (this.Packages[i].ContainsFile(fileReference))
				{
					return this.Packages[i].ExtractReference(fileReference);
				}
			}
			return null;
		}

		/// <summary>
		/// Checks whether or not this package group contains the specified item reference.
		/// </summary>
		/// <returns><c>true</c>, if reference exist was doesed, <c>false</c> otherwise.</returns>
		/// <param name="fileReference">Reference reference.</param>
		public bool DoesReferenceExist(FileReference fileReference)
		{
			for (int i = this.Packages.Count - 1; i >= 0; --i)
			{
				if (this.Packages[i].ContainsFile(fileReference))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Gets a package handler by the name of the package.
		/// </summary>
		/// <returns>The package by name.</returns>
		/// <param name="packageName">Package name.</param>
		private PackageInteractionHandler GetPackageByName(string packageName)
		{
			foreach (PackageInteractionHandler package in this.Packages)
			{
				if (package.PackageName == packageName)
				{
					return package;
				}
			}

			return null;
		}

		#region IPackage implementation

		/// <summary>
		/// Extracts the file.
		/// </summary>
		/// <returns>The file.</returns>
		/// <param name="filePath">Reference path.</param>
		public byte[] ExtractFile(string filePath)
		{
			FileReference fileReference = new FileReference(this, null, "", filePath);
			return ExtractReference(fileReference);
		}

		/// <summary>
		/// Determines whether this archive has a listfile.
		/// </summary>
		/// <returns>true</returns>
		/// <c>false</c>
		public bool HasFileList()
		{
			return this.PackageListfiles.Count > 0;
		}

		/// <summary>
		/// Gets the best available listfile from the archive. If an external listfile has been provided,
		/// that one is prioritized over the one stored in the archive.
		/// </summary>
		/// <returns>The listfile.</returns>
		public List<string> GetFileList()
		{
			// Merge all listfiles in the package lists.

			List<List<string>> listFiles = new List<List<string>>();

			foreach (KeyValuePair<string, List<string>> listPair in this.PackageListfiles)
			{
				listFiles.Add(listPair.Value);
			}

			return listFiles.SelectMany(t => t).Distinct().ToList();
		}

		/// <summary>
		/// Checks if the specified file path exists in the archive.
		/// </summary>
		/// <returns>true</returns>
		/// <c>false</c>
		/// <param name="filePath">Reference path.</param>
		public bool ContainsFile(string filePath)
		{
			FileReference fileReference = new FileReference(this, null, "", filePath);
			return DoesReferenceExist(fileReference);
		}

		/// <summary>
		/// Gets the file info of the provided path.
		/// </summary>
		/// <returns>The file info, or null if the file doesn't exist in the archive.</returns>
		/// <param name="filePath">Reference path.</param>
		public MPQFileInfo GetFileInfo(string filePath)
		{
			FileReference fileReference = new FileReference(this, null, "", filePath);
			return GetReferenceInfo(fileReference);
		}

		/// <summary>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="Everlook.Package.PackageGroup"/>.
		/// </summary>
		/// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="Everlook.Package.PackageGroup"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="System.Object"/> is equal to the current
		/// <see cref="Everlook.Package.PackageGroup"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals(object obj)
		{
			PackageGroup other = obj as PackageGroup;
			if (other != null)
			{
				return this.GroupName.Equals(other.GroupName) &&
				this.RootPackageDirectory.Equals(other.RootPackageDirectory) &&
				this.Packages.Equals(other.Packages);
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Returns a formatted string describing the current object.
		/// </summary>
		public override string ToString()
		{
			return this.GroupName;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="Everlook.Package.PackageGroup"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (this.GroupName.GetHashCode() + this.RootPackageDirectory.GetHashCode() + this.Packages.GetHashCode()).GetHashCode();
		}

		#endregion

		/// <summary>
		/// Releases all resource used by the <see cref="Everlook.Package.PackageGroup"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Everlook.Package.PackageGroup"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="Everlook.Package.PackageGroup"/> in an unusable state. After
		/// calling <see cref="Dispose"/>, you must release all references to the <see cref="Everlook.Package.PackageGroup"/>
		/// so the garbage collector can reclaim the memory that the <see cref="Everlook.Package.PackageGroup"/> was occupying.</remarks>
		public void Dispose()
		{
			foreach (PackageInteractionHandler package in this.Packages)
			{
				package.Dispose();
			}
		}
	}
}

