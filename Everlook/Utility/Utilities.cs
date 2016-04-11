//
//  Utilities.cs
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
using Gtk;

namespace Everlook.Utility
{
	/// <summary>
	/// Collection of small utility functions that make life easier.
	/// </summary>
	public static class Utilities
	{
		/// <summary>
		/// Converts any non-native path separators to the current native path separator,
		/// e.g backslashes to forwardslashes on *nix, and vice versa.
		/// </summary>
		/// <returns>The path.</returns>
		/// <param name="inputPath">Input path.</param>
		public static string CleanPath(string inputPath)
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
		public static string GetIconForFiletype(string file)
		{
			string fileIcon = Stock.File;

			if (file.EndsWith(".m2"))
			{
				// Blender armature icon?
			}
			else if (file.EndsWith(".wmo"))
			{
				// Blender object icon?
			}
			else if (file.EndsWith(".blp") || file.EndsWith(".jpg") || file.EndsWith(".gif"))
			{
				fileIcon = "image-x-generic";
			}
			else if (file.EndsWith(".wav") || file.EndsWith(".mp3") || file.EndsWith(".ogg"))
			{
				fileIcon = "audio-x-generic";
			}
			else if (file.EndsWith(".txt"))
			{
				fileIcon = "text-x-generic";
			}
			else if (file.EndsWith(".dbc") || file.EndsWith(".wdt"))
			{
				fileIcon = "x-office-spreadsheet";
			}
			else if (file.EndsWith(".exe"))
			{
				fileIcon = "application-x-executable";
			}
			else if (file.EndsWith(".dll"))
			{
				fileIcon = "application-x-executable";
			}
			else if (file.EndsWith(".wtf") || file.EndsWith(".ini"))
			{
				fileIcon = "text-x-script";
			}
			else if (file.EndsWith(".html") || file.EndsWith(".url"))
			{
				fileIcon = "text-html";
			}
			else if (file.EndsWith(".pdf"))
			{
				fileIcon = "x-office-address-book";
			}
			else if (file.EndsWith(".ttf") || file.EndsWith(".TTF"))
			{
				fileIcon = "font-x-generic";
			}
			else if (file.EndsWith(".wdl"))
			{
				fileIcon = "text-x-generic-template";
			}
			else if (file.EndsWith(".sbt"))
			{
				fileIcon = "text-x-generic";
			}
			else if (file.EndsWith(".zmp"))
			{
				fileIcon = "application-x-executable";
			}

			return fileIcon;
		}
	}
}

