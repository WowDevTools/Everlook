//
//  ExtensionMethods.cs
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
using Everlook.Explorer;
using Gdk;
using OpenTK;
using SlimTK;
using Warcraft.Core.Structures;

namespace Everlook.Utility
{
	/// <summary>
	/// Collection of small utility functions that make life easier.
	/// </summary>
	public static class ExtensionMethods
	{
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
			else
			{
				return inputPath.Replace('/', '\\');
			}
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
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Gets the icon best representing this <see cref="FileReference"/>, based on its file extension.
		/// </summary>
		/// <param name="fileReference">The item reference for which the icon should be retrieved.</param>
		/// <returns></returns>
		public static Pixbuf GetIcon(this FileReference fileReference)
		{
			return IconManager.GetIconForFiletype(fileReference.FilePath);
		}

		/// <summary>
		/// Converts the current OpenGL vector to a Warcraft vector structure.
		/// </summary>
		public static Vector3f ToWarcraftVector(this Vector3 vector3)
		{
			return new Vector3f(vector3.X, vector3.Y, vector3.Z);
		}

		/// <summary>
		/// Converts the current Warcraft vector to an OpenGL vector structure.
		/// </summary>
		public static Vector3 ToOpenGLVector(this Vector3f vector3f)
		{
			return new Vector3(vector3f.X, vector3f.Y, vector3f.Z);
		}

		/// <summary>
		/// Converts the current Warcraft box structure to an OpenGL box structure.
		/// </summary>
		/// <param name="box">The box to conver.t</param>
		/// <returns></returns>
		public static BoundingBox ToOpenGLBoundingBox(this Box box)
		{
			return new BoundingBox(box.BottomCorner.ToOpenGLVector(), box.TopCorner.ToOpenGLVector());
		}
	}
}

