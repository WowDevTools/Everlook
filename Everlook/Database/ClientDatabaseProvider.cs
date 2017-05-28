//
//  GamePathStorage.cs
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
using Warcraft.Core;
using Warcraft.DBC;
using Warcraft.DBC.Definitions;
using Warcraft.MPQ;

namespace Everlook.Database
{
	/// <summary>
	/// The <see cref="ClientDatabaseProvider"/> class acts as an intermediary between the game files and requests for
	/// client database records (DBC information). It can provide whole databases, or single records as requested.
	/// </summary>
	public class ClientDatabaseProvider
	{
		/// <summary>
		/// The <see cref="WarcraftVersion"/> that this database provider is valid for.
		/// </summary>
		private readonly WarcraftVersion Version;

		/// <summary>
		/// The package where the database data can be sourced from.
		/// </summary>
		private readonly IPackage ContentSource;

		/// <summary>
		/// The loaded databases.
		/// </summary>
		private readonly Dictionary<DatabaseName, IDBC> Databases = new Dictionary<DatabaseName, IDBC>();

		/// <summary>
		/// Initializes a new <see cref="ClientDatabaseProvider"/> for the given warcraft version and content source.
		/// </summary>
		/// <param name="version"></param>
		/// <param name="contentSource"></param>
		public ClientDatabaseProvider(WarcraftVersion version, IPackage contentSource)
		{
			this.Version = version;
			this.ContentSource = contentSource;
		}

		/// <summary>
		/// Gets the database which maps to the provided record type from the provider.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public DBC<T> GetDatabase<T>() where T : DBCRecord, new()
		{
			DatabaseName databaseName = GetDatabaseNameFromRecordType(typeof(T));

			if (!ContainsDatabase(databaseName))
			{
				LoadDatabase(databaseName);
			}

			return this.Databases[databaseName] as DBC<T>;
		}

		/// <summary>
		/// Gets a record of type <typeparamref name="T"/> from its corresponding database by its ID. The ID that the 
		/// function consumes is the zero-based index of the record in the database, however, the record IDs begin 
		/// counting from one.
		/// </summary>
		/// <param name="id"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public T GetRecord<T>(int id) where T : DBCRecord, new()
		{
			DBC<T> database = GetDatabase<T>();
			return database.GetRecordByID(id);
		}

		/// <summary>
		/// Determines whether or not the given database name has a loaded database associated with it.
		/// </summary>
		/// <param name="databaseName"></param>
		/// <returns></returns>
		public bool ContainsDatabase(DatabaseName databaseName)
		{
			return this.Databases.ContainsKey(databaseName);
		}

		/// <summary>
		/// Loads the database which corresponds to the given database name.
		/// </summary>
		/// <param name="databaseName"></param>
		private void LoadDatabase(DatabaseName databaseName)
		{
			if (this.Databases.ContainsKey(databaseName))
			{
				return;
			}

			Type genericDBCType = typeof(DBC<>);
			Type specificDBCType = genericDBCType.MakeGenericType(GetRecordTypeFromDatabaseName(databaseName));

			string databasePath = GetDatabasePackagePath(databaseName);
			byte[] databaseData = this.ContentSource.ExtractFile(databasePath);
			if (databaseData == null)
			{
				throw new FileLoadException($"Failed to load the database data for {databaseName}.", databasePath);
			}

			IDBC database = (IDBC)Activator.CreateInstance(specificDBCType, this.Version, databaseData);
			this.Databases.Add(databaseName, database);
		}

		/// <summary>
		/// Converts a database name into a qualified type.
		/// </summary>
		/// <param name="databaseName"></param>
		/// <returns></returns>
		private static Type GetRecordTypeFromDatabaseName(DatabaseName databaseName)
		{
			return Type.GetType($"Warcraft.DBC.Definitions.{databaseName}Record, libwarcraft");
		}

		/// <summary>
		/// Gets the database name from a type.
		/// </summary>
		/// <param name="recordType"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		private DatabaseName GetDatabaseNameFromRecordType(Type recordType)
		{
			string recordName = recordType.Name.Replace("Record", "");
			if (Enum.TryParse(recordName, true, out DatabaseName databaseName))
			{
				return databaseName;
			}

			throw new ArgumentException("The given type could not be resolved to a database name.", nameof(recordType));
		}

		/// <summary>
		/// Gets the path to the database file for a given database name.
		/// </summary>
		/// <param name="databaseName"></param>
		/// <returns></returns>
		private static string GetDatabasePackagePath(DatabaseName databaseName)
		{
			return $"DBFilesClient\\{databaseName}.dbc";
		}
	}
}