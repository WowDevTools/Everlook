//
//  CommunicationDelegates.cs
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

using Everlook.Explorer;

namespace Everlook.Utility
{
	/// <summary>
	/// Some delegate signatures used internally for events.
	/// </summary>
	public static class CommunicationDelegates
	{
		/// <summary>
		/// A requested file action.
		/// </summary>
		/// <param name="page">The <see cref="GamePage"/> that the action originated from.</param>
		/// <param name="reference">The reference that the action is requested to be performed on.</param>
		public delegate void FileActionDelegate(GamePage page, FileReference reference);
	}
}
