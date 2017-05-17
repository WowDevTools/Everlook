//
//  GameLoadingState.cs
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

namespace Everlook.Explorer
{
	/// <summary>
	/// The current state a game loading progress is in.
	/// </summary>
	public enum GameLoadingState
	{
		/// <summary>
		/// Still setting up. Nothing has been loaded yet.
		/// </summary>
		SettingUp,

		/// <summary>
		/// Loading in general.
		/// </summary>
		Loading,

		/// <summary>
		/// Loading package references into memory.
		/// </summary>
		LoadingPackages,

		/// <summary>
		/// Loading a node tree into memory.
		/// </summary>
		LoadingNodeTree,

		/// <summary>
		/// Loading a dictionary into memory.
		/// </summary>
		LoadingDictionary,

		/// <summary>
		/// Building a new node tree.
		/// </summary>
		BuildingNodeTree
	}
}