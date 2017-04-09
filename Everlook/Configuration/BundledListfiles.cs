//
//  BundledListfiles.cs
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

using System.Reflection;
using System.IO;
using System.Collections.Generic;
using liblistfile;
using Everlook.Package;

namespace Everlook.Configuration
{
	/// <summary>
	/// Wrapper class for the bundled optimized listfiles.
	/// </summary>
	public sealed class BundledListfiles
	{
		/// <summary>
		/// The exposed singleton instance of the bundled listfile handler.
		/// </summary>
		public static BundledListfiles Instance = new BundledListfiles();

		private readonly Dictionary<string, OptimizedListContainer> OptimizedLists = new Dictionary<string, OptimizedListContainer>();

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Configuration.BundledListfiles"/> class.
		/// </summary>
		private BundledListfiles()
		{
		}

		/// <summary>
		/// Loads the listfile for the specifed package and adds it to the local list of packages.
		/// </summary>
		/// <returns>
		/// Returns <value>false</value> if the package didn't have a listfile or if the listfile couldn't be loaded,
		/// <value>true</value> otherwise.
		/// </returns>
		public bool LoadListfileByPackage(PackageInteractionHandler inPackageHandler)
		{
			if (!HasBundledListfiles())
			{
				// We don't have any listfiles, so there's no use
				return false;
			}

			if (this.OptimizedLists.ContainsKey(inPackageHandler.PackageName))
			{
				// The package listfile container has already been loaded.
				// Thus, check if the container has a listfile for this package
				return HasListfileForPackage(inPackageHandler);
			}

			foreach (string bundledListfilePath in GetAvailableBundledListfilePaths())
			{
				if (Path.GetFileNameWithoutExtension(bundledListfilePath) == inPackageHandler.PackageName)
				{
					OptimizedListContainer bundledListfile = new OptimizedListContainer(File.ReadAllBytes(bundledListfilePath));

					// Keep the listfile container around, in case we need it.
					this.OptimizedLists.Add(bundledListfile.PackageName, bundledListfile);

					if (bundledListfile.ContainsPackageListfile(inPackageHandler.GetHashTableHash()))
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Determines whether the provided package handler has a bundled listfile.
		/// </summary>
		/// <returns><c>true</c> if this instance has listfile for package the specified PackageHandler; otherwise, <c>false</c>.</returns>
		/// <param name="inPackageHandler">Package handler.</param>
		public bool HasListfileForPackage(PackageInteractionHandler inPackageHandler)
		{
			return HasListfileForPackage(inPackageHandler.PackageName, inPackageHandler.GetHashTableHash());
		}

		/// <summary>
		/// Determines whether the package with the specified name and table hash has a bundled listfile.
		/// </summary>
		/// <returns><c>true</c> if the package has a listfile; otherwise, <c>false</c>.</returns>
		/// <param name="packageName">Package name.</param>
		/// <param name="packageTableHash">Package table hash.</param>
		public bool HasListfileForPackage(string packageName, byte[] packageTableHash)
		{
			if (this.OptimizedLists.ContainsKey(packageName))
			{
				return this.OptimizedLists[packageName].ContainsPackageListfile(packageTableHash);
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Gets the bundled listfile for the provided package handler.
		/// </summary>
		/// <returns>The bundled listfile.</returns>
		/// <param name="inPackageHandler">Package handler.</param>
		public List<string> GetBundledListfile(PackageInteractionHandler inPackageHandler)
		{
			return GetBundledListfile(inPackageHandler.PackageName, inPackageHandler.GetHashTableHash());
		}

		/// <summary>
		/// Gets the bundled listfile for the package with the specified name and table hash.
		/// </summary>
		/// <returns>The bundled listfile.</returns>
		/// <param name="packageName">Package name.</param>
		/// <param name="packageTableHash">Package table hash.</param>
		public List<string> GetBundledListfile(string packageName, byte[] packageTableHash)
		{
			return this.OptimizedLists[packageName].OptimizedLists[packageTableHash].OptimizedPaths;
		}

		/// <summary>
		/// Determines whether there are any bundled listfiles stored along with the program.
		/// </summary>
		/// <returns><c>true</c> if this instance has bundled listfiles; otherwise, <c>false</c>.</returns>
		private static bool HasBundledListfiles()
		{
			return Directory.Exists(GetBundledListfileFolderPath()) && GetBundledListfileCount() > 0;
		}

		/// <summary>
		/// Gets a list of the available bundled listfile containers.
		/// </summary>
		/// <returns>The available bundled listfile containers.</returns>
		private static IEnumerable<string> GetAvailableBundledListfilePaths()
		{
			return Directory.EnumerateFiles(GetBundledListfileFolderPath(), "*." + OptimizedListContainer.Extension, SearchOption.AllDirectories);
		}

		/// <summary>
		/// Gets the number of available bundled listfiles.
		/// </summary>
		/// <returns>The bundled listfile count.</returns>
		private static int GetBundledListfileCount()
		{
			return Directory.GetFiles(GetBundledListfileFolderPath()).Length;
		}

		/// <summary>
		/// Gets the path to the folder containing the bundled listfiles.
		/// </summary>
		/// <returns>The bundled listfile folder path.</returns>
		public static string GetBundledListfileFolderPath()
		{
			return $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}" +
				   $"{Path.DirectorySeparatorChar}Content{Path.DirectorySeparatorChar}Listfiles";
		}
	}
}

