//
//  Program.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gdk;
using GLib;
using Gtk;

namespace Everlook.Utility
{
	/// <summary>
	/// Handles loading and providing embedded GTK icons.
	/// </summary>
	public static class IconManager
	{
		/// <summary>
		/// Loads all embedded builtin icons into the application's icon theme.
		/// </summary>
		public static void LoadEmbeddedIcons()
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			string[] manifestResourceNames = executingAssembly
				.GetManifestResourceNames();
			IEnumerable<string> manifestIcons = manifestResourceNames
				.Where
				(
					path =>
					path.Contains(".Icons.") &&
					(
						path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
						path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
					)
				);

			foreach (string manifestIcon in manifestIcons)
			{
				string iconNameWithExtension = manifestIcon.Substring(manifestIcon.IndexOf("Icons.", StringComparison.Ordinal) +
				                                                      "Icons.".Length);
				string iconName = Path.GetFileNameWithoutExtension(iconNameWithExtension);


				Pixbuf iconBuffer = LoadEmbeddedImage(manifestIcon);
				if (iconBuffer != null)
				{
					IconTheme.AddBuiltinIcon(iconName, 16, iconBuffer);
				}
			}
		}

		/// <summary>
		/// Loads an embedded image from the resource manifest of a specified width and height.
		/// </summary>
		/// <param name="resourceName">The name of the resource to load.</param>
		/// <param name="width">The width of the output <see cref="Pixbuf"/>.</param>
		/// <param name="height">The height of the output <see cref="Pixbuf"/>.</param>
		/// <returns>A pixel buffer containing the image.</returns>
		private static Pixbuf LoadEmbeddedImage(string resourceName, int width = 16, int height = 16)
		{
			using (Stream vectorStream =
				Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
			{
				if (vectorStream == null)
				{
					return null;
				}

				using (MemoryStream ms = new MemoryStream())
				{
					vectorStream.CopyTo(ms);

					return new Pixbuf(ms.ToArray(), width, height);
				}
			}
		}

		/// <summary>
		/// Gets the specified icon as a pixel buffer. If the icon is not found in the current theme, or
		/// if loading should fail for any other reason, a default icon will be returned instead.
		/// </summary>
		/// <param name="iconName"></param>
		/// <returns></returns>
		public static Pixbuf GetIcon(string iconName)
		{
			try
			{
				return LoadIconPixbuf(iconName);
			}
			catch (GException gex)
			{
				Console.WriteLine($"Loading of icon \"{iconName}\" failed. Exception message: {gex.Message}");
				return LoadIconPixbuf("empty");
			}
		}

		/// <summary>
		/// Gets the icon that would best represent the provided file. This is
		/// usually the mimetype.
		/// </summary>
		/// <returns>The icon for the filetype.</returns>
		/// <param name="file">Reference.</param>
		public static Pixbuf GetIconForFiletype(string file)
		{
			Pixbuf icon = GetIcon(Stock.File);
			file = file.ToLower();

			if (file.EndsWith(".m2"))
			{
				icon = GetIcon("Blender-Armature-Icon");
			}
			else if (file.EndsWith(".wmo"))
			{
				icon = GetIcon("Blender-Object-Icon");
			}
			else if (file.EndsWith(".adt"))
			{
				icon = GetIcon("Blender-Planet-Icon");
			}
			else if (file.EndsWith(".wlw") || file.EndsWith(".wlq") || file.EndsWith(".wlm"))
			{
				icon = GetIcon("Blender-Wave-Icon");
			}
			else if (file.EndsWith(".blp") || file.EndsWith(".jpg") || file.EndsWith(".gif") || file.EndsWith(".png"))
			{
				icon = GetIcon("image-x-generic");
			}
			else if (file.EndsWith(".wav") || file.EndsWith(".mp3") || file.EndsWith(".ogg"))
			{
				icon = GetIcon("audio-x-generic");
			}
			else if (file.EndsWith(".txt"))
			{
				icon = GetIcon("text-x-generic");
			}
			else if (file.EndsWith(".dbc") || file.EndsWith(".wdt"))
			{
				icon = GetIcon("x-office-spreadsheet");
			}
			else if (file.EndsWith(".exe") || file.EndsWith(".dll") || file.EndsWith(".zmp"))
			{
				icon = GetIcon("application-x-executable");
			}
			else if (file.EndsWith(".wtf") || file.EndsWith(".ini"))
			{
				icon = GetIcon("text-x-script");
			}
			else if (file.EndsWith(".html") || file.EndsWith(".url"))
			{
				icon = GetIcon("text-html");
			}
			else if (file.EndsWith(".pdf"))
			{
				icon = GetIcon("x-office-address-book");
			}
			else if (file.EndsWith(".ttf"))
			{
				icon = GetIcon("font-x-generic");
			}
			else if (file.EndsWith(".wdl"))
			{
				icon = GetIcon("text-x-generic-template");
			}
			else if (file.EndsWith(".sbt") || file.EndsWith(".xml"))
			{
				icon = GetIcon("text-x-generic");
			}

			return icon;
		}

		/// <summary>
		/// Loads the pixel buffer for the specified icon. This method is unchecked and can
		/// throw exceptions.
		/// </summary>
		/// <param name="iconName">The name of the icon.</param>
		/// <param name="size">The desired size of the icon.</param>
		/// <exception cref="GException">
		/// Thrown for a number of reasons, but can be thrown if the icon is not present
		/// in the current icon theme.
		/// </exception>
		/// <returns></returns>
		private static Pixbuf LoadIconPixbuf(string iconName, int size = 16)
		{
			return IconTheme.Default.LoadIcon(iconName, size, IconLookupFlags.UseBuiltin);
		}
	}
}