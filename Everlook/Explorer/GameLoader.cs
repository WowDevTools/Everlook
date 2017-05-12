using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Everlook.Package;
using liblistfile;
using liblistfile.NodeTree;
using Warcraft.MPQ;

namespace Everlook.Explorer
{
	public class GameLoader
	{
		private readonly ListfileDictionary Dictionary;

		public GameLoader()
		{
			this.Dictionary = new ListfileDictionary(File.ReadAllBytes("Dictionary/dictionary.dic"));
		}

		public (PackageGroup packageGroup, OptimizedNodeTree nodeTree) LoadGame(string gamePath)
		{
			List<string> packagePaths = Directory.EnumerateFiles(gamePath, "*",
					SearchOption.AllDirectories)
				.Where(p => p.EndsWith(".mpq", StringComparison.InvariantCultureIgnoreCase))
				.OrderBy(p => p)
				.ToList();

			string packageSetHash = GeneratePackageSetHash(packagePaths);
			string packageTreeFilename = $".{packageSetHash}.tree";
			string packageTreeFilePath = Path.Combine(gamePath, packageTreeFilename);

			PackageGroup packageGroup = new PackageGroup(packageSetHash);
			OptimizedNodeTree nodeTree;
			if (!File.Exists(packageTreeFilePath))
			{
				// Generate tree, TODO: callbacks to the UI reporting progress
				List<(string packageName, IPackage package)> packages = new List<(string packageName, IPackage package)>();
				foreach (string packagePath in packagePaths)
				{
					PackageInteractionHandler package = new PackageInteractionHandler(packagePath);
					packages.Add((Path.GetFileNameWithoutExtension(packagePath), package));
				}

				MultiPackageNodeTreeBuilder multiBuilder = new MultiPackageNodeTreeBuilder(this.Dictionary);
				foreach (var packageInfo in packages)
				{
					Console.WriteLine($"Consuming package: {packageInfo.packageName}");

					multiBuilder.ConsumePackage(packageInfo.packageName, packageInfo.package);
					packageGroup.AddPackage((PackageInteractionHandler)packageInfo.package);
				}

				multiBuilder.Build();
				nodeTree = multiBuilder.CreateTree();
			}
			else
			{
				// Load packages
				packageGroup = new PackageGroup(packageSetHash, gamePath);

				// Load tree
				nodeTree = new OptimizedNodeTree(File.OpenRead(packageTreeFilePath));
			}

			return (packageGroup, nodeTree);
		}

		private static string GeneratePackageSetHash(IEnumerable<string> packagePaths)
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