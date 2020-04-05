//
//  WarcraftGameContext.cs
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
using System.IO;
using Everlook.Database;
using Everlook.Explorer;
using Everlook.Package;
using Warcraft.Core;
using Warcraft.WMO.RootFile.Chunks;

namespace Everlook.Utility
{
    /// <summary>
    /// Represnts a context for a World of Warcraft version.
    /// </summary>
    public sealed class WarcraftGameContext : IGameContext
    {
        /// <inheritdoc />
        public WarcraftVersion Version { get; }

        /// <inheritdoc />
        public ClientDatabaseProvider Database { get; }

        /// <inheritdoc />
        public PackageGroup Assets { get; }

        /// <inheritdoc />
        public FileTreeModel FileTree { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WarcraftGameContext"/> class.
        /// </summary>
        /// <param name="version">The game version of the context.</param>
        /// <param name="assets">The location of the assets relevant to the context.</param>
        /// <param name="fileTree">The file tree relevant to the context.</param>
        /// <exception cref="ArgumentNullException">Thrown if the assets or the file tree are null.</exception>
        public WarcraftGameContext(WarcraftVersion version, PackageGroup assets, FileTreeModel fileTree)
        {
            if (assets is null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            if (fileTree is null)
            {
                throw new ArgumentNullException(nameof(fileTree));
            }

            this.Version = version;
            this.Assets = assets;
            this.FileTree = fileTree;

            this.Database = new ClientDatabaseProvider(this.Version, this.Assets);
        }

        /// <summary>
        /// Gets the file reference pointing to the model of a doodad instance.
        /// </summary>
        /// <param name="doodadInstance">The doodad instance to locate.</param>
        /// <returns>A file reference pointing to the instance.</returns>
        public FileReference GetReferenceForDoodad(DoodadInstance doodadInstance)
        {
            var doodadReference = GetReferenceForPath(doodadInstance.Name);
            if (doodadReference != null)
            {
                return doodadReference;
            }

            // Doodads may have the *.mdx extension instead of *.m2. Try with that as well.
            doodadReference = GetReferenceForPath(Path.ChangeExtension(doodadInstance.Name, "m2"));
            if (doodadReference is null)
            {
                throw new ArgumentException
                (
                    $"Failed to retrieve doodad reference for {doodadInstance.Name}",
                    nameof(doodadInstance)
                );
            }

            return doodadReference;
        }

        /// <inheritdoc />
        public FileReference? GetReferenceForPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            var treePath = this.FileTree.GetPath(path);
            if (treePath is null)
            {
                return null;
            }

            return this.FileTree.GetReferenceByPath(this, treePath);
        }
    }
}
