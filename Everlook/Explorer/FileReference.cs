//
//  FileReference.cs
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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Everlook.Utility;
using FileTree.Tree.Nodes;
using FileTree.Tree.Serialized;
using Warcraft.Core;
using Warcraft.MPQ.FileInfo;

namespace Everlook.Explorer
{
    /// <summary>
    /// Represents a file stored in a game package. Holds the package name and path of the file
    /// inside the package.
    /// </summary>
    public class FileReference : GLib.Object, IEquatable<FileReference>
    {
        /// <summary>
        /// Gets the package this reference belongs to.
        /// </summary>
        /// <value>The group.</value>
        public IGameContext Context { get; }

        /// <summary>
        /// Gets the node this reference maps to.
        /// </summary>
        public SerializedNode Node { get; }

        /// <summary>
        /// Gets the name of the package where the file is stored.
        /// </summary>
        /// <value>The name of the package.</value>
        public string PackageName { get; }

        /// <summary>
        /// Gets the file path of the file inside the package. This string uses the backslash character ('\') as its
        /// directory separator.
        /// </summary>
        /// <value>The file path.</value>
        public string FilePath { get; }

        /// <summary>
        /// Gets the directory that the file resides in.
        /// </summary>
        public string FileDirectory
        {
            get
            {
                var directory = Path.GetDirectoryName(this.FilePath.Replace('\\', Path.DirectorySeparatorChar));
                if (directory is null)
                {
                    throw new InvalidOperationException();
                }

                return directory;
            }
        }

        /// <summary>
        /// Gets the file info of this reference.
        /// </summary>
        /// <value>The file info.</value>
        public MPQFileInfo? ReferenceInfo
        {
            get
            {
                if (!this.IsFile)
                {
                    return null;
                }

                return this.Context.Assets.TryGetReferenceInfo(this, out var result) ? result : null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this reference is deleted in package it is stored in.
        /// </summary>
        /// <value><c>true</c> if this instance is deleted in package; otherwise, <c>false</c>.</value>
        public bool IsDeletedInPackage => this.Node.Type.HasFlag(NodeType.Deleted);

        /// <summary>
        /// Gets a value indicating whether this or not this reference is a package reference.
        /// </summary>
        /// <value><c>true</c> if this reference is a package; otherwise, <c>false</c>.</value>
        public bool IsPackage => this.Node.Type.HasFlag(NodeType.Package);

        /// <summary>
        /// Gets a value indicating whether this reference is a directory.
        /// </summary>
        /// <value><c>true</c> if this instance is directory; otherwise, <c>false</c>.</value>
        public bool IsDirectory => this.Node.Type.HasFlag(NodeType.Directory);

        /// <summary>
        /// Gets a value indicating whether this reference is a file.
        /// </summary>
        /// <value><c>true</c> if this instance is file; otherwise, <c>false</c>.</value>
        public bool IsFile => this.Node.Type.HasFlag(NodeType.File);

        /// <summary>
        /// Gets a value indicating whether this reference is a virtual reference.
        /// </summary>
        public bool IsVirtual => this.Node.Type.HasFlag(NodeType.Virtual);

        /// <summary>
        /// Gets the name of the file or directory.
        /// </summary>
        public string Filename
        {
            get
            {
                var filename = this.IsDirectory
                    ? Path.GetDirectoryName(this.FilePath.Replace('\\', Path.DirectorySeparatorChar))
                    : Path.GetFileName(this.FilePath.Replace('\\', Path.DirectorySeparatorChar));

                if (filename is null)
                {
                    throw new InvalidOperationException();
                }

                return filename;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileReference"/> class.
        /// </summary>
        /// <param name="gameContext">The game context for the reference.</param>
        /// <param name="node">The node object in the tree that this reference points to.</param>
        /// <param name="packageName">The name of the package this reference belongs to.</param>
        /// <param name="filePath">The complete file path this reference points to.</param>
        public FileReference(IGameContext gameContext, SerializedNode node, string packageName, string filePath)
        {
            this.PackageName = packageName;
            this.FilePath = filePath.Replace('/', '\\');
            this.Node = node;
            this.Context = gameContext;
        }

        /// <summary>
        /// Attempts to extract this instance from the package group it is associated with.
        /// </summary>
        /// <param name="data">The extracted data.</param>
        /// <returns>true if the data was successfully extracted; otherwise, false.</returns>
        public bool TryExtract([NotNullWhen(true)] out byte[]? data)
        {
            return this.Context.Assets.TryExtractVersionedReference(this, out data);
        }

        /// <summary>
        /// Gets the type of the referenced file.
        /// </summary>
        /// <returns>The referenced file type.</returns>
        public WarcraftFileType GetReferencedFileType()
        {
            return FileInfoUtilities.GetFileType(this.FilePath);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is FileReference other && Equals(other);
        }

        /// <inheritdoc />
        public bool Equals(FileReference other)
        {
            if (!(other is null))
            {
                return
                    this.Context.Equals(other.Context) &&
                    this.PackageName == other.PackageName &&
                    this.FilePath == other.FilePath;
            }
            return false;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.PackageName}:{this.FilePath}";
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return
            (
                this.PackageName.GetHashCode() +
                this.FilePath.GetHashCode() +
                this.Context.Assets.GroupName.GetHashCode()
            )
            .GetHashCode();
        }
    }
}
