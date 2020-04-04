//
//  FilterType.cs
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
}
