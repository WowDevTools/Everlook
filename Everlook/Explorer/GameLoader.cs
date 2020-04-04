//
//  GameLoader.cs
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
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Everlook.Package;
using FileTree.ProgressReporters;
using FileTree.Tree;
using FileTree.Tree.Serialized;
using ListFile;
using log4net;
using Warcraft.MPQ;

namespace Everlook.Explorer
{
    /// <summary>
    /// Handler class for loading games from disk. This class will load sets of packages and their node trees, and
    /// generate new trees if none are found.
    /// </summary>
    public class GameLoader
    {
        /// <summary>
        /// Logger instance for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameLoader));

        /// <summary>
        /// A dictionary used for generating node trees.
        /// </summary>
        private ListfileDictionary _dictionary;

        /// <summary>
        /// Loads the bundled dictionary from disk.
        /// </summary>
        /// <param name="ct">A cancellation topen.</param>
        /// <returns>A loaded ListfileDictionary.</returns>
        private static Task<ListfileDictionary> LoadDictionaryAsync(CancellationToken ct)
        {
            return Task.Run
            (
                () =>
                {
                    var dict = new ListfileDictionary();
                    dict.LoadFromStream(File.OpenRead("Dictionary/dictionary.dic"), ct);

                    return dict;
                },
                ct
            );
        }

        /// <summary>
        /// Attempts to load a game in a specified path, returning a <see cref="PackageGroup"/> object with the
        /// packages in the path and an <see cref="SerializedTree"/> with a fully qualified node tree of the
        /// package group.
        /// If no packages are found, then this method will return null in both fields.
        /// </summary>
        /// <param name="gameAlias">The alias of the game at the path.</param>
        /// <param name="gamePath">The path to load as a game.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <param name="progress">An <see cref="IProgress{GameLoadingProgress}"/> object for progress reporting.</param>
        /// <returns>A tuple with a package group and a node tree for the requested game.</returns>
        public async Task<(PackageGroup packageGroup, SerializedTree nodeTree)> LoadGameAsync
        (
            string gameAlias,
            string gamePath,
            CancellationToken ct,
            IProgress<GameLoadingProgress> progress = null
        )
        {
            progress?.Report(new GameLoadingProgress
            {
                CompletionPercentage = 0.0f,
                State = GameLoadingState.SettingUp,
                Alias = gameAlias
            });

            List<string> packagePaths = Directory.EnumerateFiles
            (
                gamePath,
                "*",
                SearchOption.AllDirectories
            )
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
            SerializedTree nodeTree = null;

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
                    nodeTree = new SerializedTree(File.OpenRead(packageTreeFilePath));
                    generateTree = false;
                }
                catch (FileNotFoundException)
                {
                    Log.Error("No file for the node tree found at the given location.");
                }
                catch (NotSupportedException)
                {
                    Log.Info("Unsupported node tree version present. Deleting and regenerating.");
                    File.Delete(packageTreeFilePath);
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

                    try
                    {
                        PackageInteractionHandler package = await PackageInteractionHandler.LoadAsync(packagePath);
                        packages.Add((Path.GetFileNameWithoutExtension(packagePath), package));
                    }
                    catch (FileLoadException fex)
                    {
                        Log.Warn($"Failed to load archive {Path.GetFileNameWithoutExtension(packagePath)}: {fex.Message}");
                    }

                    ++completedSteps;
                }

                // Load dictionary if neccesary
                if (this._dictionary == null)
                {
                    progress?.Report(new GameLoadingProgress
                    {
                        CompletionPercentage = completedSteps / totalSteps,
                        State = GameLoadingState.LoadingDictionary,
                        Alias = gameAlias
                    });

                    this._dictionary = await LoadDictionaryAsync(ct);
                }

                // Generate node tree
                TreeBuilder builder = new TreeBuilder();
                foreach (var packageInfo in packages)
                {
                    ct.ThrowIfCancellationRequested();

                    progress?.Report(new GameLoadingProgress
                    {
                        CompletionPercentage = completedSteps / totalSteps,
                        State = GameLoadingState.BuildingNodeTree,
                        Alias = gameAlias
                    });

                    double steps = completedSteps;
                    var createNodesProgress = new Progress<PackageNodesCreationProgress>
                    (
                        p =>
                        {
                            progress?.Report
                            (
                                new GameLoadingProgress
                                {
                                    CompletionPercentage = steps / totalSteps,
                                    State = GameLoadingState.BuildingNodeTree,
                                    Alias = gameAlias,
                                    CurrentPackage = packageInfo.packageName,
                                    NodesCreationProgress = p
                                }
                            );
                        }
                    );

                    await Task.Run(() => builder.AddPackage(packageInfo.packageName, packageInfo.package, createNodesProgress, ct), ct);
                    packageGroup.AddPackage((PackageInteractionHandler)packageInfo.package);

                    ++completedSteps;
                }

                // Build node tree
                var tree = builder.GetTree();

                var optimizeTreeProgress = new Progress<TreeOptimizationProgress>
                (
                    p =>
                    {
                        progress?.Report
                        (
                            new GameLoadingProgress
                            {
                                CompletionPercentage = completedSteps / totalSteps,
                                State = GameLoadingState.BuildingNodeTree,
                                Alias = gameAlias,
                                OptimizationProgress = p
                            }
                        );
                    }
                );

                var optimizer = new TreeOptimizer(this._dictionary);

                var treeClosureCopy = tree;
                tree = await Task.Run(() => optimizer.OptimizeTree(treeClosureCopy, optimizeTreeProgress, ct), ct);

                using (var fs = File.OpenWrite(packageTreeFilePath))
                {
                    using (var serializer = new TreeSerializer(fs))
                    {
                        await serializer.SerializeAsync(tree, ct);
                    }
                }

                nodeTree = new SerializedTree(File.OpenRead(packageTreeFilePath));
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
        /// <param name="packagePaths">The set of paths to hash together.</param>
        /// <returns>The hash created from the path set.</returns>
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
