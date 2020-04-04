//
//  ClientDatabaseProvider.cs
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

using System;
using System.Collections.Generic;
using System.IO;
using Warcraft.Core;
using Warcraft.DBC;
using Warcraft.DBC.Definitions;
using Warcraft.MPQ;
using static Everlook.Database.TypeTranslatorHelpers;

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
        private readonly WarcraftVersion _version;

        /// <summary>
        /// The package where the database data can be sourced from.
        /// </summary>
        private readonly IPackage _contentSource;

        /// <summary>
        /// Locking object for threaded access to the databases.
        /// </summary>
        private readonly object _databaseLock = new object();

        /// <summary>
        /// The loaded databases.
        /// </summary>
        private readonly Dictionary<DatabaseName, IDBC> _databases = new Dictionary<DatabaseName, IDBC>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientDatabaseProvider"/> class.
        /// </summary>
        /// <param name="version">The game version to use when loading the databases.</param>
        /// <param name="contentSource">The <see cref="IPackage"/> where data can be retrieved.</param>
        public ClientDatabaseProvider(WarcraftVersion version, IPackage contentSource)
        {
            this._version = version;
            this._contentSource = contentSource;
        }

        /// <summary>
        /// Gets the database which maps to the provided record type from the provider.
        /// </summary>
        /// <typeparam name="T">The type of database to retrieve.</typeparam>
        /// <returns>A database of type <typeparamref name="T"/>.</returns>
        public DBC<T> GetDatabase<T>() where T : DBCRecord, new()
        {
            DatabaseName databaseName = GetDatabaseNameFromRecordType(typeof(T));

            lock (this._databaseLock)
            {
                if (!ContainsDatabase(databaseName))
                {
                    LoadDatabase(databaseName);
                }

                return this._databases[databaseName] as DBC<T>;
            }
        }

        /// <summary>
        /// Gets a record of type <typeparamref name="T"/> from its corresponding database by its ID. This is equivalent
        /// to a primary key lookup.
        /// </summary>
        /// <param name="id">The primary key ID.</param>
        /// <typeparam name="T">The type of record to retrieve.</typeparam>
        /// <returns>A record of type <typeparamref name="T"/>.</returns>
        public T GetRecordByID<T>(int id) where T : DBCRecord, new()
        {
            DBC<T> database = GetDatabase<T>();
            return database.GetRecordByID(id);
        }

        /// <summary>
        /// Gets a record of type <typeparamref name="T"/> from its corresponding database by its index. This is not a
        /// lookup by the primary key of the row, but rather a direct indexing into the data.
        /// </summary>
        /// <param name="index">The index of the row in the database.</param>
        /// <typeparam name="T">The type of record to retrieve.</typeparam>
        /// <returns>A record of type <typeparamref name="T"/>.</returns>
        public T GetRecordByIndex<T>(int index) where T : DBCRecord, new()
        {
            DBC<T> database = GetDatabase<T>();
            return database[index];
        }

        /// <summary>
        /// Determines whether or not the given database name has a loaded database associated with it.
        /// </summary>
        /// <param name="databaseName">The name of the database.</param>
        /// <returns>true if the provider contains the given database; false otherwise.</returns>
        public bool ContainsDatabase(DatabaseName databaseName)
        {
            lock (this._databaseLock)
            {
                return this._databases.ContainsKey(databaseName);
            }
        }

        /// <summary>
        /// Loads the database which corresponds to the given database name.
        /// </summary>
        /// <param name="databaseName">The name of the database.</param>
        private void LoadDatabase(DatabaseName databaseName)
        {
            if (this._databases.ContainsKey(databaseName))
            {
                return;
            }

            Type genericDBCType = typeof(DBC<>);
            Type specificDBCType = genericDBCType.MakeGenericType(GetRecordTypeFromDatabaseName(databaseName));

            string databasePath = GetDatabasePackagePath(databaseName);
            byte[] databaseData = this._contentSource.ExtractFile(databasePath);

            IDBC database = (IDBC)Activator.CreateInstance(specificDBCType, this._version, databaseData);
            this._databases.Add(databaseName, database);
        }

        /// <summary>
        /// Gets the path to the database file for a given database name.
        /// </summary>
        /// <param name="databaseName">The name of the database.</param>
        /// <returns>The file path of the database in the package.</returns>
        private static string GetDatabasePackagePath(DatabaseName databaseName)
        {
            return $"DBFilesClient\\{databaseName}.dbc";
        }
    }
}
