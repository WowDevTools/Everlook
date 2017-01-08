//
//  ExplorerStore.cs
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
using System.Linq;
using Everlook.Package;
using Everlook.Utility;
using Gdk;
using Gtk;

namespace Everlook.Explorer
{
	/// <summary>
	/// The <see cref="ExplorerStore"/> class acts as an encapsulation class for functions which add, sort and filter
	/// the files and folders added to the file explorer.
	/// </summary>
	public class ExplorerStore
	{
		/// <summary>
		/// The <see cref="TreeStore"/> to populate with nodes.
		/// </summary>
		private readonly TreeStore FiletreeStore;

		/// <summary>
		/// The <see cref="TreeModelFilter"/> to use for filtering the nodes.
		/// </summary>
		private readonly TreeModelFilter FiletreeFilter;

		/// <summary>
		/// The <see cref="TreeModelSort"/> to use for sorting the nodes.
		/// </summary>
		private readonly TreeModelSort FiletreeStorter;

		/// <summary>
		/// The package node group mapping. Maps package groups to their base virtual item references.
		/// </summary>
		private readonly Dictionary<PackageGroup, VirtualFileReference> PackageGroupVirtualNodeMapping =
			new Dictionary<PackageGroup, VirtualFileReference>();

		/// <summary>
		/// Maps tree nodes to package names and paths.
		/// Key: TreeIter that represents the item reference.
		/// Value: FileReference that the iter maps to.
		/// </summary>
		private readonly Dictionary<TreeIter, FileReference> PackageNodeItemMapping =
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
		/// Creates a new instance of the <see cref="ExplorerStore"/> class and binds it to the provided set of
		/// <see cref="TreeView"/> storage classes.
		/// </summary>
		/// <param name="inFiletreeStore">The <see cref="TreeStore"/> to store nodes in.</param>
		/// <param name="inFiletreeFilter">The <see cref="TreeModelFilter"/> to use for filtering the nodes.</param>
		/// <param name="inFiletreeSorter">The <see cref="TreeModelSort"/> to use for sorting the nodes.</param>
		public ExplorerStore(TreeStore inFiletreeStore, TreeModelFilter inFiletreeFilter, TreeModelSort inFiletreeSorter)
		{
			this.FiletreeStore = inFiletreeStore;
			this.FiletreeFilter = inFiletreeFilter;
			this.FiletreeStorter = inFiletreeSorter;
		}

		/// <summary>
		/// Clears the contents of the storage.
		/// </summary>
		public void Clear()
		{
			this.PackageNodeItemMapping.Clear();

			this.PackageGroupVirtualNodeMapping.Clear();
			this.VirtualReferenceMappings.Clear();
		}

		/// <summary>
		/// Adds a package group node to the game explorer view
		/// </summary>
		/// <param name="groupReference">PackageGroup reference.</param>
		public void AddPackageGroupNode(FileReference groupReference)
		{
			// Add the group node
			Pixbuf packageGroupIcon = IconManager.GetIcon("user-home");
			TreeIter packageGroupNode = this.FiletreeStore.AppendValues(packageGroupIcon,
				groupReference.PackageGroup.GroupName, "", "Virtual file tree", (int)NodeType.PackageGroup);
			groupReference.ReferenceIter = packageGroupNode;

			this.PackageNodeItemMapping.Add(packageGroupNode, groupReference);

			VirtualFileReference virtualGroupReference = groupReference as VirtualFileReference;
			if (virtualGroupReference != null)
			{
				this.PackageGroupVirtualNodeMapping.Add(groupReference.PackageGroup, virtualGroupReference);
			}

			// Add the package folder subnode
			Pixbuf packageFolderIcon = IconManager.GetIcon("applications-other");
			TreeIter packageFolderNode = this.FiletreeStore.AppendValues(packageGroupNode,
				packageFolderIcon, "Packages", "", "Individual packages", (int)NodeType.PackageFolder);

			groupReference.ChildReferences.First().ReferenceIter = packageFolderNode;

			this.PackageNodeItemMapping.Add(packageFolderNode, groupReference.ChildReferences.First());
		}

		/// <summary>
		/// Adds a package node to the game explorer view.
		/// </summary>
		/// <param name="packageReference">File reference pointing to the package.</param>
		public void AddPackageNode(FileReference packageReference)
		{
			bool hasParentBeenAdded = packageReference.ParentReference.HasBeenAddedToTheUI();
			bool hasThisBeenAdded = packageReference.HasBeenAddedToTheUI();
			if (hasParentBeenAdded && !hasThisBeenAdded)
			{
				TreeIter parentIter = packageReference.ParentReference.ReferenceIter;
				Pixbuf packageIcon = IconManager.GetIcon("package-x-generic");

				TreeIter packageNode = this.FiletreeStore.AppendValues(parentIter,
					packageIcon, packageReference.PackageName, "", "", (int)NodeType.Package);
				packageReference.ReferenceIter = packageNode;

				this.PackageNodeItemMapping.Add(packageNode, packageReference);
			}

			// Map package nodes to virtual root nodes
			VirtualFileReference virtualGroupReference;
			if (this.PackageGroupVirtualNodeMapping.TryGetValue(packageReference.PackageGroup, out virtualGroupReference))
			{
				AddVirtualMapping(packageReference, virtualGroupReference);
			}
		}

		/// <summary>
		/// Adds a directory node to the game explorer view, attachedt to the provided parent
		/// package and directory.
		/// </summary>
		/// <param name="childReference">Child reference representing the directory.</param>
		public void AddDirectoryNode(FileReference childReference)
		{
			if (childReference.ParentReference.HasBeenAddedToTheUI() && !childReference.HasBeenAddedToTheUI())
			{
				TreeIter parentIter = childReference.ParentReference.ReferenceIter;
				TreeIter node = CreateDirectoryTreeNode(parentIter, childReference);
				childReference.ReferenceIter = node;

				this.PackageNodeItemMapping.Add(node, childReference);
			}

			// Now, let's add (or append to) the virtual node
			VirtualFileReference virtualParentReference = GetVirtualReference(childReference.ParentReference);

			if (virtualParentReference != null)
			{
				if (virtualParentReference.HasBeenAddedToTheUI())
				{

					VirtualFileReference virtualChildReference = GetVirtualReference(childReference);

					if (virtualChildReference != null)
					{
						// Append this directory reference as an additional overridden hard reference
						virtualChildReference.OverriddenHardReferences.Add(childReference);
					}
					else
					{
						virtualChildReference = new VirtualFileReference(virtualParentReference, childReference.PackageGroup, childReference);

						if (!virtualParentReference.ChildReferences.Contains(virtualChildReference))
						{
							virtualParentReference.ChildReferences.Add(virtualChildReference);

							// Create a new virtual reference and a node that maps to it.
							TreeIter virtualParentIter = virtualParentReference.ReferenceIter;
							TreeIter node = CreateDirectoryTreeNode(virtualParentIter, virtualChildReference);
							virtualChildReference.ReferenceIter = node;

							this.PackageNodeItemMapping.Add(node, virtualChildReference);

							AddVirtualMapping(childReference, virtualChildReference);
						}
					}
				}
			}
		}

		/// <summary>
		/// Creates a node in the <see cref="FiletreeStore"/> for the specified directory reference, as
		/// a child below the specified parent node.
		/// </summary>
		/// <param name="parentNode">The parent node where the new node should be attached.</param>
		/// <param name="directory">The <see cref="FileReference"/> describing the directory.</param>
		/// <returns>A <see cref="TreeIter"/> pointing to the new directory node.</returns>
		private TreeIter CreateDirectoryTreeNode(TreeIter parentNode, FileReference directory)
		{
			Pixbuf directoryIcon = IconManager.GetIcon(Stock.Directory);
			return this.FiletreeStore.AppendValues(parentNode,
				directoryIcon, directory.GetReferencedItemName(), "", "", (int)NodeType.Directory);
		}

		/// <summary>
		/// Adds a file reference to the game explorer view, attached to its parent reference
		/// </summary>
		/// <param name="childReference">Child file reference.</param>
		public void AddFileNode(FileReference childReference)
		{
			if (childReference.ParentReference.HasBeenAddedToTheUI() && !childReference.HasBeenAddedToTheUI())
			{
				childReference.ParentReference.ChildReferences.Add(childReference);

				TreeIter parentIter = childReference.ParentReference.ReferenceIter;
				TreeIter node = CreateFileTreeNode(parentIter, childReference);
				childReference.ReferenceIter = node;

				this.PackageNodeItemMapping.Add(node, childReference);
			}

			// Now, let's add (or append to) the virtual node
			VirtualFileReference virtualParentReference = GetVirtualReference(childReference.ParentReference);

			if (virtualParentReference != null)
			{
				if (virtualParentReference.HasBeenAddedToTheUI())
				{

					VirtualFileReference virtualChildReference = GetVirtualReference(childReference);

					if (virtualChildReference != null)
					{
						// Append this directory reference as an additional overridden hard reference
						virtualChildReference.OverriddenHardReferences.Add(childReference);
					}
					else
					{
						virtualChildReference = new VirtualFileReference(virtualParentReference, childReference.PackageGroup, childReference);

						if (!virtualParentReference.ChildReferences.Contains(virtualChildReference))
						{
							virtualParentReference.ChildReferences.Add(virtualChildReference);

							// Create a new virtual reference and a node that maps to it.
							TreeIter virtualParentNode = virtualParentReference.ReferenceIter;
							TreeIter node = CreateFileTreeNode(virtualParentNode, virtualChildReference);
							virtualChildReference.ReferenceIter = node;

							this.PackageNodeItemMapping.Add(node, virtualChildReference);

							AddVirtualMapping(childReference, virtualChildReference);
						}
					}
				}
			}
		}

		/// <summary>
		/// Creates a node in the <see cref="FiletreeStore"/> for the specified file reference, as
		/// a child below the specified parent node.
		/// </summary>
		/// <param name="parentNode">The parent node where the new node should be attached.</param>
		/// <param name="file">The <see cref="FileReference"/> describing the file.</param>
		/// <returns>A <see cref="TreeIter"/> pointing to the new directory node.</returns>
		private TreeIter CreateFileTreeNode(TreeIter parentNode, FileReference file)
		{
			return this.FiletreeStore.AppendValues(parentNode, file.GetIcon(),
				file.GetReferencedItemName(), "", "", (int)NodeType.File);
		}

		/// <summary>
		/// Adds a virtual mapping.
		/// </summary>
		/// <param name="hardReference">Hard reference.</param>
		/// <param name="virtualReference">Virtual reference.</param>
		private void AddVirtualMapping(FileReference hardReference, VirtualFileReference virtualReference)
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
		/// Converts a <see cref="TreeIter"/> into an <see cref="FileReference"/>. The reference object is queried
		/// from the explorerBuilder's internal store.
		/// </summary>
		/// <returns>The FileReference object pointed to by the TreeIter.</returns>
		/// <param name="iter">The TreeIter.</param>
		public FileReference GetItemReferenceFromIter(TreeIter iter)
		{
			FileReference reference;
			if (this.PackageNodeItemMapping.TryGetValue(iter, out reference))
			{
				return reference;
			}

			if (this.PackageNodeItemMapping.TryGetValue(GetStoreIterFromFilterIter(iter), out reference))
			{
				return reference;
			}

			if (this.PackageNodeItemMapping.TryGetValue(GetStoreIterFromSorterIter(iter), out reference))
			{
				return reference;
			}

			return null;
		}

		/// <summary>
		/// Gets the <see cref="TreePath"/> of the specified <see cref="TreeIter"/>.
		/// </summary>
		/// <param name="iter">The iter to get the path for.</param>
		/// <returns>The TreePath of the iter.</returns>
		public TreePath GetPath(TreeIter iter)
		{
			TreePath storePath = this.FiletreeStore.GetPath(iter);
			if (storePath != null)
			{
				return storePath;
			}

			TreePath filterPath = this.FiletreeStore.GetPath(GetStoreIterFromFilterIter(iter));
			if (filterPath != null)
			{
				return filterPath;
			}

			TreePath sorterPath = this.FiletreeStore.GetPath(GetStoreIterFromSorterIter(iter));
			if (sorterPath != null)
			{
				return sorterPath;
			}

			return null;
		}

		/// <summary>
		/// Converts a <see cref="TreePath"/> into an <see cref="FileReference"/>. The reference object is queried
		/// from the explorerBuilder's internal store.
		/// </summary>
		/// <returns>The FileReference object pointed to by the TreeIter.</returns>
		/// <param name="path">The TreeIter.</param>
		public FileReference GetItemReferenceFromPath(TreePath path)
		{
			return GetItemReferenceFromIter(GetStoreIterFromVisiblePath(path));
		}

		/// <summary>
		/// Gets a <see cref="TreeIter"/> that's valid for the <see cref="FiletreeStore"/> from a
		/// <see cref="TreePath"/> visible to the user in the UI.
		/// </summary>
		/// <param name="path">The TreePath.</param>
		/// <returns>A <see cref="TreeIter"/>.</returns>
		public TreeIter GetStoreIterFromVisiblePath(TreePath path)
		{
			TreeIter sorterIter;
			this.FiletreeStorter.GetIter(out sorterIter, path);
			return GetStoreIterFromSorterIter(sorterIter);
		}

		/// <summary>
		/// Gets a <see cref="TreeIter"/> that's valid for the <see cref="FiletreeStore"/> from a TreeIter
		/// valid for the <see cref="FiletreeStorter"/>.
		/// </summary>
		/// <param name="sorterIter">The GameExplorerTreeSorter iter.</param>
		/// <returns>A <see cref="TreeIter"/>.</returns>
		public TreeIter GetStoreIterFromSorterIter(TreeIter sorterIter)
		{
			TreeIter filterIter = this.FiletreeStorter.ConvertIterToChildIter(sorterIter);
			return GetStoreIterFromFilterIter(filterIter);
		}

		/// <summary>
		/// Gets a <see cref="TreeIter"/> that's valid for the <see cref="FiletreeStore"/> from a TreeIter
		/// valid for the <see cref="FiletreeFilter"/>.
		/// </summary>
		/// <param name="filterIter">The GameExplorerTreeFilter iter.</param>
		/// <returns>A <see cref="TreeIter"/>.</returns>
		public TreeIter GetStoreIterFromFilterIter(TreeIter filterIter)
		{
			return this.FiletreeFilter.ConvertIterToChildIter(filterIter);
		}
	}
}