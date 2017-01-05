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

		private readonly object EnumeratedReferenceQueueLock = new object();
		/// <summary>
		/// A list of enumerated references. This list acts as an intermediate location where the UI can fetch results
		/// when it's idle.
		/// </summary>
		public readonly List<FileReference> EnumeratedReferences = new List<FileReference>();

		private ReferenceEnumeratedEventArgs PackageGroupAddedArgs;
		private ReferenceEnumeratedEventArgs PackageEnumeratedArgs;

		/// <summary>
		/// The cached package directories. Used when the user adds or removes game directories during runtime.
		/// </summary>
		private List<string> CachedPackageDirectories = new List<string>();

		/// <summary>
		/// The package groups. This is, at a glance, groupings of packages in a game directory
		/// that act as a cohesive unit. Usually, a single package group represents a single game
		/// instance.
		/// </summary>
		private readonly Dictionary<string, PackageGroup> PackageGroups = new Dictionary<string, PackageGroup>();

		/// <summary>
		/// The package node group mapping. Maps package groups to their base virtual item references.
		/// </summary>
		public readonly Dictionary<PackageGroup, VirtualFileReference> PackageGroupVirtualNodeMapping =
			new Dictionary<PackageGroup, VirtualFileReference>();

		/// <summary>
		/// Maps package names and paths to tree nodes.
		/// Key: Path of package, folder or file.
		/// Value: TreeIter that maps to the package, folder or file.
		/// </summary>
		public readonly Dictionary<FileReference, TreeIter> PackageItemNodeMapping =
			new Dictionary<FileReference, TreeIter>();

		/// <summary>
		/// Maps tree nodes to package names and paths.
		/// Key: TreeIter that represents the item reference.
		/// Value: FileReference that the iter maps to.
		/// </summary>
		public readonly Dictionary<TreeIter, FileReference> PackageNodeItemMapping =
			new Dictionary<TreeIter, FileReference>();

		/// <summary>
		/// The virtual reference mapping. Maps item references to their virtual counterparts.
		/// Key: PackageGroup that hosts this virtual reference.
		/// Dictionary::Key: An item path in an arbitrary package.
		/// Dictionary::Value: A virtual item reference that hosts the hard item references.
		/// </summary>
		private readonly Dictionary<PackageGroup, Dictionary<string, VirtualFileReference>> VirtualReferenceMappings =
			new Dictionary<PackageGroup, Dictionary<string, VirtualFileReference>>();


		/// <summary>
		/// A queue of work submitted by the UI (and indirectly, the user). Worker threads are given
		/// one reference from this queue to be enumerated, and then it is removed.
		/// </summary>
		private readonly List<FileReference> WorkQueue = new List<FileReference>();

		/// <summary>
		/// A queue of references that have not yet been fully enumerated, yet have been submitted to the
		/// work queue. These wait here until they are enumerated, at which point they are resubmitted to the work queue.
		/// </summary>
		private readonly List<FileReference> WaitQueue = new List<FileReference>();

		/// <summary>
		/// The main enumeration loop thread. Accepts work from the work queue and distributes it
		/// to the available threads.
		/// </summary>
		private readonly Thread EnumerationLoopThread;

		/// <summary>
		/// The main resubmission loop thread. Takes waiting references and adds them back to the work queue.
		/// </summary>
		private readonly Thread ResubmissionLoopThread;

		/// <summary>
		/// Whether or not the explorer builder should currently process any work. Acts as an on/off switch
		/// for the main background thread.
		/// </summary>
		private volatile bool bShouldProcessWork;

		/// <summary>
		/// Whether or not all possible package groups for the provided paths in <see cref="CachedPackageDirectories"/>
		/// have been created and loaded.
		/// </summary>
		private bool bArePackageGroupsLoaded;

		/// <summary>
		/// Whether or not the explorer builder is currently reloading. Reloading constitutes clearing all
		/// enumerated data, and recreating all package groups using the new paths.
		/// </summary>
		private bool bIsReloading;


		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Explorer.ExplorerBuilder"/> class.
		/// </summary>
		public ExplorerBuilder()
		{
			ThreadPool.SetMinThreads(10, 4);

			this.EnumerationLoopThread = new Thread(EnumerationLoop)
			{
				Name = "EnumerationLoop",
				IsBackground = true
			};

			this.ResubmissionLoopThread = new Thread(ResubmissionLoop)
			{
				Name = "ResubmissionLoop",
				IsBackground = true
			};

			Reload();
		}

		/// <summary>
		/// Gets a value indicating whether this instance is actively accepting work orders.
		/// </summary>
		/// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
		public bool IsActive
		{
			get { return this.bShouldProcessWork; }
		}

		/// <summary>
		/// Starts the enumeration thread in the background.
		/// </summary>
		public void Start()
		{
			if (!this.EnumerationLoopThread.IsAlive)
			{
				this.bShouldProcessWork = true;

				this.EnumerationLoopThread.Start();
				this.ResubmissionLoopThread.Start();
			}
		}

		/// <summary>
		/// Stops the enumeration thread, allowing it to finish the current work order.
		/// </summary>
		public void Stop()
		{
			if (this.EnumerationLoopThread.IsAlive)
			{
				this.bShouldProcessWork = false;

				this.EnumerationLoopThread.Join();
				this.ResubmissionLoopThread.Join();
			}
		}

		/// <summary>
		/// Reloads the explorer builder, resetting all list files and known content.
		/// </summary>
		public void Reload()
		{
			if (!this.bIsReloading)
			{
				this.bIsReloading = true;
				this.bArePackageGroupsLoaded = false;

				Thread t = new Thread(Reload_Implementation)
				{
					Name = "ReloadExplorer",
					IsBackground = true
				};

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
				this.CachedPackageDirectories = GamePathStorage.Instance.GamePaths;
				this.PackageGroups.Clear();

				if (this.CachedPackageDirectories.Count > 0)
				{
					this.WorkQueue.Clear();
					this.PackageItemNodeMapping.Clear();
					this.PackageNodeItemMapping.Clear();

					this.PackageGroupVirtualNodeMapping.Clear();
					this.VirtualReferenceMappings.Clear();
				}

				foreach (string packageDirectory in this.CachedPackageDirectories)
				{
					if (Directory.Exists(packageDirectory))
					{
						// Create the package group and add it to the available ones
						string folderName = Path.GetFileName(packageDirectory);
						PackageGroup packageGroup = new PackageGroup(folderName, packageDirectory);
						// TODO: Creating a package group is real slow. Speed it up

						this.PackageGroups.Add(folderName, packageGroup);

						// Create a virtual item reference that points to the package group
						VirtualFileReference packageGroupReference = new VirtualFileReference(packageGroup,
							new FileReference(packageGroup))
						{
							State = ReferenceState.Enumerating
						};

						// Create a virtual package folder for the individual packages under the package group
						FileReference packageGroupPackagesFolderReference = new FileReference(packageGroup, packageGroupReference, "");

						// Add the package folder as a child to the package group node
						packageGroupReference.ChildReferences.Add(packageGroupPackagesFolderReference);

						// Send the package group node to the UI
						this.PackageGroupAddedArgs = new ReferenceEnumeratedEventArgs(packageGroupReference);
						RaisePackageGroupAdded();

						// Add the packages in the package group as nodes to the package folder
						foreach (KeyValuePair<string, List<string>> packageListFile in packageGroup.PackageListfiles)
						{
							if (packageListFile.Value != null)
							{
								string packageName = Path.GetFileName(packageListFile.Key);
								FileReference packageReference = new FileReference(packageGroup, packageGroupPackagesFolderReference,
									packageName, "");

								// Send the package node to the UI
								this.PackageEnumeratedArgs = new ReferenceEnumeratedEventArgs(packageReference);
								RaisePackageEnumerated();

								// Submit the package as a work order, enumerating the topmost directories
								SubmitWork(packageReference);
							}
						}
					}
				}

				this.bIsReloading = false;
				this.bArePackageGroupsLoaded = true;
			}
		}

		/// <summary>
		/// Determines whether the package directory changed.
		/// </summary>
		/// <returns><c>true</c> if the package directory has changed; otherwise, <c>false</c>.</returns>
		public bool HasPackageDirectoryChanged()
		{
			return !this.CachedPackageDirectories.OrderBy(t => t).SequenceEqual(GamePathStorage.Instance.GamePaths.OrderBy(t => t));
		}

		/// <summary>
		/// Main loop of this worker class. Enumerates any work placed into the work queue.
		/// </summary>
		private void EnumerationLoop()
		{
			while (this.bShouldProcessWork)
			{
				if (this.bArePackageGroupsLoaded && this.WorkQueue.Count > 0)
				{
					// Grab the first item in the queue and queue it up
					FileReference targetReference = this.WorkQueue.First();
					ThreadPool.QueueUserWorkItem(EnumerateFilesAndFolders, targetReference);
					this.WorkQueue.Remove(targetReference);
				}
			}
		}

		/// <summary>
		/// Submits work to the explorer builder. The work submitted is processed in a
		/// first-in, first-out order as work orders may depend on each other.
		/// </summary>
		/// <param name="reference">Reference.</param>
		public void SubmitWork(FileReference reference)
		{
			if (!this.WorkQueue.Contains(reference) && reference.State == ReferenceState.NotEnumerated)
			{
				reference.State = ReferenceState.Enumerating;
				this.WorkQueue.Add(reference);
			}
			else if (reference.State == ReferenceState.Enumerating)
			{
				this.WaitQueue.Add(reference);
			}
		}

		/// <summary>
		/// Enumerates the files and subfolders in the specified package, starting at
		/// the provided root path.
		/// </summary>
		/// <param name="parentReferenceObject">Parent reference where the search should start.</param>
		protected void EnumerateFilesAndFolders(object parentReferenceObject)
		{
			if (!this.bShouldProcessWork)
			{
				// Early drop out
				return;
			}

			FileReference parentReference = parentReferenceObject as FileReference;
			if (parentReference != null)
			{
				VirtualFileReference virtualParentReference = parentReference as VirtualFileReference;
				if (virtualParentReference != null)
				{
					EnumerateHardReference(virtualParentReference.HardReference);

					for (int i = 0; i < virtualParentReference.OverriddenHardReferences.Count; ++i)
					{
						EnumerateHardReference(virtualParentReference.OverriddenHardReferences[i]);
					}

					virtualParentReference.State = ReferenceState.Enumerated;
				}
				else
				{
					EnumerateHardReference(parentReference); // TODO: Probable issue, no assignment of state
				}
			}
		}

		/// <summary>
		/// Enumerates a hard reference.
		/// </summary>
		/// <param name="hardReference">Hard reference.</param>
		protected void EnumerateHardReference(FileReference hardReference)
		{
			List<FileReference> localEnumeratedReferences = new List<FileReference>();
			List<string> packageListFile;
			if (hardReference.PackageGroup.PackageListfiles.TryGetValue(hardReference.PackageName, out packageListFile))
			{
				IEnumerable<string> strippedListfile =
					packageListFile.Where(s => s.StartsWith(hardReference.FilePath, true, new CultureInfo("en-GB")));
				foreach (string filePath in strippedListfile)
				{
					string childPath = Regex.Replace(filePath, "^(?-i)" + Regex.Escape(hardReference.FilePath), "");

					int slashIndex = childPath.IndexOf('\\');
					string topDirectory = childPath.Substring(0, slashIndex + 1);

					if (!string.IsNullOrEmpty(topDirectory))
					{
						FileReference directoryReference = new FileReference(hardReference.PackageGroup, hardReference, topDirectory);
						if (!hardReference.ChildReferences.Contains(directoryReference))
						{
							hardReference.ChildReferences.Add(directoryReference);

							localEnumeratedReferences.Add(directoryReference);
						}
					}
					else if (string.IsNullOrEmpty(topDirectory) && slashIndex == -1)
					{
						FileReference fileReference = new FileReference(hardReference.PackageGroup, hardReference, childPath);
						if (!hardReference.ChildReferences.Contains(fileReference))
						{
							// Files can't have any children, so it will always be enumerated.
							hardReference.State = ReferenceState.Enumerated;
							hardReference.ChildReferences.Add(fileReference);

							localEnumeratedReferences.Add(fileReference);
						}
					}
					else
					{
						break;
					}
				}


				lock (this.EnumeratedReferenceQueueLock)
				{
					// Add this directory's enumerated files in order as one block
					this.EnumeratedReferences.AddRange(localEnumeratedReferences);
				}

				hardReference.State = ReferenceState.Enumerated;
			}
			else
			{
				throw new InvalidDataException("No listfile was found for the package referenced by this item reference.");
			}
		}

		/// <summary>
		/// The resubmission loop handles waiting references whose parents are currently enumerating. When the parents
		/// are finished, they are readded to the work queue.
		/// </summary>
		private void ResubmissionLoop()
		{
			while (this.bShouldProcessWork)
			{
				List<FileReference> readyReferences = new List<FileReference>();
				for (int i = 0; i < this.WaitQueue.Count; ++i)
				{
					if (this.WaitQueue[i].ParentReference?.State == ReferenceState.Enumerated)
					{
						readyReferences.Add(this.WaitQueue[i]);
					}
				}

				foreach (FileReference readyReference in readyReferences)
				{
					this.WaitQueue.Remove(readyReference);
					SubmitWork(readyReference);
				}
			}
		}

		/// <summary>
		/// Adds a virtual mapping.
		/// </summary>
		/// <param name="hardReference">Hard reference.</param>
		/// <param name="virtualReference">Virtual reference.</param>
		public void AddVirtualMapping(FileReference hardReference, VirtualFileReference virtualReference)
		{
			PackageGroup referenceGroup = hardReference.PackageGroup;
			if (this.VirtualReferenceMappings.ContainsKey(referenceGroup))
			{
				if (!this.VirtualReferenceMappings[referenceGroup].ContainsKey(hardReference.FilePath))
				{
					this.VirtualReferenceMappings[referenceGroup].Add(hardReference.FilePath, virtualReference);
				}
			}
			else
			{
				Dictionary<string, VirtualFileReference> groupDictionary = new Dictionary<string, VirtualFileReference>();
				groupDictionary.Add(hardReference.FilePath, virtualReference);

				this.VirtualReferenceMappings.Add(referenceGroup, groupDictionary);
			}
		}

		/// <summary>
		/// Gets a virtual reference.
		/// </summary>
		/// <returns>The virtual reference.</returns>
		/// <param name="hardReference">Hard reference.</param>
		public VirtualFileReference GetVirtualReference(FileReference hardReference)
		{
			PackageGroup referenceGroup = hardReference.PackageGroup;
			if (this.VirtualReferenceMappings.ContainsKey(referenceGroup))
			{
				VirtualFileReference virtualReference;
				if (this.VirtualReferenceMappings[referenceGroup].TryGetValue(hardReference.FilePath, out virtualReference))
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
				PackageGroupAdded(this, this.PackageGroupAddedArgs);
			}
		}

		/// <summary>
		/// Raises the package enumerated event.
		/// </summary>
		protected void RaisePackageEnumerated()
		{
			if (PackageEnumerated != null)
			{
				PackageEnumerated(this, this.PackageEnumeratedArgs);
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
			Stop();

			foreach (KeyValuePair<string, PackageGroup> group in this.PackageGroups)
			{
				group.Value.Dispose();
			}
		}
	}

	/// <summary>
	/// Package enumerated event handler.
	/// </summary>
	public delegate void ItemEnumeratedEventHandler(object sender, ReferenceEnumeratedEventArgs e);


	/// <summary>
	/// Reference enumerated event arguments.
	/// </summary>
	public class ReferenceEnumeratedEventArgs : EventArgs
	{
		/// <summary>
		/// Contains the enumerated file reference.
		/// </summary>
		/// <value>The item.</value>
		public FileReference Reference { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ReferenceEnumeratedEventArgs"/> class.
		/// </summary>
		/// <param name="inReference">In item.</param>
		public ReferenceEnumeratedEventArgs(FileReference inReference)
		{
			this.Reference = inReference;
		}
	}
}