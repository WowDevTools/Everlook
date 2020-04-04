//
//  PackageInteractionHandler.cs
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
using System.IO;
using System.Threading.Tasks;
using Everlook.Explorer;
using log4net;
using Warcraft.Core;
using Warcraft.MPQ;
using Warcraft.MPQ.FileInfo;

namespace Everlook.Package
{
    /// <summary>
    /// Package interaction handler. This class is responsible for loading a package and performing file operations
    /// on it.
    /// </summary>
    public sealed class PackageInteractionHandler : IDisposable, IPackage
    {
        /// <summary>
        /// Logger instance for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(PackageInteractionHandler));

        /// <summary>
        /// Gets the package path.
        /// </summary>
        /// <value>The package path.</value>
        public string PackagePath
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the name of the package.
        /// </summary>
        /// <value>The name of the package.</value>
        public string PackageName => Path.GetFileNameWithoutExtension(this.PackagePath);

        private MPQ _package;

        /// <summary>
        /// Asynchronously loads a package at the given path.
        /// </summary>
        /// <param name="packagePath">The path on disk where the package is.</param>
        /// <returns>A loaded PackageInteractionHandler.</returns>
        public static Task<PackageInteractionHandler> LoadAsync(string packagePath)
        {
            return Task.Run
            (
                () =>
                {
                    PackageInteractionHandler handler = new PackageInteractionHandler();
                    handler.Load(packagePath);

                    return handler;
                }
            );
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Everlook.Package.PackageInteractionHandler"/> class. This
        /// does not initialize or load any package information. Use <see cref="Load"/> to fill the handler, or
        /// <see cref="LoadAsync"/> to create it asynchronously.
        /// </summary>
        public PackageInteractionHandler()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Everlook.Package.PackageInteractionHandler"/> class and loads
        /// the package at the provided path.
        /// </summary>
        /// <param name="inPackagePath">The path on disk where the package is.</param>
        public PackageInteractionHandler(string inPackagePath)
        {
            Load(inPackagePath);
        }

        /// <summary>
        /// Loads the package at the specified path, binding it to the handler.
        /// </summary>
        /// <param name="inPackagePath">The path on disk where the package is.</param>
        /// <exception cref="FileNotFoundException">Thrown if no file exists at the given path.</exception>
        public void Load(string inPackagePath)
        {
            if (File.Exists(inPackagePath))
            {
                this._package = new MPQ(new FileStream(inPackagePath, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            else
            {
                throw new FileNotFoundException("No package could be found at the specified path.");
            }

            this.PackagePath = inPackagePath;
        }

        /// <summary>
        /// Checks if the package contains the specified file. This method only checks the file path.
        /// </summary>
        /// <returns><c>true</c>, if the package contains the file, <c>false</c> otherwise.</returns>
        /// <param name="fileReference">Reference reference.</param>
        public bool ContainsFile(FileReference fileReference)
        {
            if (!fileReference.IsFile)
            {
                return false;
            }

            return ContainsFile(fileReference.FilePath);
        }

        /// <summary>
        /// Extracts the specified reference from its associated package. This method only operates on the file path.
        /// </summary>
        /// <param name="fileReference">Reference reference.</param>
        /// <returns>The raw data of the file pointed to by the reference.</returns>
        public byte[] ExtractReference(FileReference fileReference)
        {
            if (!fileReference.IsFile)
            {
                throw new ArgumentException("The specified reference can't be extracted.", nameof(fileReference));
            }

            try
            {
                return ExtractFile(fileReference.FilePath);
            }
            catch (InvalidFileSectorTableException fex)
            {
                Log.Warn(
                    $"Failed to extract the file \"{fileReference.FilePath}\" due to an invalid sector table (\"{fex.Message}\").");
                throw;
            }
        }

        /// <summary>
        /// Gets a set of information about the specified package file, such as stored size, disk size
        /// and storage flags.
        /// </summary>
        /// <returns>The reference info.</returns>
        /// <param name="fileReference">Reference reference.</param>
        public MPQFileInfo GetReferenceInfo(FileReference fileReference)
        {
            if (!fileReference.IsFile)
            {
                throw new ArgumentException("The specified reference is not a file.", nameof(fileReference));
            }

            return GetFileInfo(fileReference.FilePath);
        }

        /// <inheritdoc />
        public bool TryExtractFile(string filePath, out byte[] data)
        {
            return this._package.TryExtractFile(filePath, out data);
        }

        /// <inheritdoc />
        public byte[] ExtractFile(string filePath)
        {
            return this._package.ExtractFile(filePath);
        }

        /// <inheritdoc />
        public bool HasFileList()
        {
            return this._package.HasFileList();
        }

        /// <inheritdoc />
        public IEnumerable<string> GetFileList()
        {
            return this._package.GetFileList();
        }

        /// <inheritdoc />
        public bool ContainsFile(string filePath)
        {
            return this._package.ContainsFile(filePath);
        }

        /// <inheritdoc />
        public bool TryGetFileInfo(string filePath, out MPQFileInfo fileInfo)
        {
            return this._package.TryGetFileInfo(filePath, out fileInfo);
        }

        /// <inheritdoc />
        public MPQFileInfo GetFileInfo(string filePath)
        {
            return this._package.GetFileInfo(filePath);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this._package.Dispose();
        }
    }
}
