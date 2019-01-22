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
using Everlook.Explorer;
using Gdk;
using OpenTK;
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
			if (notebook == null)
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
		/// e.g backslashes to forwardslashes on *nix, and vice versa.
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
			int platform = (int)Environment.OSVersion.Platform;
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
			if (fileReference == null)
			{
				throw new ArgumentNullException(nameof(fileReference));
			}

			return IconManager.GetIconForFiletype(fileReference.FilePath);
		}

		/// <summary>
		/// Converts the current OpenGL vector to a Warcraft vector structure.
		/// </summary>
		/// <param name="vector3">An <see cref="OpenTK.Vector3"/>-type vector.</param>
		/// <returns>A System.Numerics vector.</returns>
		public static System.Numerics.Vector3 ToSystemVector(this Vector3 vector3)
		{
			return new System.Numerics.Vector3(vector3.X, vector3.Y, vector3.Z);
		}

		/// <summary>
		/// Converts the current Warcraft vector to an OpenGL vector structure.
		/// </summary>
		/// <param name="vector3">A <see cref="System.Numerics.Vector3"/>-type vector.</param>
		/// <returns>An OpenTK vector.</returns>
		public static Vector3 ToOpenGLVector(this System.Numerics.Vector3 vector3)
		{
			return new Vector3(vector3.X, vector3.Y, vector3.Z);
		}

		/// <summary>
		/// Converts the current Warcraft vector to an OpenGL vector structure.
		/// </summary>
		/// <param name="vector2">A <see cref="System.Numerics.Vector2"/>-type vector.</param>
		/// <returns>An OpenTK vector.</returns>
		public static Vector2 ToOpenGLVector(this System.Numerics.Vector2 vector2)
		{
			return new Vector2(vector2.X, vector2.Y);
		}

		/// <summary>
		/// Converts the current Warcraft quaternion to an OpenGL quaternion structure.
		/// </summary>
		/// <param name="quaternion">A <see cref="System.Numerics.Quaternion"/>-type vector.</param>
		/// <returns>An OpenTK vector.</returns>
		public static Quaternion ToOpenGLQuaternion(this System.Numerics.Quaternion quaternion)
		{
			return new Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
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

			yield return top.ToOpenGLVector();
			yield return top.ToOpenGLVector() - new Vector3(xDiff, 0, 0);
			yield return top.ToOpenGLVector() - new Vector3(xDiff, yDiff, 0);
			yield return top.ToOpenGLVector() - new Vector3(0, yDiff, 0);

			yield return bottom.ToOpenGLVector();
			yield return bottom.ToOpenGLVector() + new Vector3(xDiff, 0, 0);
			yield return bottom.ToOpenGLVector() + new Vector3(xDiff, yDiff, 0);
			yield return bottom.ToOpenGLVector() + new Vector3(0, yDiff, 0);
		}
	}
}
