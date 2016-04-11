//
//  ExplorerBuilder.cs
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
using System.Threading;
using System.Collections.Generic;
using Gtk;
using Everlook.Configuration;
using System.IO;
using System.Linq;
using Warcraft.MPQ;
using System.Text.RegularExpressions;

namespace Everlook.Explorer
{
	/// <summary>
	/// The Explorer Builder class acts as a background worker for the file explorer, enumerating file nodes as requested.
	/// </summary>
	public class ExplorerBuilder
	{
		/// <summary>
		/// Occurs when a top-level package has been enumerated. This event does not mean that all files in the 
		/// package have been enumerated, only that the package has been registered by the builder.
		/// </summary>
		public event PackageEnumeratedEventHandler PackageEnumerated;

		/// <summary>
		/// Occurs when a directory has been enumerated.
		/// </summary>
		public event DirectoryEnumeratedEventHandler DirectoryEnumerated;

		/// <summary>
		/// Occurs when a file has been enumerated.
		/// </summary>
		public event FileEnumeratedEventHandler FileEnumerated;

		private ItemEnumeratedEventArgs PackageEnumeratedArgs;
		private ItemEnumeratedEventArgs DirectoryEnumeratedArgs;
		private ItemEnumeratedEventArgs FileEnumeratedArgs;

		/// <summary>
		/// The cached package directory. Used when the user changes game directory during runtime.
		/// </summary>
		private string CachedPackageDirectory;

		/// <summary>
		/// The package path mapping. Maps the package names to their paths on disk.
		/// </summary>
		public readonly Dictionary<string, string> PackagePathMapping = new Dictionary<string, string>();

		/// <summary>
		/// The package listfiles. 
		/// Key: The package name.
		/// Value: A list of all files present in the package.
		/// </summary>
		public readonly Dictionary<string, List<string>> PackageListfiles = new Dictionary<string, List<string>>();

		/// <summary>
		/// The package folder dictionary. Holds values in the following configuration:
		///	Key: Package Path.
		/// Value: Dictionary of folders and files in the package.
		/// Value.Key: Parent director Name.
		/// Value.Value: List of subfolders and files.
		/// </summary>
		public readonly Dictionary<ItemReference, List<ItemReference>> PackageSubfolderContent = new Dictionary<ItemReference, List<ItemReference>>();

		/// <summary>
		/// The package folder node mapping. Maps package names and paths to tree nodes.
		/// Key: Path of package, folder or file.
		/// Value: TreeIter that maps to the package, folder or file.
		/// </summary>
		public readonly Dictionary<ItemReference, TreeIter> PackageFolderNodeMapping = new Dictionary<ItemReference,TreeIter>();

		/// <summary>
		/// Static reference to the configuration handler.
		/// </summary>
		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

		private readonly List<ItemReference> WorkQueue = new List<ItemReference>();

		private readonly Thread EnumerationThread;
		private bool bShouldProcessWork = false;
		private bool bArePackageListsLoaded = false;
		private bool bIsReloading = false;


		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Explorer.ExplorerBuilder"/> class.
		/// </summary>
		public ExplorerBuilder()
		{
			this.EnumerationThread = new Thread(EnumerationLoop);
			Reload();
		}

		/// <summary>
		/// Gets a value indicating whether this instance is actively rendering frames for the viewport.
		/// </summary>
		/// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
		public bool IsActive
		{
			get
			{
				return bShouldProcessWork;
			}
		}

		/// <summary>
		/// Starts the enumeration thread in the background.
		/// </summary>
		public void Start()
		{
			if (!EnumerationThread.IsAlive)
			{
				this.bShouldProcessWork = true;
				this.EnumerationThread.Start();
			}
			else
			{
				throw new ThreadStateException("The enumeration thread has already been started.");
			}
		}

		/// <summary>
		/// Stops the enumeration thread, allowing it to finish the current work order.
		/// </summary>
		public void Stop()
		{
			if (EnumerationThread.IsAlive)
			{
				this.bShouldProcessWork = false;
			}
			else
			{
				throw new ThreadStateException("The enumeration thread has not been started.");
			}
		}

		/// <summary>
		/// Reloads the explorer builder, resetting all list files and known content.
		/// </summary>
		public void Reload()
		{
			if (!bIsReloading)
			{
				bIsReloading = true;
				bArePackageListsLoaded = false;
				Thread t = new Thread(Reload_Implementation);

				t.Start();
			}
		}

		/// <summary>
		/// Loads all packages in the currently selected game directory. This function does not enumerate files
		/// and directories deeper than one to keep the UI responsive.
		/// </summary>
		protected void Reload_Implementation()
		{
			if (CachedPackageDirectory != Config.GetGameDirectory() && Directory.Exists(Config.GetGameDirectory()))
			{
				
				// Grab all packages in the game directory
				List<string> PackagePaths = new List<string>();
				foreach (string file in Directory.EnumerateFiles(Config.GetGameDirectory(), "*.*", SearchOption.AllDirectories)
				.Where(s => s.EndsWith(".mpq") || s.EndsWith(".MPQ")))
				{
					PackagePaths.Add(file);
				}

				if (PackagePaths.Count > 0)
				{
					CachedPackageDirectory = Config.GetGameDirectory();
					PackagePathMapping.Clear();
					PackageListfiles.Clear();
					PackageSubfolderContent.Clear();
					PackageFolderNodeMapping.Clear();

					// Load the list files from all packages
					foreach (string PackagePath in PackagePaths)
					{
						using (FileStream fs = File.OpenRead(PackagePath))
						{
							string PackageName = System.IO.Path.GetFileName(PackagePath);

							try
							{
								using (MPQ Package = new MPQ(fs))
								{
									PackageListfiles.Add(PackageName, Package.GetFileList());
									PackagePathMapping.Add(PackageName, PackagePath);
								}
							}
							catch (FileLoadException ex)
							{						
								Console.WriteLine(String.Format("Exception in ExplorerBuilder.LoadPackages() (Package: {0}): {1}", PackageName, ex.Message));
								break;
							}
						}
					}

					// Add top level package nodes to the game explorer view
					foreach (KeyValuePair<string, List<string>> PackageListfile in PackageListfiles)
					{
						if (PackageListfile.Value != null)
						{
							string PackageName = System.IO.Path.GetFileName(PackageListfile.Key);

							ItemReference packageReference = new ItemReference(PackageName, "");
							PackageEnumeratedArgs = new ItemEnumeratedEventArgs(packageReference);

							RaisePackageEnumerated();
							SubmitWork(packageReference);
						}
					}
				}
			}

			bIsReloading = false;
			bArePackageListsLoaded = true;
		}

		/// <summary>
		/// Main loop of this worker class. Enumerates any work placed into the work queue.
		/// </summary>
		private void EnumerationLoop()
		{
			while (bShouldProcessWork)
			{
				if (bArePackageListsLoaded && WorkQueue.Count > 0)
				{
					// Grab the first item in the queue.
					ItemReference targetReference = WorkQueue.First();
					EnumerateFilesAndFolders(targetReference);

					WorkQueue.Remove(targetReference);
				}
			}
		}

		/// <summary>
		/// Submits work to the explorer builder. The work submitted is processed in a 
		/// first-in, first-out order as work orders may depend on each other.
		/// </summary>
		/// <param name="reference">Reference.</param>
		public void SubmitWork(ItemReference reference)
		{
			if (!WorkQueue.Contains(reference))
			{
				WorkQueue.Add(reference);
			}
		}

		// TODO: Rework
		/// <summary>
		/// Enumerates the files and subfolders in the specified package, starting at 
		/// the provided root path.
		/// </summary>
		/// <param name="parentReference">Parent reference where the search should start.</param>
		protected void EnumerateFilesAndFolders(ItemReference parentReference)
		{
			List<string> PackageListfile;
			if (PackageListfiles.TryGetValue(parentReference.PackageName, out PackageListfile))
			{
				bool bHasFoundStartOfFolderBlock = false;

				foreach (string FilePath in PackageListfile)
				{
					if (FilePath.StartsWith(parentReference.ItemPath))
					{
						bHasFoundStartOfFolderBlock = true;

						string childPath = Regex.Replace(FilePath, "^" + Regex.Escape(parentReference.ItemPath), "");

						int slashIndex = childPath.IndexOf('\\');
						string topDirectory = childPath.Substring(0, slashIndex + 1);

						if (!String.IsNullOrEmpty(topDirectory))
						{
							ItemReference itemReference = new ItemReference(parentReference, topDirectory);

							DirectoryEnumeratedArgs = new ItemEnumeratedEventArgs(itemReference);
							RaiseDirectoryEnumerated();
						}
						else if (String.IsNullOrEmpty(topDirectory) && slashIndex == -1)
						{									
							ItemReference itemReference = new ItemReference(parentReference, childPath);

							FileEnumeratedArgs = new ItemEnumeratedEventArgs(itemReference);
							RaiseFileEnumerated();
						}
					}
					else if (bHasFoundStartOfFolderBlock)
					{
						break;
					}
				}
			}		
		}

		/// <summary>
		/// Raises the package enumerated event.
		/// </summary>
		protected void RaisePackageEnumerated()
		{
			if (PackageEnumerated != null)
			{
				PackageEnumerated(this, PackageEnumeratedArgs);
			}
		}

		/// <summary>
		/// Raises the directory enumerated event.
		/// </summary>
		protected void RaiseDirectoryEnumerated()
		{
			if (DirectoryEnumerated != null)
			{
				DirectoryEnumerated(this, DirectoryEnumeratedArgs);
			}
		}

		/// <summary>
		/// Raises the file enumerated event.
		/// </summary>
		protected void RaiseFileEnumerated()
		{
			if (FileEnumerated != null)
			{
				FileEnumerated(this, FileEnumeratedArgs);
			}
		}
	}

	/// <summary>
	/// Package enumerated event handler.
	/// </summary>
	public delegate void PackageEnumeratedEventHandler(object sender,ItemEnumeratedEventArgs e);

	/// <summary>
	/// Directory enumerated event handler.
	/// </summary>
	public delegate void DirectoryEnumeratedEventHandler(object sender,ItemEnumeratedEventArgs e);

	/// <summary>
	/// File enumerated event handler.
	/// </summary>
	public delegate void FileEnumeratedEventHandler(object sender,ItemEnumeratedEventArgs e);

	/// <summary>
	/// Item enumerated event arguments.
	/// </summary>
	public class ItemEnumeratedEventArgs : EventArgs
	{
		/// <summary>
		/// Contains the enumerated item reference.
		/// </summary>
		/// <value>The item.</value>
		public ItemReference Item
		{
			get;
			private set;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Explorer.ItemEnumeratedEventArgs"/> class.
		/// </summary>
		/// <param name="InItem">In item.</param>
		public ItemEnumeratedEventArgs(ItemReference InItem)
		{
			this.Item = InItem;
		}
	}
}

