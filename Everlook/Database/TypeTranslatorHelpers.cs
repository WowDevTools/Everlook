//
//  TypeTranslatorHelpers.cs
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
using Warcraft.DBC;

namespace Everlook.Database
{
	/// <summary>
	/// Helper methods for translating types to database names and back.
	/// </summary>
	public static class TypeTranslatorHelpers
	{
		/// <summary>
		/// Converts a database name into a qualified type.
		/// </summary>
		/// <param name="databaseName">The enumerated name of the database,</param>
		/// <returns>The type mapping to the database name.</returns>
		public static Type GetRecordTypeFromDatabaseName(DatabaseName databaseName)
		{
			return Type.GetType($"Warcraft.DBC.Definitions.{databaseName}Record, libwarcraft");
		}

		/// <summary>
		/// Gets the database name from a type.
		/// </summary>
		/// <param name="recordType">The type of the record.</param>
		/// <returns>The enumerated database name.</returns>
		/// <exception cref="ArgumentException">Thrown if the given type can't be resolved to a database name.</exception>
		public static DatabaseName GetDatabaseNameFromRecordType(Type recordType)
		{
			string recordName = recordType.Name.Replace("Record", string.Empty);
			if (Enum.TryParse(recordName, true, out DatabaseName databaseName))
			{
				return databaseName;
			}

			throw new ArgumentException("The given type could not be resolved to a database name.", nameof(recordType));
		}
	}
}
