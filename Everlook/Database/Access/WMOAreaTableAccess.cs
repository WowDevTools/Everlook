//
//  WMOAreaTableAccess.cs
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
using System.Linq;
using Warcraft.DBC;
using Warcraft.DBC.Definitions;
using Warcraft.DBC.SpecialFields;

namespace Everlook.Database.Access
{
	/// <summary>
	/// Static helper methods for accessing the WMO Area Table.
	/// </summary>
	public static class WMOAreaTableAccess
	{
		/// <summary>
		/// Gets the <see cref="WMOAreaTableRecord"/> for the given WMO group ID.
		/// </summary>
		/// <param name="database">The database to search.</param>
		/// <param name="groupID">The WMO group ID key.</param>
		/// <returns>A WMOAreaTableRecord corresponding to the given group ID.</returns>
		/// <exception cref="ArgumentException">
		/// Thrown if the given <see cref="ForeignKey{T}"/>is not a key for the WMO group ID field.
		/// </exception>
		public static WMOAreaTableRecord GetWMOGroupArea(this DBC<WMOAreaTableRecord> database, ForeignKey<uint> groupID)
		{
			if (groupID.Field != nameof(WMOAreaTableRecord.WMOGroupID))
			{
				throw new ArgumentException("The given foreign key is not valid for searching by WMO group ID.");
			}

			return database.FirstOrDefault(x => x.WMOGroupID == groupID.Key);
		}

		/// <summary>
		/// Gets the <see cref="WMOAreaTableRecord"/> for the given WMO ID.
		/// </summary>
		/// <param name="database">The database to search.</param>
		/// <param name="wmoID">The WMO ID key.</param>
		/// <returns>A WMOAreaTableRecord corresponding to the given ID.</returns>
		/// <exception cref="ArgumentException">
		/// Thrown if the given <see cref="ForeignKey{T}"/>is not a key for the WMO ID field.
		/// </exception>
		public static WMOAreaTableRecord GetWMOArea(this DBC<WMOAreaTableRecord> database, ForeignKey<uint> wmoID)
		{
			if (wmoID.Field != nameof(WMOAreaTableRecord.WMOID))
			{
				throw new ArgumentException("The given foreign key is not valid for searching by WMO ID.");
			}

			return database.FirstOrDefault(x => x.WMOID == wmoID.Key);
		}
	}
}
