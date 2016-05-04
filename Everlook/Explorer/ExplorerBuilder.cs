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
using System.Text.RegularExpressions;
using Everlook.Package;

namespace Everlook.Explorer
{
	/// <summary>
	/// The Explorer Builder class acts as a background worker for the file explorer, enumerating file nodes as requested.
	/// </summary>
	public class ExplorerBuilder : IDisposable
	{
		/// <summary>
		/// Occurs when a package group has been added.
		/// </summary>
		public event ItemEnumeratedEventHandler PackageGroupAdded;
		
		/// <summary>
		/// Occurs when a top-level package has been enumerated. This event does not mean that all files in the 
		/// package have been enumerated, only that the package has been registered by the builder.
		/// </summary>
		public event ItemEnumeratedEventHandler PackageEnumerated;

		/// <summary>
		/// Occurs when a directory has been enumerated.
		/// </summary>
		public event ItemEnumeratedEventHandler DirectoryEnumerated;

		/// <summary>
		/// Occurs when a file has been enumerated.
		/// </summary>
		public event ItemEnumeratedEventHandler FileEnumerated;

		/// <summary>
		/// Occurs when a work order has been completed.
		/// </summary>
		public event ItemEnumeratedEventHandler EnumerationFinished;

		private ItemEnumeratedEventArgs PackageGroupAddedArgs;
		private ItemEnumeratedEventArgs PackageEnumeratedArgs;
		private ItemEnumeratedEventArgs DirectoryEnumeratedArgs;
		private ItemEnumeratedEventArgs FileEnumeratedArgs;
		private ItemEnumeratedEventArgs EnumerationFinishedArgs;

		/// <summary>
		/// The cached package directory. Used when the user changes game directory during runtime.
		/// </summary>
		private List<string> CachedPackageDirectories = new List<string>();

		/// <summary>
		/// The package groups. This is, at a glance, groupings of packages in a game directory
		/// that act as a cohesive unit. Usually, a single package group represents a single game
		/// instance.
		/// </summary>
		public readonly Dictionary<string, PackageGroup> PackageGroups = new Dictionary<string, PackageGroup>();

		/// <summary>
		/// The package node group mapping. Maps package groups to their base virtual item references.
		/// </summary>
		public readonly Dictionary<PackageGroup, VirtualItemReference> PackageGroupVirtualNodeMapping = new Dictionary<PackageGroup, VirtualItemReference>();

		/// <summary>
		/// Maps package names and paths to tree nodes.
		/// Key: Path of package, folder or file.
		/// Value: TreeIter that maps to the package, folder or file.
		/// </summary>
		public readonly Dictionary<ItemReference, TreeIter> PackageItemNodeMapping = new Dictionary<ItemReference, TreeIter>();

		/// <summary>
		/// Maps tree nodes to package names and paths.
		/// Key: TreeIter that represents the item reference.
		/// Value: ItemReference that the iter maps to.
		/// </summary>
		public readonly Dictionary<TreeIter, ItemReference> PackageNodeItemMapping = new Dictionary<TreeIter, ItemReference>();

		/// <summary>
		/// The virtual reference mapping. Maps item references to their virtual counterparts.
		/// Key: An item path in an arbitrary package.
		/// Value: A virtual item reference that hosts the hard item references.
		/// </summary>
		public readonly Dictionary<ItemReference, VirtualItemReference> VirtualReferenceMapping = new Dictionary<ItemReference, VirtualItemReference>();

		private readonly List<ItemReference> WorkQueue = new List<ItemReference>();

		private readonly Thread EnumerationLoopThread;

		private readonly List<Thread> ActiveEnumerationThreads = new List<Thread>();
		private readonly int MaxEnumerationThreadCount;

		private bool bShouldProcessWork;
		private bool bArePackageGroupsLoaded;
		private bool bIsReloading;


		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Explorer.ExplorerBuilder"/> class.
		/// </summary>
		public ExplorerBuilder()
		{
			this.MaxEnumerationThreadCount = Environment.ProcessorCount * 250;

			this.EnumerationLoopThread = new Thread(EnumerationLoop);
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
			if (!EnumerationLoopThread.IsAlive)
			{
				this.bShouldProcessWork = true;
				this.EnumerationLoopThread.Start();
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
			if (EnumerationLoopThread.IsAlive)
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
				bArePackageGroupsLoaded = false;
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
			if (HasPackageDirectoryChanged())
			{
				CachedPackageDirectories = GamePathStorage.Instance.GamePaths;
				this.PackageGroups.Clear();

				foreach (string packageDirectory in CachedPackageDirectories)
				{
					if (Directory.Exists(packageDirectory))
					{						
						string FolderName = Path.GetFileName(packageDirectory);
						this.PackageGroups.Add(FolderName, new PackageGroup(FolderName, packageDirectory));
					}
				}

				if (this.PackageGroups.Count > 0)
				{
					WorkQueue.Clear();
					PackageItemNodeMapping.Clear();
					PackageNodeItemMapping.Clear();

					foreach (KeyValuePair<string, PackageGroup> GroupEntry in PackageGroups)
					{
						VirtualItemReference packageGroupReference = new VirtualItemReference(GroupEntry.Value, new ItemReference(GroupEntry.Value));
						ItemReference packageGroupPackagesFolderReference = new ItemReference(GroupEntry.Value, packageGroupReference, "");

						packageGroupReference.ChildReferences.Add(packageGroupPackagesFolderReference);

						this.PackageGroupAddedArgs = new ItemEnumeratedEventArgs(packageGroupReference);
						RaisePackageGroupAdded();

						// Add top level package nodes to the game explorer view
						foreach (KeyValuePair<string, List<string>> PackageListfile in GroupEntry.Value.PackageListfiles)
						{
							if (PackageListfile.Value != null)
							{
								string PackageName = Path.GetFileName(PackageListfile.Key);

								ItemReference packageReference = new ItemReference(GroupEntry.Value, packageGroupPackagesFolderReference, PackageName, "");
								PackageEnumeratedArgs = new ItemEnumeratedEventArgs(packageReference);

								RaisePackageEnumerated();
								SubmitWork(packageReference);
							}
						}
					}
				}

				bIsReloading = false;
				bArePackageGroupsLoaded = true;
			}
		}

		/// <summary>
		/// Determines whether the package directory changed.
		/// </summary>
		/// <returns><c>true</c> if the package directory has changed; otherwise, <c>false</c>.</returns>
		public bool HasPackageDirectoryChanged()
		{
			return !Enumerable.SequenceEqual(CachedPackageDirectories.OrderBy(t => t), GamePathStorage.Instance.GamePaths.OrderBy(t => t));
		}

		/// <summary>
		/// Main loop of this worker class. Enumerates any work placed into the work queue.
		/// </summary>
		private void EnumerationLoop()
		{
			while (bShouldProcessWork)
			{				
				if (bArePackageGroupsLoaded && WorkQueue.Count > 0)
				{
					// Clear out finished threads
					List<Thread> FinishedThreads = new List<Thread>();
					foreach (Thread t in ActiveEnumerationThreads)
					{
						if (!t.IsAlive)
						{
							FinishedThreads.Add(t);
						}
					}

					foreach (Thread t in FinishedThreads)
					{
						ActiveEnumerationThreads.Remove(t);
					}

					if (ActiveEnumerationThreads.Count < this.MaxEnumerationThreadCount)
					{
						// Grab the first item in the queue.
						ItemReference targetReference = WorkQueue.First();
						Thread t = new Thread(new ParameterizedThreadStart(EnumerateFilesAndFolders));
						this.ActiveEnumerationThreads.Add(t);

						t.Start(targetReference);
						WorkQueue.Remove(targetReference);
					}
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

		/// <summary>
		/// Enumerates the files and subfolders in the specified package, starting at 
		/// the provided root path.
		/// </summary>
		/// <param name="parentReferenceObject">Parent reference where the search should start.</param>
		protected void EnumerateFilesAndFolders(object parentReferenceObject)
		{
			ItemReference parentReference = parentReferenceObject as ItemReference;
			if (parentReference != null)
			{
				VirtualItemReference virtualParentReference = parentReference as VirtualItemReference;
				if (virtualParentReference != null)
				{
					EnumerateHardReference(virtualParentReference.HardReference);

					foreach (ItemReference hardReference in virtualParentReference.OverriddenHardReferences)
					{
						EnumerateHardReference(hardReference);
					}
				}
				else
				{
					EnumerateHardReference(parentReference);
				}
			}
		}

		/// <summary>
		/// Enumerates a hard reference.
		/// </summary>
		/// <param name="hardReference">Hard reference.</param>
		protected void EnumerateHardReference(ItemReference hardReference)
		{
			List<string> PackageListfile;
			if (hardReference.Group.PackageListfiles.TryGetValue(hardReference.PackageName, out PackageListfile))
			{
				foreach (string FilePath in PackageListfile.Where(s => s.StartsWith(hardReference.ItemPath)))
				{
					string childPath = Regex.Replace(FilePath, "^" + Regex.Escape(hardReference.ItemPath), "");

					int slashIndex = childPath.IndexOf('\\');
					string topDirectory = childPath.Substring(0, slashIndex + 1);

					if (!String.IsNullOrEmpty(topDirectory))
					{
						ItemReference itemReference = new ItemReference(hardReference.Group, hardReference, topDirectory);
						if (!hardReference.ChildReferences.Contains(itemReference))
						{
							hardReference.ChildReferences.Add(itemReference);

							DirectoryEnumeratedArgs = new ItemEnumeratedEventArgs(itemReference);
							RaiseDirectoryEnumerated();
						}
					}
					else if (String.IsNullOrEmpty(topDirectory) && slashIndex == -1)
					{									
						ItemReference itemReference = new ItemReference(hardReference.Group, hardReference, childPath);
						if (!hardReference.ChildReferences.Contains(itemReference))
						{
							hardReference.ChildReferences.Add(itemReference);

							FileEnumeratedArgs = new ItemEnumeratedEventArgs(itemReference);
							RaiseFileEnumerated();
						}
					}
					else
					{
						break;
					}
				}

				hardReference.IsEnumerated = true;
				EnumerationFinishedArgs = new ItemEnumeratedEventArgs(hardReference);
				RaiseEnumerationFinished();
			}
			else
			{
				throw new InvalidDataException("No listfile was found for the package referenced by this item reference.");
			}
		}

		/// <summary>
		/// Raises the package group added event.
		/// </summary>
		protected void RaisePackageGroupAdded()
		{
			if (PackageGroupAdded != null)
			{
				PackageGroupAdded(this, PackageGroupAddedArgs);
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

		/// <summary>
		/// Raises the enumeration finished event.
		/// </summary>
		protected void RaiseEnumerationFinished()
		{
			if (EnumerationFinished != null)
			{
				EnumerationFinished(this, EnumerationFinishedArgs);
			}
		}

		/// <summary>
		/// Releases all resource used by the <see cref="Everlook.Explorer.ExplorerBuilder"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Everlook.Explorer.ExplorerBuilder"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="Everlook.Explorer.ExplorerBuilder"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="Everlook.Explorer.ExplorerBuilder"/> so the garbage collector can reclaim the memory that the
		/// <see cref="Everlook.Explorer.ExplorerBuilder"/> was occupying.</remarks>
		public void Dispose()
		{
			foreach (KeyValuePair<string, PackageGroup> Group in this.PackageGroups)
			{
				Group.Value.Dispose();
			}
		}
	}

	/// <summary>
	/// Package enumerated event handler.
	/// </summary>
	public delegate void ItemEnumeratedEventHandler(object sender,ItemEnumeratedEventArgs e);

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

