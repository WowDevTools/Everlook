//
//  IGameContext.cs
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
using Everlook.Database;
using Everlook.Explorer;
using Everlook.Package;
using Warcraft.Core;

namespace Everlook.Utility
{
    /// <summary>
    /// Represents a context object for a given game which can provide access to the file system, the database, and
    /// other relevant functions.
    /// </summary>
    public interface IGameContext
    {
        /// <summary>
        /// Gets the game version that the context is relevant for.
        /// </summary>
        WarcraftVersion Version { get; }

        /// <summary>
        /// Gets the database accessor for the context.
        /// </summary>
        ClientDatabaseProvider Database { get; }

        /// <summary>
        /// Gets the asset accessor for the context.
        /// </summary>
        PackageGroup Assets { get; }

        /// <summary>
        /// Gets the UI file tree for the context.
        /// </summary>
        FileTreeModel FileTree { get; }

        /// <summary>
        /// Gets the file reference pointing to a given path.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>A file reference pointing to the path.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the path is null or empty.</exception>
        FileReference GetReferenceForPath(string path);
    }
}
