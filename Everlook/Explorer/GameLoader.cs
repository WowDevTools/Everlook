//
//  GameLoader.cs
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
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Everlook.Package;
using liblistfile;
using liblistfile.NodeTree;
using log4net;
using Warcraft.MPQ;

namespace Everlook.Explorer
{
	public class GameLoader
	{
		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(GameLoader));

		/// <summary>
		/// A dictionary used for generating node trees.
		/// </summary>
		private ListfileDictionary Dictionary;

		/// <summary>
		/// Loads the bundled dictionary from disk.
		/// </summary>
		/// <returns></returns>
		private static async Task<ListfileDictionary> LoadDictionary()
		{
			return await Task.Run(() => new ListfileDictionary(File.ReadAllBytes("Dictionary/dictionary.dic")));
		}

		///  <summary>
		///  Attempts to load a game in a specified path, returning a <see cref="PackageGroup"/> object with the
		///  packages in the path and an <see cref="OptimizedNodeTree"/> with a fully qualified node tree of the
		///  package group.
		///  If no packages are found, then this method will return null in both fields.
		///  </summary>
		/// <param name="gameAlias">The alias of the game at the path.</param>
		/// <param name="gamePath">The path to load as a game.</param>
		/// <param name="ct">A cancellation token.</param>
		/// <param name="progress">An <see cref="IProgress{GameLoadingProgress}"/> object for progress reporting.</param>
		/// <returns></returns>
		public async Task<(PackageGroup packageGroup, OptimizedNodeTree nodeTree)> LoadGameAsync(
			string gameAlias,
			string gamePath,
			CancellationToken ct = new CancellationToken(),
			IProgress<GameLoadingProgress> progress = null)
		{
			progress?.Report(new GameLoadingProgress
			{
				CompletionPercentage = 0.0f,
				State = GameLoadingState.SettingUp,
				Alias = gameAlias
			});

			List<string> packagePaths = Directory.EnumerateFiles(gamePath, "*",
					SearchOption.AllDirectories)
				.Where(p => p.EndsWith(".mpq", StringComparison.InvariantCultureIgnoreCase))
				.OrderBy(p => p)
				.ToList();

			if (packagePaths.Count == 0)
			{
				return (null, null);
			}

			string packageSetHash = GeneratePathSetHash(packagePaths);
			string packageTreeFilename = $".{packageSetHash}.tree";
			string packageTreeFilePath = Path.Combine(gamePath, packageTreeFilename);

			PackageGroup packageGroup = new PackageGroup(packageSetHash);
			OptimizedNodeTree nodeTree = null;

			bool generateTree = true;
			if (File.Exists(packageTreeFilePath))
			{
				progress?.Report(new GameLoadingProgress
				{
					CompletionPercentage = 0,
					State = GameLoadingState.LoadingNodeTree,
					Alias = gameAlias
				});

				try
				{
					// Load tree
					nodeTree = new OptimizedNodeTree(packageTreeFilePath);
					generateTree = false;
				}
				catch (ArgumentException aex)
				{
					// TODO: Implement separate exceptions
					if (aex.Message.Contains("Unseekable"))
					{
						throw;
					}

					if (aex.Message.Contains("Unsupported"))
					{
						Log.Info("Unsupported node tree version present. Deleting and regenerating.");
						File.Delete(packageTreeFilePath);
					}
				}
			}

			if (generateTree)
			{
				// Internal counters for progress reporting
				double completedSteps = 0;
				double totalSteps = packagePaths.Count * 2;

				// Load packages
				List<(string packageName, IPackage package)> packages = new List<(string packageName, IPackage package)>();
				foreach (string packagePath in packagePaths)
				{
					ct.ThrowIfCancellationRequested();

					progress?.Report(new GameLoadingProgress
					{
						CompletionPercentage = completedSteps / totalSteps,
						State = GameLoadingState.LoadingPackages,
						Alias = gameAlias
					});

					PackageInteractionHandler package = await PackageInteractionHandler.LoadAsync(packagePath);
					packages.Add((Path.GetFileNameWithoutExtension(packagePath), package));

					++completedSteps;
				}

				// Load dictionary if neccesary
				if (this.Dictionary == null)
				{
					progress?.Report(new GameLoadingProgress
					{
						CompletionPercentage = completedSteps / totalSteps,
						State = GameLoadingState.LoadingDictionary,
						Alias = gameAlias
					});

					this.Dictionary = await LoadDictionary();
				}

				// Generate node tree
				MultiPackageNodeTreeBuilder multiBuilder = new MultiPackageNodeTreeBuilder(this.Dictionary);
				foreach (var packageInfo in packages)
				{
					ct.ThrowIfCancellationRequested();

					progress?.Report(new GameLoadingProgress
					{
						CompletionPercentage = completedSteps / totalSteps,
						State = GameLoadingState.BuildingNodeTree,
						Alias = gameAlias
					});

					await multiBuilder.ConsumePackageAsync(packageInfo.packageName, packageInfo.package);
					packageGroup.AddPackage((PackageInteractionHandler)packageInfo.package);

					++completedSteps;
				}

				// Build node tree
				multiBuilder.Build();

				// Save it to disk
				File.WriteAllBytes(packageTreeFilePath, multiBuilder.CreateTree());

				nodeTree = new OptimizedNodeTree(packageTreeFilePath);
			}
			else
			{
				progress?.Report(new GameLoadingProgress
				{
					CompletionPercentage = 1,
					State = GameLoadingState.LoadingPackages,
					Alias = gameAlias
				});

				// Load packages
				packageGroup = await PackageGroup.LoadAsync(gameAlias, packageSetHash, gamePath, ct, progress);
			}


			progress?.Report(new GameLoadingProgress
			{
				CompletionPercentage = 1,
				State = GameLoadingState.Loading,
				Alias = gameAlias
			});

			return (packageGroup, nodeTree);
		}

		/// <summary>
		/// Generates a hash for a set of paths by combining their file names with their sizes. This is a quick
		/// and dirty way of discerning changes in sets of files without hashing any of their contents. Useful
		/// for large files like archives.
		/// </summary>
		/// <param name="packagePaths"></param>
		/// <returns></returns>
		private static string GeneratePathSetHash(IEnumerable<string> packagePaths)
		{
			StringBuilder sb = new StringBuilder();
			foreach (string path in packagePaths)
			{
				string packageName = Path.GetFileNameWithoutExtension(path);
				string packageSize = new FileInfo(path).Length.ToString();

				sb.Append(packageName);
				sb.Append(packageSize);
			}

			using (MD5 md5 = MD5.Create())
			{
				byte[] input = Encoding.UTF8.GetBytes(sb.ToString());
				byte[] hash = md5.ComputeHash(input);

				return BitConverter.ToString(hash);
			}
		}
	}
}