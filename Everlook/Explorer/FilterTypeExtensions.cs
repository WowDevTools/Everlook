//
//  FilterTypeExtensions.cs
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

using Warcraft.Core;

namespace Everlook.Explorer
{
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
		public static WarcraftFileType GetFileTypeSet(this FilterType filterCategory)
		{
			switch (filterCategory)
			{
				case FilterType.Models:
				{
					return ModelTypes;
				}
				case FilterType.Textures:
				{
					return TextureTypes;
				}
				case FilterType.Audio:
				{
					return AudioTypes;
				}
				case FilterType.Data:
				{
					return DataTypes;
				}
				case FilterType.Terrain:
				{
					return TerrainTypes;
				}
				default:
				{
					return WarcraftFileType.Unknown;
				}
			}
		}

		/// <summary>
		/// Reference extensions for model-related file types.
		/// </summary>
		public const WarcraftFileType ModelTypes =
			WarcraftFileType.GameObjectModel |
			WarcraftFileType.WorldObjectModel |
			WarcraftFileType.WorldObjectModelGroup |
			WarcraftFileType.Shader |
			WarcraftFileType.Animation |
			WarcraftFileType.Physics |
			WarcraftFileType.Skeleton;

		/// <summary>
		/// Reference extensions for texture and image file types.
		/// </summary>
		public const WarcraftFileType TextureTypes =
			WarcraftFileType.BinaryImage |
			WarcraftFileType.TargaImage |
			WarcraftFileType.GIFImage |
			WarcraftFileType.PNGImage |
			WarcraftFileType.BitmapImage |
			WarcraftFileType.IconImage |
			WarcraftFileType.Shader |
			WarcraftFileType.Hashmap;

		/// <summary>
		/// Reference extensions for audio filetypes.
		/// </summary>
		public const WarcraftFileType AudioTypes =
			WarcraftFileType.MP3Audio |
			WarcraftFileType.WMAAudio |
			WarcraftFileType.WaveAudio |
			WarcraftFileType.VorbisAudio;

		/// <summary>
		/// Reference extensions for data storage filetypes.
		/// </summary>
		public const WarcraftFileType DataTypes =
			WarcraftFileType.DatabaseContainer |
			WarcraftFileType.DataCache |
			WarcraftFileType.ConfigurationFile |
			WarcraftFileType.AddonManifest |
			WarcraftFileType.AddonManifestSignature |
			WarcraftFileType.Assembly |
			WarcraftFileType.Hashmap |
			WarcraftFileType.Web |
			WarcraftFileType.PDF |
			WarcraftFileType.INI |
			WarcraftFileType.XML |
			WarcraftFileType.Script;

		/// <summary>
		/// Reference extensions for terrain-related file types.
		/// </summary>
		public const WarcraftFileType TerrainTypes =
			WarcraftFileType.TerrainData |
			WarcraftFileType.TerrainLevel |
			WarcraftFileType.TerrainLiquid |
			WarcraftFileType.TerrainTable |
			WarcraftFileType.TerrainWater |
			WarcraftFileType.Lighting;
	}
}
