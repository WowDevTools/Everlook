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
using log4net;
using Warcraft.Core;

namespace Everlook.Utility
{
	/// <summary>
	/// Handles loading and providing embedded GTK icons.
	/// </summary>
	public static class IconManager
	{
		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(IconManager));

		private static Dictionary<(string iconName, int iconSize), Pixbuf> IconCache = new Dictionary<(string iconName, int iconSize), Pixbuf>();

		/// <summary>
		/// Loads all embedded builtin icons into the application's icon theme.
		/// </summary>
		public static void LoadEmbeddedIcons()
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			string[] manifestResourceNames = executingAssembly
				.GetManifestResourceNames();
			IEnumerable<string> manifestIcons = manifestResourceNames.Where
			(
				path =>
				path.Contains(".Icons.") &&
				(
					path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
					path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
				)
			);

			foreach (string manifestIconName in manifestIcons)
			{
				// Grab the second to last part of the resource name, that is, the filename before the extension.
				// Note that this assumes that there is only a single extension.
				string[] manifestNameParts = manifestIconName.Split('.');
				string iconName = manifestNameParts.ElementAt(manifestNameParts.Length - 2);

				Pixbuf iconBuffer = LoadEmbeddedImage(manifestIconName);
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
		/// <param name="iconName">The name of the icon.</param>
		/// <returns>A pixel buffer containing the icon.</returns>
		public static Pixbuf GetIcon(string iconName)
		{
			try
			{
				return LoadIconPixbuf(iconName);
			}
			catch (GException gex)
			{
				Log.Warn($"Loading of icon \"{iconName}\" failed. Exception message: {gex.Message}\n" +
						 $"A fallback icon will be used instead.");
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
			return GetIconForFiletype(FileInfoUtilities.GetFileType(file));
		}

		/// <summary>
		/// Gets the icon that would best represent the provided file. This is
		/// usually the mimetype.
		/// </summary>
		/// <returns>The icon for the filetype.</returns>
		/// <param name="fileType">The file type.</param>
		public static Pixbuf GetIconForFiletype(WarcraftFileType fileType)
		{
			switch (fileType)
			{
				case WarcraftFileType.Directory:
				{
					return GetIcon(Stock.Directory);
				}
				case WarcraftFileType.MoPaQArchive:
				{
					return GetIcon("package-x-generic");
				}
				case WarcraftFileType.TerrainTable:
				case WarcraftFileType.DatabaseContainer:
				case WarcraftFileType.Hashmap:
				{
					return GetIcon("x-office-spreadsheet");
				}
				case WarcraftFileType.TerrainWater:
				case WarcraftFileType.TerrainLiquid:
				{
					return GetIcon("Blender-Wave-Icon");
				}
				case WarcraftFileType.TerrainLevel:
				{
					return GetIcon("text-x-generic-template");
				}
				case WarcraftFileType.TerrainData:
				{
					return GetIcon("Blender-Planet-Icon");
				}
				case WarcraftFileType.GameObjectModel:
				{
					return GetIcon("Blender-Armature-Icon");
				}
				case WarcraftFileType.WorldObjectModel:
				case WarcraftFileType.WorldObjectModelGroup:
				{
					return GetIcon("Blender-Object-Icon");
				}
				case WarcraftFileType.WaveAudio:
				case WarcraftFileType.MP3Audio:
				case WarcraftFileType.VorbisAudio:
				case WarcraftFileType.WMAAudio:
				{
					return GetIcon("audio-x-generic");
				}
				case WarcraftFileType.Subtitles:
				{
					return GetIcon("gnome-subtitles");
				}
				case WarcraftFileType.Text:
				case WarcraftFileType.AddonManifest:
				case WarcraftFileType.AddonManifestSignature:
				{
					return GetIcon("text-x-generic");
				}
				case WarcraftFileType.GIFImage:
				{
					return GetIcon("image-gif");
				}
				case WarcraftFileType.PNGImage:
				{
					return GetIcon("image-png");
				}
				case WarcraftFileType.JPGImage:
				{
					return GetIcon("image-jpeg");
				}
				case WarcraftFileType.IconImage:
				{
					return GetIcon("image-x-ico");
				}
				case WarcraftFileType.BitmapImage:
				{
					return GetIcon("image-bmp");
				}
				case WarcraftFileType.BinaryImage:
				case WarcraftFileType.TargaImage:
				{
					return GetIcon("image-x-generic");
				}
				case WarcraftFileType.PDF:
				{
					return GetIcon("application-pdf");
				}
				case WarcraftFileType.Web:
				{
					return GetIcon("text-html");
				}
				case WarcraftFileType.Assembly:
				{
					return GetIcon("application-x-executable");
				}
				case WarcraftFileType.Font:
				{
					return GetIcon("font-x-generic");
				}
				case WarcraftFileType.Animation:
				{
					return GetIcon("Blender-JumpingToon-Icon");
				}
				case WarcraftFileType.Physics:
				{
					return GetIcon("Blender-Deform-Icon");
				}
				case WarcraftFileType.Skeleton:
				{
					return GetIcon("Blender-Skeleton-Icon");
				}
				case WarcraftFileType.DataCache:
				{
					return GetIcon("text-x-sql");
				}
				case WarcraftFileType.INI:
				case WarcraftFileType.ConfigurationFile:
				case WarcraftFileType.Script:
				{
					return GetIcon("utilities-terminal");
				}
				case WarcraftFileType.Lighting:
				{
					return GetIcon("Blender-Sun-Icon");
				}
				case WarcraftFileType.Shader:
				{
					return GetIcon("Blender-Shader-Icon");
				}
				case WarcraftFileType.XML:
				{
					return GetIcon("text-xml");
				}
				case WarcraftFileType.Unknown:
				default:
				{
					return GetIcon(Stock.File);
				}
			}
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
		/// <returns>A pixel buffer containing the icon.</returns>
		private static Pixbuf LoadIconPixbuf(string iconName, int size = 16)
		{
			var key = (iconName, size);
			if (!IconCache.ContainsKey(key))
			{
				Pixbuf icon = IconTheme.Default.LoadIcon(iconName, size, IconLookupFlags.UseBuiltin);
				IconCache.Add(key, icon);
			}

			return IconCache[key];
		}
	}
}
