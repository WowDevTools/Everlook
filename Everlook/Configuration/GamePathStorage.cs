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
using System.IO;
using System.Collections.Generic;
using Warcraft.Core;

namespace Everlook.Configuration
{
	/// <summary>
	/// Game path storage. This class handles storing a set of game paths in a binary file.
	/// A binary file format is used for maximal cross-platform compatibility - a null character
	/// is, unfortunately, the only safe separator for multiple paths in current and future systems.
	/// Yes, Linux allows tabs, newlines and carriage returns in paths.
	/// </summary>
	public sealed class GamePathStorage
	{
		private object StorageLock = new object();

		/// <summary>
		/// A static instance of the path storage class.
		/// </summary>
		public static GamePathStorage Instance = new GamePathStorage();

		/// <summary>
		/// Gets the stored game paths.
		/// </summary>
		/// <value>The game paths.</value>
		public List<string> GamePaths
		{
			get
			{
				return ReadStoredPaths();
			}
		}

		private GamePathStorage()
		{
			if (!File.Exists(GetPathStoragePath()))
			{
				string storageDirectory = Directory.GetParent (GetPathStoragePath ()).FullName;
				if (!Directory.Exists(storageDirectory))
				{
					Directory.CreateDirectory (storageDirectory);
				}

				File.Create(GetPathStoragePath()).Close();
			}
		}

		/// <summary>
		/// Stores a provided path in the path storage.
		/// </summary>
		/// <param name="pathToStore">Path to store.</param>
		public void StorePath(string pathToStore)
		{
			if (!GamePaths.Contains(pathToStore))
			{
				lock (StorageLock)
				{				
					using (FileStream fs = File.Open(GetPathStoragePath(), FileMode.Append))
					{
						using (BinaryWriter bw = new BinaryWriter(fs))
						{
							bw.WriteNullTerminatedString(pathToStore);
							bw.Flush();
						}
					}
				}
			}
		}

		/// <summary>
		/// Removes a path that's been stored.
		/// </summary>
		/// <param name="pathToRemove">Path to remove.</param>
		public void RemoveStoredPath(string pathToRemove)
		{
			List<string> storedPaths = GamePaths;
			if (storedPaths.Contains(pathToRemove))
			{
				ClearPaths();
				lock (StorageLock)
				{
					storedPaths.Remove(pathToRemove);

					using (FileStream fs = File.OpenWrite(GetPathStoragePath()))
					{
						using (BinaryWriter bw = new BinaryWriter(fs))
						{
							foreach (string pathToStoreAgain in storedPaths)
							{
								bw.WriteNullTerminatedString(pathToStoreAgain);
								bw.Flush();
							}
						}
					}
				}
			}
		}

		private List<string> ReadStoredPaths()
		{
			List<string> storedPaths = new List<string>();
			lock (StorageLock)
			{
				using (FileStream fs = File.OpenRead(GetPathStoragePath()))
				{
					using (BinaryReader br = new BinaryReader(fs))
					{
						while (br.BaseStream.Position != br.BaseStream.Length)
						{
							storedPaths.Add(br.ReadNullTerminatedString());
						}
					}
				}
			}

			return storedPaths;
		}

		private void ClearPaths()
		{
			lock (StorageLock)
			{
				File.Delete(GetPathStoragePath());
				File.Create(GetPathStoragePath()).Close();
			}
		}

		/// <summary>
		/// Gets the path to the game path storage.
		/// </summary>
		/// <returns>The path storage path.</returns>
		private static string GetPathStoragePath()
		{
			return String.Format("{0}{1}Everlook{1}gamepaths.store", 
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				Path.DirectorySeparatorChar);
		}
	}
}

