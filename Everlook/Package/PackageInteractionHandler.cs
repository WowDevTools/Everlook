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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Everlook.Explorer;
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
        /// Gets the package path.
        /// </summary>
        /// <value>The package path.</value>
        public string? PackagePath
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the name of the package.
        /// </summary>
        /// <value>The name of the package.</value>
        public string PackageName
        {
            get
            {
                var filename = Path.GetFileNameWithoutExtension(this.PackagePath);
                if (filename is null)
                {
                    throw new InvalidOperationException();
                }

                return filename;
            }
        }

        private MPQ? _package;

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
                    var handler = new PackageInteractionHandler();
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
        private PackageInteractionHandler()
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
                _package = new MPQ(new FileStream(inPackagePath, FileMode.Open, FileAccess.Read, FileShare.Read));
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

        /// <inheritdoc />
        public bool TryExtractFile(string filePath, [NotNullWhen(true)] out byte[]? data)
        {
            data = null;

            if (_package is null)
            {
                return false;
            }

            return _package.TryExtractFile(filePath, out data);
        }

        /// <inheritdoc />
        [Obsolete]
        public byte[]? ExtractFile(string filePath)
        {
            return _package?.ExtractFile(filePath);
        }

        /// <inheritdoc />
        public bool HasFileList()
        {
            if (_package is null)
            {
                return false;
            }

            return _package.HasFileList();
        }

        /// <inheritdoc />
        public IEnumerable<string> GetFileList()
        {
            if (_package is null)
            {
                return new string[] { };
            }

            return _package.GetFileList();
        }

        /// <inheritdoc />
        public bool ContainsFile(string filePath)
        {
            if (_package is null)
            {
                return false;
            }

            return _package.ContainsFile(filePath);
        }

        /// <inheritdoc />
        public bool TryGetFileInfo(string filePath, [NotNullWhen(true)] out MPQFileInfo? fileInfo)
        {
            fileInfo = null;
            if (_package is null)
            {
                return false;
            }

            return _package.TryGetFileInfo(filePath, out fileInfo);
        }

        /// <inheritdoc />
        [Obsolete]
        public MPQFileInfo? GetFileInfo(string filePath)
        {
            try
            {
                return _package?.GetFileInfo(filePath);
            }
            catch (FileNotFoundException)
            {
                // TODO: YUCK
                return null;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _package?.Dispose();
        }
    }
}
