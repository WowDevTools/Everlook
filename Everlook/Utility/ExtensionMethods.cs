//
//  ExtensionMethods.cs
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
using System.Numerics;
using Everlook.Explorer;
using Gdk;
using Warcraft.Core.Structures;

namespace Everlook.Utility
{
    /// <summary>
    /// Collection of small utility functions that make life easier.
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// Clears all pages from a notebook.
        /// </summary>
        /// <param name="notebook">The notebook to clear the pages from.</param>
        public static void ClearPages(this Gtk.Notebook notebook)
        {
            if (notebook is null)
            {
                throw new ArgumentNullException(nameof(notebook));
            }

            while (notebook.NPages > 0)
            {
                notebook.RemovePage(-1);
            }
        }

        /// <summary>
        /// Converts any non-native path separators to the current native path separator,
        /// e.g backslashes to forward slashes on *nix, and vice versa.
        /// </summary>
        /// <returns>The path.</returns>
        /// <param name="inputPath">Input path.</param>
        public static string ConvertPathSeparatorsToCurrentNativeSeparator(this string inputPath)
        {
            if (IsRunningOnUnix())
            {
                return inputPath.Replace('\\', '/');
            }

            return inputPath.Replace('/', '\\');
        }

        /// <summary>
        /// Determines if the application is running on a unix-like system.
        /// </summary>
        /// <returns><c>true</c> if is running on unix; otherwise, <c>false</c>.</returns>
        public static bool IsRunningOnUnix()
        {
            var platform = (int)Environment.OSVersion.Platform;
            if ((platform == 4) || (platform == 6) || (platform == 128))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the icon best representing this <see cref="FileReference"/>, based on its file extension.
        /// </summary>
        /// <param name="fileReference">The item reference for which the icon should be retrieved.</param>
        /// <returns>A pixel buffer containg the icon.</returns>
        public static Pixbuf GetIcon(this FileReference fileReference)
        {
            if (fileReference is null)
            {
                throw new ArgumentNullException(nameof(fileReference));
            }

            return IconManager.GetIconForFiletype(fileReference.FilePath);
        }

        /// <summary>
        /// Gets the coordinates of all corners in the box.
        /// </summary>
        /// <param name="box">The bounding box.</param>
        /// <returns>The corners.</returns>
        public static IEnumerable<Vector3> GetCorners(this Box box)
        {
            var top = box.TopCorner;
            var bottom = box.BottomCorner;

            var xDiff = top.X - bottom.X;
            var yDiff = top.Y - bottom.Y;

            yield return top;
            yield return top - new Vector3(xDiff, 0, 0);
            yield return top - new Vector3(xDiff, yDiff, 0);
            yield return top - new Vector3(0, yDiff, 0);

            yield return bottom;
            yield return bottom + new Vector3(xDiff, 0, 0);
            yield return bottom + new Vector3(xDiff, yDiff, 0);
            yield return bottom + new Vector3(0, yDiff, 0);
        }
    }
}
