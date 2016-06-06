//
//  FilterType.cs
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

namespace Everlook.Explorer
{
	/// <summary>
	/// Defines the different types of filter categories the game explorer can filter on.
	/// </summary>
	public enum FilterType
	{
		/// <summary>
		/// Shows all files, regardless of extension.
		/// </summary>
		All = 0,

		/// <summary>
		/// Shows models and model-related files.
		/// </summary>
		Models = 1,

		/// <summary>
		/// Shows textures and images.
		/// </summary>
		Textures = 2,

		/// <summary>
		/// Shows audio files, such as music and sound effects.
		/// </summary>
		Audio = 3,

		/// <summary>
		/// Shows data files, such as configuration files and clientside databases.
		/// </summary>
		Data = 4,

		/// <summary>
		/// Shows terrain and terrain-related files.
		/// </summary>
		Terrain = 5
	}

	/// <summary>
	/// Static container and accessor class for sets of filetype extensions, sorted by category.
	/// </summary>
	public static class FilterTypeExtensions
	{
		/// <summary>
		/// Gets the type of the extension list for the provided filter category.
		/// </summary>
		/// <returns>The extension list for the provided filter category.</returns>
		/// <param name="filterCategory">The category to get.</param>
		public static List<string> GetExtensionList(this FilterType filterCategory)
		{
			switch (filterCategory)
			{
				case FilterType.Models:
					{
						return ModelTypeExtensions;
					}
				case FilterType.Textures:
					{
						return TextureTypeExtensions;
					}
				case FilterType.Audio:
					{
						return AudioTypeExtensions;
					}
				case FilterType.Data:
					{
						return DataTypeExtensions;
					}
				case FilterType.Terrain:
					{
						return TerrainTypeExtensions;
					}
				default:
					{
						return null;
					}
			}
		}

		/// <summary>
		/// Determines whether or not the provided extension is present in any of the categories registered in this class.
		/// </summary>
		/// <returns><c>true</c> if the extension is unknown; otherwise, <c>false</c>.</returns>
		/// <param name="extension">Extension.</param>
		public static bool IsExtensionUnknown(string extension)
		{
			return ModelTypeExtensions.Contains(extension)
			|| TextureTypeExtensions.Contains(extension)
			|| AudioTypeExtensions.Contains(extension)
			|| DataTypeExtensions.Contains(extension)
			|| TerrainTypeExtensions.Contains(extension);
		}

		/// <summary>
		/// File extensions for model-related file types.
		/// </summary>
		public static readonly List<string> ModelTypeExtensions = new List<string>
		{
			"m2",
			"wmo",
			"mdx",
			"anim",
			"phys",
			"bone",

			// Shaders are included in this set, as they are directly tied to models.
			"bls",
			"wfx"
		};

		/// <summary>
		/// File extensions for texture and image file types.
		/// </summary>
		public static readonly List<string> TextureTypeExtensions = new List<string>
		{
			"blp",
			"tga",
			"gif",
			"png",
			"bmp",

			// Shaders are included in this set, as they are directly tied to textures.
			"bls",
			"wfx",

			// TRS hashmaps are included in this set, as they are directly tied to minimap textures.
			"trs"
		};

		/// <summary>
		/// File extensions for audio filetypes.
		/// </summary>
		public static readonly List<string> AudioTypeExtensions = new List<string>
		{
			"mp3",
			"wma",
			"wav",
			"ogg"
		};

		/// <summary>
		/// File extensions for data storage filetypes.
		/// </summary>
		public static readonly List<string> DataTypeExtensions = new List<string>
		{
			"wdb",
			"adb",
			"tbl",
			"dbc",
			"dbc2",
			"db",
			"wtf",
			"toc",
			"zmp",
			"sbt",
			"trs",

			// This extension list comes with a bunch of normal-ish filetypes that are packed into
			// some archives as a backup for the client and launcher.
			"lua",
			"html",
			"pdf",
			"exe",
			"dll",
			"xml",
			"js",
			"css",
			"plist",
			"icns",
			"xib",
			"nib",
			"ini"
		};

		/// <summary>
		/// File extensions for terrain-related file types.
		/// </summary>
		public static readonly List<string> TerrainTypeExtensions = new List<string>
		{
			"adt",
			"wdt",
			"wdl",
			"wlm",
			"wlq",
			"wlw",
			"lit",
			"def",
		};
	}
}

