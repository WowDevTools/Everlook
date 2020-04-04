//
//  PackageGroup.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Everlook.Explorer;
using log4net;
using Warcraft.MPQ;
using Warcraft.MPQ.FileInfo;

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
        /// Logger instance for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(PackageGroup));

        /// <summary>
        /// Gets the name of the package group.
        /// </summary>
        /// <value>The name of the group.</value>
        public string GroupName { get; }

        /// <summary>
        /// The packages handled by this package group.
        /// </summary>
        private readonly List<PackageInteractionHandler> _packages = new List<PackageInteractionHandler>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Everlook.Package.PackageGroup"/> class.
        /// </summary>
        /// <param name="groupName">The name of the group.</param>
        /// <param name="rootPackageDirectory">The root directory where packages should be searched for.</param>
        /// <exception cref="ArgumentNullException">Thrown if the name is null or empty.</exception>
        public PackageGroup(string groupName, string rootPackageDirectory)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentNullException(nameof(groupName), "A package group must be provided with a name.");
            }

            this.GroupName = groupName;

            LoadPackagesFromPath(rootPackageDirectory);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageGroup"/> class. The group is by default empty,
        /// and must be manually filled.
        /// </summary>
        /// <param name="groupName">The name of the package group.</param>
        /// <exception cref="ArgumentNullException">Thrown if the name is null or empty.</exception>
        public PackageGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentNullException(nameof(groupName), "A package group must be provided with a name.");
            }

            this.GroupName = groupName;
        }

        /// <summary>
        /// Creates a new package group, and asynchronously loads all of the packages in the provided directory.
        /// </summary>
        /// <param name="alias">The alias of the package group that's being loaded.</param>
        /// <param name="groupName">The name of the group that is to be created.</param>
        /// <param name="packageDirectory">The directory where the packages to load are.</param>
        /// <param name="ct">A <see cref="CancellationToken"/> which can be used to cancel the operation.</param>
        /// <param name="progress">The progress reporting object.</param>
        /// <returns>A loaded package group.</returns>
        public static async Task<PackageGroup> LoadAsync
        (
            string alias,
            string groupName,
            string packageDirectory,
            CancellationToken ct,
            IProgress<GameLoadingProgress>? progress = null
        )
        {
            var group = new PackageGroup(groupName);

            // Grab all packages in the game directory
            var packagePaths = Directory.EnumerateFiles(packageDirectory, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".mpq") || s.EndsWith(".MPQ"))
                .OrderBy(a => a)
                .ToList();

            // Internal counters for progress reporting
            double completedSteps = 0;
            double totalSteps = packagePaths.Count;
            foreach (var packagePath in packagePaths)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    progress?.Report(new GameLoadingProgress
                    {
                        CompletionPercentage = completedSteps / totalSteps,
                        State = GameLoadingState.LoadingPackages,
                        Alias = alias
                    });

                    var handler = await PackageInteractionHandler.LoadAsync(packagePath);
                    group.AddPackage(handler);

                    ++completedSteps;
                }
                catch (FileLoadException fex)
                {
                    Log.Warn($"FileLoadException for package \"{packagePath}\": {fex.Message}\n" +
                             $"Please report this on GitHub or via email.");
                }
                catch (NotImplementedException nex)
                {
                    Log.Warn($"NotImplementedException for package \"{packagePath}\": {nex.Message}\n" +
                             $"There's a good chance your game version isn't supported yet.");
                }
            }

            return group;
        }

        /// <summary>
        /// Loads all packages in the specified path into the package group. Packages are searched for in the specified
        /// directory, and all subdirectories.
        /// </summary>
        /// <param name="path">The path on disk where the packages are.</param>
        public void LoadPackagesFromPath(string path)
        {
            // Grab all packages in the game directory
            var packagePaths = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".mpq") || s.EndsWith(".MPQ"))
                .OrderBy(a => a)
                .ToList();

            foreach (var packagePath in packagePaths)
            {
                try
                {
                    AddPackage(new PackageInteractionHandler(packagePath));
                }
                catch (FileLoadException fex)
                {
                    Log.Warn($"FileLoadException for package \"{packagePath}\": {fex.Message}\n" +
                             $"Please report this on GitHub or via email.");
                }
                catch (NotImplementedException nex)
                {
                    Log.Warn($"NotImplementedException for package \"{packagePath}\": {nex.Message}\n" +
                             $"There's a good chance your game version isn't supported yet.");
                }
            }
        }

        /// <summary>
        /// Adds a package to the package group.
        /// </summary>
        /// <param name="package">The package to add.</param>
        /// <exception cref="ArgumentNullException">Thrown if the package is null.</exception>
        public void AddPackage(PackageInteractionHandler package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (_packages.Contains(package))
            {
                return;
            }

            _packages.Add(package);
        }

        /// <summary>
        /// Attempts to get the reference info for the specified reference. This method gets the most recent info for
        /// the file from overriding packages.
        /// </summary>
        /// <returns>The reference info.</returns>
        /// <param name="fileReference">Reference reference.</param>
        /// <param name="fileInfo">The file information.</param>
        public bool TryGetReferenceInfo(FileReference fileReference, [NotNullWhen(true)] out MPQFileInfo? fileInfo)
        {
            return TryGetFileInfo(fileReference.FilePath, out fileInfo);
        }

        /// <summary>
        /// Attempts to get the file info for the specified reference in its specific package. If the file does not
        /// exist in the package referenced in <paramref name="fileReference"/>, this method will return false.
        /// </summary>
        /// <returns>The reference info.</returns>
        /// <param name="fileReference">Reference reference.</param>
        /// <param name="fileInfo">The file info.</param>
        public bool TryGetVersionedReferenceInfo
        (
            FileReference fileReference,
            [NotNullWhen(true)] out MPQFileInfo? fileInfo
        )
        {
            var package = GetPackageByName(fileReference.PackageName);
            return package.TryGetReferenceInfo(fileReference, out fileInfo);
        }

        /// <summary>
        /// Attempts to extract a file from a specific package in the package group. If the file does not exist in
        /// the package referenced in <paramref name="fileReference"/>, this method returned will return null.
        /// </summary>
        /// <returns>The unversioned file or null.</returns>
        /// <param name="fileReference">Reference reference.</param>
        /// <param name="data">The data.</param>
        public bool TryExtractVersionedReference(FileReference fileReference, [NotNullWhen(true)] out byte[]? data)
        {
            data = null;

            if (fileReference.IsVirtual)
            {
                return TryExtractReference(fileReference, out data);
            }

            var package = GetPackageByName(fileReference.PackageName);
            return !(package is null) && package.TryExtractReference(fileReference, out data);
        }

        /// <summary>
        /// Attempts to extract a file from the package group. This method returns the most recently overridden version
        /// of the specified file with no regard for the origin package. The returned file may originate from the
        /// package referenced in the <paramref name="fileReference"/>, or it may originate from a patch package.
        ///
        /// If the file does not exist in any package, this method will return false.
        /// </summary>
        /// <returns>The file or null.</returns>
        /// <param name="fileReference">Reference reference.</param>
        /// <param name="data">The data.</param>
        public bool TryExtractReference(FileReference fileReference, [NotNullWhen(true)] out byte[]? data)
        {
            return TryExtractFile(fileReference.FilePath, out data);
        }

        /// <summary>
        /// Checks whether or not this package group contains the specified item reference.
        /// </summary>
        /// <returns><c>true</c>, if reference exist was doesed, <c>false</c> otherwise.</returns>
        /// <param name="fileReference">Reference reference.</param>
        public bool ContainsReference(FileReference fileReference)
        {
            if (fileReference == null)
            {
                throw new ArgumentNullException(nameof(fileReference));
            }

            return ContainsFile(fileReference.FilePath);
        }

        /// <summary>
        /// Gets a package handler by the name of the package.
        /// </summary>
        /// <returns>The package by name.</returns>
        /// <param name="packageName">Package name.</param>
        private PackageInteractionHandler GetPackageByName(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                throw new ArgumentException("Cannot find a package with an empty name.", nameof(packageName));
            }

            foreach (var package in _packages)
            {
                if (package.PackageName == packageName)
                {
                    return package;
                }
            }

            throw new ArgumentException
            (
                $"No package with the given name could be found: {packageName}",
                nameof(packageName)
            );
        }

        /// <inheritdoc />
        public bool TryExtractFile(string filePath, [NotNullWhen(true)] out byte[]? data)
        {
            data = null;

            for (var i = _packages.Count - 1; i >= 0; --i)
            {
                if (_packages[i].TryExtractFile(filePath, out data))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        [Obsolete]
        public byte[] ExtractFile(string filePath)
        {
            for (var i = _packages.Count - 1; i >= 0; --i)
            {
                if (_packages[i].TryExtractFile(filePath, out var data))
                {
                    return data;
                }
            }

            throw new FileNotFoundException("The specified file was not found in this package group.", filePath);
        }

        /// <inheritdoc />
        public bool HasFileList()
        {
            return false;
        }

        /// <inheritdoc />
        public IEnumerable<string>? GetFileList()
        {
            return null;
        }

        /// <inheritdoc />
        public bool ContainsFile(string filePath)
        {
            for (var i = _packages.Count - 1; i >= 0; --i)
            {
                if (_packages[i].ContainsFile(filePath))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public bool TryGetFileInfo(string filePath, [NotNullWhen(true)] out MPQFileInfo? fileInfo)
        {
            fileInfo = null;

            for (var i = _packages.Count - 1; i >= 0; --i)
            {
                if (_packages[i].ContainsFile(filePath))
                {
                    return _packages[i].TryGetFileInfo(filePath, out fileInfo);
                }
            }

            return false;
        }

        /// <inheritdoc />
        [Obsolete]
        public MPQFileInfo GetFileInfo(string filePath)
        {
            if (!TryGetFileInfo(filePath, out var fileInfo))
            {
                throw new FileNotFoundException("The specified file was not found in this package group.", filePath);
            }

            return fileInfo;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj is PackageGroup other)
            {
                return this.GroupName.Equals(other.GroupName) &&
                _packages.Equals(other._packages);
            }

            return false;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.GroupName;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (this.GroupName.GetHashCode() + _packages.GetHashCode()).GetHashCode();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var package in _packages)
            {
                package.Dispose();
            }
        }
    }
}
