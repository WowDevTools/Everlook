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
using System.IO;
using System.Reflection;
using Gdk;
using Gtk;
using OpenTK;
using Warcraft.Core;

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
		/// Gets the icon that would best represent the provided file. This is
		/// usually the mimetype.
		/// </summary>
		/// <returns>The icon for the filetype.</returns>
		/// <param name="file">File.</param>
		public static Pixbuf GetIconForFiletype(string file)
		{
			Pixbuf icon = IconTheme.Default.LoadIcon(Stock.File, 16, 0);
			file = file.ToLower();

			if (file.EndsWith(".m2"))
			{
				icon = LoadEmbeddedVector("Blender-Armature-Icon");
			}
			else if (file.EndsWith(".wmo"))
			{
				icon = LoadEmbeddedVector("Blender-Object-Icon");
			}
			else if (file.EndsWith(".adt"))
			{
				icon = LoadEmbeddedVector("Blender-Planet-Icon");
			}
			else if (file.EndsWith(".wlw") || file.EndsWith(".wlq") || file.EndsWith(".wlm"))
			{
				icon = LoadEmbeddedVector("Blender-Wave-Icon");
			}
			else if (file.EndsWith(".blp") || file.EndsWith(".jpg") || file.EndsWith(".gif") || file.EndsWith(".png"))
			{
				icon = IconTheme.Default.LoadIcon("image-x-generic", 16, 0);
			}
			else if (file.EndsWith(".wav") || file.EndsWith(".mp3") || file.EndsWith(".ogg"))
			{
				icon = IconTheme.Default.LoadIcon("audio-x-generic", 16, 0);
			}
			else if (file.EndsWith(".txt"))
			{
				icon = IconTheme.Default.LoadIcon("text-x-generic", 16, 0);
			}
			else if (file.EndsWith(".dbc") || file.EndsWith(".wdt"))
			{
				icon = IconTheme.Default.LoadIcon("x-office-spreadsheet", 16, 0);
			}
			else if (file.EndsWith(".exe") || file.EndsWith(".dll"))
			{
				icon = IconTheme.Default.LoadIcon("application-x-executable", 16, 0);
			}
			else if (file.EndsWith(".wtf") || file.EndsWith(".ini"))
			{
				icon = IconTheme.Default.LoadIcon("text-x-script", 16, 0);
			}
			else if (file.EndsWith(".html") || file.EndsWith(".url"))
			{
				icon = IconTheme.Default.LoadIcon("text-html", 16, 0);
			}
			else if (file.EndsWith(".pdf"))
			{
				icon = IconTheme.Default.LoadIcon("x-office-address-book", 16, 0);
			}
			else if (file.EndsWith(".ttf"))
			{
				icon = IconTheme.Default.LoadIcon("font-x-generic", 16, 0);
			}
			else if (file.EndsWith(".wdl"))
			{
				icon = IconTheme.Default.LoadIcon("text-x-generic-template", 16, 0);
			}
			else if (file.EndsWith(".sbt"))
			{
				icon = IconTheme.Default.LoadIcon("text-x-generic", 16, 0);
			}
			else if (file.EndsWith(".zmp"))
			{
				icon = IconTheme.Default.LoadIcon("application-x-executable", 16, 0);
			}

			return icon;
		}

		private static Pixbuf LoadEmbeddedVector(string vectorName)
		{
			using (Stream shaderStream =
				Assembly.GetExecutingAssembly().GetManifestResourceStream($"Everlook.Content.Icons.{vectorName}.svg"))
			{
				if (shaderStream == null)
				{
					return null;
				}

				using (MemoryStream ms = new MemoryStream())
				{
					shaderStream.CopyTo(ms);

					return new Pixbuf(ms.ToArray(), 16, 16);
				}
			}
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
	}
}

