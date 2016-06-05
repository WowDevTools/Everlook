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
using System.Globalization;

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
		/// The cached package directories. Used when the user adds or removes game directories during runtime.
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
		public readonly Dictionary<PackageGroup, VirtualItemReference> PackageGroupVirtualNodeMapping =
			new Dictionary<PackageGroup, VirtualItemReference>();

		/// <summary>
		/// Maps package names and paths to tree nodes.
		/// Key: Path of package, folder or file.
		/// Value: TreeIter that maps to the package, folder or file.
		/// </summary>
		public readonly Dictionary<ItemReference, TreeIter> PackageItemNodeMapping =
			new Dictionary<ItemReference, TreeIter>();

		/// <summary>
		/// Maps tree nodes to package names and paths.
		/// Key: TreeIter that represents the item reference.
		/// Value: ItemReference that the iter maps to.
		/// </summary>
		public readonly Dictionary<TreeIter, ItemReference> PackageNodeItemMapping =
			new Dictionary<TreeIter, ItemReference>();

		/// <summary>
		/// The virtual reference mapping. Maps item references to their virtual counterparts.
		/// Key: Group that hosts this virtual reference.
		/// Dictionary::Key: An item path in an arbitrary package.
		/// Dictionary::Value: A virtual item reference that hosts the hard item references.
		/// </summary>
		private readonly Dictionary<PackageGroup, Dictionary<string, VirtualItemReference>> VirtualReferenceMappings =
			new Dictionary<PackageGroup, Dictionary<string, VirtualItemReference>>();

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
		/// Gets a value indicating whether this instance is actively accepting work orders.
		/// </summary>
		/// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
		public bool IsActive
		{
			get { return bShouldProcessWork; }
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

				if (CachedPackageDirectories.Count > 0)
				{
					WorkQueue.Clear();
					PackageItemNodeMapping.Clear();
					PackageNodeItemMapping.Clear();

					PackageGroupVirtualNodeMapping.Clear();
					VirtualReferenceMappings.Clear();
				}

				foreach (string packageDirectory in CachedPackageDirectories)
				{
					if (Directory.Exists(packageDirectory))
					{
						// Create the package group and add it to the available ones
						string FolderName = Path.GetFileName(packageDirectory);
						PackageGroup Group = new PackageGroup(FolderName, packageDirectory);
						// TODO: Creating a package group is real slow. Speed it up

						this.PackageGroups.Add(FolderName, Group);

						// Create a virtual item reference that points to the package group
						VirtualItemReference packageGroupReference = new VirtualItemReference(Group,
							new ItemReference(Group));

						// Create a virtual package folder for the individual packages under the package group
						ItemReference packageGroupPackagesFolderReference = new ItemReference(Group, packageGroupReference, "");

						// Add the package folder as a child to the package group node
						packageGroupReference.ChildReferences.Add(packageGroupPackagesFolderReference);

						// Send the references to the UI
						this.PackageGroupAddedArgs = new ItemEnumeratedEventArgs(packageGroupReference);
						RaisePackageGroupAdded();

						// Add the packages in the package group as nodes to the package folder
						foreach (KeyValuePair<string, List<string>> PackageListfile in Group.PackageListfiles)
						{
							if (PackageListfile.Value != null)
							{
								string PackageName = Path.GetFileName(PackageListfile.Key);
								ItemReference packageReference = new ItemReference(Group, packageGroupPackagesFolderReference,
									PackageName, "");

								// Send the package node to the UI
								this.PackageEnumeratedArgs = new ItemEnumeratedEventArgs(packageReference);
								RaisePackageEnumerated();

								// Submit the package as a work order, enumerating the topmost directories
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
			return !CachedPackageDirectories.OrderBy(t => t).SequenceEqual(GamePathStorage.Instance.GamePaths.OrderBy(t => t));
		}

		/// <summary>
		/// Main loop of this worker class. Enumerates any work placed into the work queue.
		/// </summary>
		private void EnumerationLoop()
		{
			while (bShouldProcessWork)
			{
				if (ActiveEnumerationThreads.Count > 0)
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
				}

				if (bArePackageGroupsLoaded && WorkQueue.Count > 0)
				{
					// If there's room for more threads, get the first work order and start a new one
					if (ActiveEnumerationThreads.Count < this.MaxEnumerationThreadCount)
					{
						// Grab the first item in the queue.
						ItemReference targetReference = WorkQueue.First();
						Thread t = new Thread(EnumerateFilesAndFolders);
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
				IEnumerable<string> strippedListfile =
					PackageListfile.Where(s => s.StartsWith(hardReference.ItemPath, true, new CultureInfo("en-GB")));
				foreach (string FilePath in strippedListfile)
				{
					string childPath = Regex.Replace(FilePath, "^(?-i)" + Regex.Escape(hardReference.ItemPath), "");

					int slashIndex = childPath.IndexOf('\\');
					string topDirectory = childPath.Substring(0, slashIndex + 1);

					if (!String.IsNullOrEmpty(topDirectory))
					{
						ItemReference directoryReference = new ItemReference(hardReference.Group, hardReference, topDirectory);
						if (!hardReference.ChildReferences.Contains(directoryReference))
						{
							hardReference.ChildReferences.Add(directoryReference);

							DirectoryEnumeratedArgs = new ItemEnumeratedEventArgs(directoryReference);
							RaiseDirectoryEnumerated();
						}
					}
					else if (String.IsNullOrEmpty(topDirectory) && slashIndex == -1)
					{
						ItemReference fileReference = new ItemReference(hardReference.Group, hardReference, childPath);
						if (!hardReference.ChildReferences.Contains(fileReference))
						{
							// Files can't have any children, so it will always be enumerated.
							fileReference.IsEnumerated = true;

							hardReference.ChildReferences.Add(fileReference);

							FileEnumeratedArgs = new ItemEnumeratedEventArgs(fileReference);
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
		/// Adds a virtual mapping.
		/// </summary>
		/// <param name="hardReference">Hard reference.</param>
		/// <param name="virtualReference">Virtual reference.</param>
		public void AddVirtualMapping(ItemReference hardReference, VirtualItemReference virtualReference)
		{
			PackageGroup referenceGroup = hardReference.Group;
			if (VirtualReferenceMappings.ContainsKey(referenceGroup))
			{
				if (!VirtualReferenceMappings[referenceGroup].ContainsKey(hardReference.ItemPath))
				{
					VirtualReferenceMappings[referenceGroup].Add(hardReference.ItemPath, virtualReference);
				}
			}
			else
			{
				Dictionary<string, VirtualItemReference> groupDictionary = new Dictionary<string, VirtualItemReference>();
				groupDictionary.Add(hardReference.ItemPath, virtualReference);

				VirtualReferenceMappings.Add(referenceGroup, groupDictionary);
			}
		}

		/// <summary>
		/// Gets a virtual reference.
		/// </summary>
		/// <returns>The virtual reference.</returns>
		/// <param name="hardReference">Hard reference.</param>
		public VirtualItemReference GetVirtualReference(ItemReference hardReference)
		{
			PackageGroup referenceGroup = hardReference.Group;
			if (VirtualReferenceMappings.ContainsKey(referenceGroup))
			{
				VirtualItemReference virtualReference;
				if (VirtualReferenceMappings[referenceGroup].TryGetValue(hardReference.ItemPath, out virtualReference))
				{
					return virtualReference;
				}
			}

			return null;
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
	public delegate void ItemEnumeratedEventHandler(object sender, ItemEnumeratedEventArgs e);

	/// <summary>
	/// Item enumerated event arguments.
	/// </summary>
	public class ItemEnumeratedEventArgs : EventArgs
	{
		/// <summary>
		/// Contains the enumerated item reference.
		/// </summary>
		/// <value>The item.</value>
		public ItemReference Item { get; private set; }

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