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

		public Tuple<PackageGroup, OptimizedNodeTree> LoadGame(string gamePath)
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
				List<Tuple<string, IPackage>> packages = new List<Tuple<string, IPackage>>();
				foreach (string packagePath in packagePaths)
				{
					PackageInteractionHandler package = new PackageInteractionHandler(packagePath);
					packages.Add(new Tuple<string, IPackage>(Path.GetFileNameWithoutExtension(packagePath), package));
				}

				MultiPackageNodeTreeBuilder multiBuilder = new MultiPackageNodeTreeBuilder(this.Dictionary);
				foreach (var packageInfo in packages)
				{
					Console.WriteLine($"Consuming package: {packageInfo.Item1}");

					multiBuilder.ConsumePackage(packageInfo.Item1, packageInfo.Item2);
					packageGroup.AddPackage((PackageInteractionHandler)packageInfo.Item2);
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

			return new Tuple<PackageGroup, OptimizedNodeTree>(packageGroup, nodeTree);
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