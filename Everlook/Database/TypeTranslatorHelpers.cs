using System;
using Warcraft.DBC;
using Warcraft.DBC.Definitions;

namespace Everlook.Database
{
	public static class TypeTranslatorHelpers
	{
		/// <summary>
		/// Converts a database name into a qualified type.
		/// </summary>
		/// <param name="databaseName"></param>
		/// <returns></returns>
		public static Type GetRecordTypeFromDatabaseName(DatabaseName databaseName)
		{
			return Type.GetType($"Warcraft.DBC.Definitions.{databaseName}Record, libwarcraft");
		}

		/// <summary>
		/// Gets the database name from a type.
		/// </summary>
		/// <param name="recordType"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static DatabaseName GetDatabaseNameFromRecordType(Type recordType)
		{
			string recordName = recordType.Name.Replace("Record", "");
			if (Enum.TryParse(recordName, true, out DatabaseName databaseName))
			{
				return databaseName;
			}

			throw new ArgumentException("The given type could not be resolved to a database name.", nameof(recordType));
		}
	}
}