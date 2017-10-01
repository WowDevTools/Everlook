//
//  FileTreeModel.cs
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
using System.Text;
using Everlook.Package;
using Everlook.Utility;
using GLib;
using Gtk;
using liblistfile;
using liblistfile.NodeTree;

using FileNode = liblistfile.NodeTree.Node;
using Object = GLib.Object;

namespace Everlook.Explorer
{
	/// <summary>
	/// GTK TreeModel which serves an <see cref="OptimizedNodeTree"/>.
	/// </summary>
	public class FileTreeModel : Object, ITreeModelImplementor
	{
		private readonly OptimizedNodeTree Tree;

		/// <summary>
		/// Gets the flags of the model.
		/// </summary>
		public TreeModelFlags Flags => TreeModelFlags.ItersPersist;

		/// <summary>
		/// Gets the number of columns in the model.
		/// </summary>
		public int NColumns => 1;

		/// <summary>
		/// A randomly generated stamp for the tree.
		/// </summary>
		private readonly int Stamp;

		/// <summary>
		/// Initializes a new instance of the <see cref="FileTreeModel"/> class.
		/// </summary>
		/// <param name="nodeTree">The precomputed node tree to wrap around.</param>
		public FileTreeModel(OptimizedNodeTree nodeTree)
		{
			this.Tree = nodeTree;
			this.Stamp = new Random().Next();
		}

		/// <summary>
		/// Gets the name of a given node.
		/// </summary>
		/// <param name="node">The node to get the name of.</param>
		/// <returns>The name of the node.</returns>
		public string GetNodeName(FileNode node)
		{
			return this.Tree.GetNodeName(node);
		}

		/// <summary>
		/// Gets the package the given node belongs to.
		/// </summary>
		/// <param name="node">The node to get the package of.</param>
		/// <returns>The name of the package.</returns>
		public string GetNodePackage(FileNode node)
		{
			var currentNode = node;
			while (!(currentNode.Type.HasFlag(NodeType.Package) || currentNode.Type.HasFlag(NodeType.Meta)))
			{
				currentNode = this.Tree.GetNode((ulong)currentNode.ParentOffset);
			}

			return GetNodeName(currentNode);
		}

		/// <summary>
		/// Gets the absolute file path of a node within the package.
		/// </summary>
		/// <param name="node">The node to get the path of.</param>
		/// <returns>The file path of the node in the package.</returns>
		public string GetNodeFilePath(FileNode node)
		{
			StringBuilder sb = new StringBuilder();

			var currentNode = node;
			while (!(currentNode.Type.HasFlag(NodeType.Package) || currentNode.Type.HasFlag(NodeType.Meta)))
			{
				if (currentNode.Type.HasFlag(NodeType.Directory))
				{
					sb.Insert(0, '\\');
				}

				sb.Insert(0, GetNodeName(currentNode));

				currentNode = this.Tree.GetNode((ulong)currentNode.ParentOffset);
			}

			return sb.ToString();
		}

		/// <summary>
		/// Enumerates all references pointing to files under the given reference. If the reference already points to
		/// a file, it is returned back. If it is null, nothing is returned. The search is performed as a depth-first
		/// level scan.
		/// </summary>
		/// <param name="fileReference">The reference to enumerate.</param>
		/// <returns>A set of all the child references of the given reference.</returns>
		public IEnumerable<FileReference> EnumerateFilesOfReference(FileReference fileReference)
		{
			if (fileReference == null)
			{
				yield break;
			}

			if (fileReference.IsFile)
			{
				yield return fileReference;
				yield break;
			}

			List<FileNode> folderNodes = new List<FileNode> { fileReference.Node };

			while (folderNodes.Count > 0)
			{
				var folderNode = folderNodes.First();

				foreach (ulong offset in folderNode.ChildOffsets)
				{
					var childNode = this.Tree.GetNode(offset);

					if (childNode.Type.HasFlag(NodeType.File))
					{
						yield return new FileReference
						(
							fileReference.Context,
							childNode,
							fileReference.PackageName,
							GetNodeFilePath(childNode)
						);
					}

					if (childNode.Type.HasFlag(NodeType.Directory))
					{
						folderNodes.Add(childNode);
					}
				}

				folderNodes.Remove(folderNode);
			}
		}

		/// <summary>
		/// Gets a <see cref="FileReference"/> from a given iter in the tree.
		/// </summary>
		/// <param name="gameContext">The game context for the reference.</param>
		/// <param name="iter">The iter in the tree.</param>
		/// <returns>The FileReference pointed to by the given iter.</returns>
		/// <exception cref="InvalidDataException">Thrown if the iter doesn't belong to the model.</exception>
		public FileReference GetReferenceByIter(IGameContext gameContext, TreeIter iter)
		{
			if (iter.Stamp != this.Stamp)
			{
				throw new InvalidDataException("The given iter was not valid for this model.");
			}

			var node = this.Tree.GetNode((ulong)iter.UserData);
			if (node == null)
			{
				throw new InvalidDataException("The iter did not contain a valid node offset.");
			}

			return new FileReference(gameContext, node, GetNodePackage(node), GetNodeFilePath(node));
		}

		/// <summary>
		/// Gets a <see cref="FileReference"/> from a given path in the tree.
		/// </summary>
		/// <param name="gameContext">The game context for the reference.</param>
		/// <param name="path">The path in the tree.</param>
		/// <returns>The FileReference pointed to by the given TreePath.</returns>
		public FileReference GetReferenceByPath(IGameContext gameContext, TreePath path)
		{
			GetIter(out var iter, path);
			return GetReferenceByIter(gameContext, iter);
		}

		/// <summary>
		/// Gets the path to the node pointed to by <paramref name="nodePath"/>. If the path doesn't point to a specific
		/// node, it will be set to the deepest possible node.
		/// </summary>
		/// <param name="nodePath">The path to the node. Expected to have '\' as its separator character.</param>
		/// <returns>The path to the node.</returns>
		public TreePath GetPath(string nodePath)
		{
			if (string.IsNullOrEmpty(nodePath))
			{
				throw new ArgumentNullException(nameof(nodePath));
			}

			var parentNode = this.Tree.Root;
			string[] pathParts = nodePath.Split('\\');

			var result = new TreePath();

			foreach (var part in pathParts)
			{
				bool foundNode = false;
				foreach (var nodeOffset in parentNode.ChildOffsets)
				{
					var node = this.Tree.GetNode(nodeOffset);
					if (!string.Equals(this.Tree.GetNodeName(node), part, StringComparison.OrdinalIgnoreCase))
					{
						// Skip descendant nodes that are not part of the path
						continue;
					}

					var childIndex = parentNode.ChildOffsets.IndexOf(nodeOffset);
					result.AppendIndex(childIndex);

					parentNode = node;

					foundNode = true;
					break;
				}

				if (!foundNode)
				{
					// If we reach this point, we did not find a matching node on this level. Therefore, the path is
					// invalid.
					return null;
				}
			}

			return result;
		}

		/// <summary>
		/// Gets the type of the given column.
		/// </summary>
		/// <param name="index">The index of the column.</param>
		/// <returns>The GType of the column.</returns>
		public GType GetColumnType(int index)
		{
			if (index > 0)
			{
				return GType.Invalid;
			}

			return LookupGType(typeof(FileNode));
		}

		/// <summary>
		/// Gets an iter at a specified path.
		/// </summary>
		/// <param name="iter">Will contain the iter.</param>
		/// <param name="path">The path to the iter.</param>
		/// <returns>true if the iter is now set to the iter at the given path; false otherwise.</returns>
		public bool GetIter(out TreeIter iter, TreePath path)
		{
			iter = TreeIter.Zero;

			ulong currentOffset = 0;
			var currentNode = this.Tree.Root;
			foreach (int index in path.Indices)
			{
				ulong longIndex = (ulong)index;
				if (longIndex > currentNode.ChildCount - 1)
				{
					return false;
				}

				currentOffset = currentNode.ChildOffsets[index];
				currentNode = this.Tree.GetNode(currentOffset);
			}

			iter.UserData = new IntPtr((long)currentOffset);
			iter.Stamp = this.Stamp;
			return true;
		}

		/// <summary>
		/// Gets the path to a specified iter.
		/// </summary>
		/// <param name="iter">The iter to get the path of.</param>
		/// <returns>The TreePath corresponding to the given iter.</returns>
		public TreePath GetPath(TreeIter iter)
		{
			if (iter.Stamp != this.Stamp)
			{
				throw new InvalidDataException("The given iter was not valid for this model.");
			}

			var result = new TreePath();
			var node = this.Tree.GetNode((ulong)iter.UserData);
			if (node == null)
			{
				return result;
			}

			while (node.ParentOffset > -1)
			{
				var parentNode = this.Tree.GetNode((ulong)node.ParentOffset);
				ulong nodeOffset = this.Tree.GetNodeOffset(node);
				result.PrependIndex(parentNode.ChildOffsets.IndexOf(nodeOffset));

				node = parentNode;
			}

			result.PrependIndex(this.Tree.Root.ChildOffsets.IndexOf((ulong)iter.UserData));
			return result;
		}

		/// <summary>
		/// Gets the value stored in the model at a given iter.
		/// </summary>
		/// <param name="iter">The iter where the value is stored.</param>
		/// <param name="column">The column to get the value from</param>
		/// <param name="value">Will contain the value.</param>
		/// <exception cref="InvalidDataException">Thrown if the iter doesn't belong to the model.</exception>
		public void GetValue(TreeIter iter, int column, ref Value value)
		{
			if (iter.Stamp != this.Stamp && !iter.Equals(TreeIter.Zero))
			{
				throw new InvalidDataException("The given iter was not valid for this model.");
			}

			var node = iter.Equals(TreeIter.Zero) ? this.Tree.Root : this.Tree.GetNode((ulong)iter.UserData);
			if (node == null)
			{
				return;
			}

			value.Init(LookupGType(typeof(FileNode)));
			value.Val = node;
		}

		/// <summary>
		/// Moves the given iter to the next one at the same level.
		/// </summary>
		/// <param name="iter">The iter to move.</param>
		/// <returns>true if the iter is now set to the next iter at the same level; false otherwise</returns>
		/// <exception cref="InvalidDataException">Thrown if the iter doesn't belong to the model.</exception>
		public bool IterNext(ref TreeIter iter)
		{
			if (iter.Stamp != this.Stamp)
			{
				throw new InvalidDataException("The given iter was not valid for this model.");
			}

			ulong currentOffset = (ulong)iter.UserData;
			var currentNode = this.Tree.GetNode(currentOffset);
			var parentNode = this.Tree.GetNode((ulong)currentNode.ParentOffset);

			int currentIndex = parentNode.ChildOffsets.IndexOf(currentOffset);
			int nextIndex = currentIndex + 1;

			if (nextIndex < (int)parentNode.ChildCount)
			{
				iter.UserData = new IntPtr((long)parentNode.ChildOffsets[nextIndex]);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Moves the given iter to the previous one at the same level.
		/// </summary>
		/// <param name="iter">The iter to move.</param>
		/// <returns>true if the iter is now set to the previous iter at the same level; false otherwise.</returns>
		/// <exception cref="InvalidDataException">Thrown if the iter doesn't belong to the model.</exception>
		public bool IterPrevious(ref TreeIter iter)
		{
			if (iter.Stamp != this.Stamp)
			{
				throw new InvalidDataException("The given iter was not valid for this model.");
			}

			ulong currentOffset = (ulong)iter.UserData;
			var currentNode = this.Tree.GetNode(currentOffset);

			var parentNode = this.Tree.GetNode((ulong)currentNode.ParentOffset);

			int currentIndex = parentNode.ChildOffsets.IndexOf(currentOffset);
			int previousIndex = currentIndex - 1;

			if (previousIndex >= 0 && previousIndex < (int)parentNode.ChildCount)
			{
				iter.UserData = new IntPtr((long)parentNode.ChildOffsets[previousIndex]);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Gets the iter of the first child of the given iter.
		/// </summary>
		/// <param name="iter">Will contain the first child.</param>
		/// <param name="parent">The iter to get the first child from.</param>
		/// <returns>true if the iter is now set to the first child of the parent; false otherwise</returns>
		/// <exception cref="InvalidDataException">Thrown if the iter doesn't belong to the model.</exception>
		/// <exception cref="ArgumentException">Thrown if the iter is not valid.</exception>
		public bool IterChildren(out TreeIter iter, TreeIter parent)
		{
			if (parent.Stamp != this.Stamp)
			{
				throw new InvalidDataException("The given parent was not valid for this model.");
			}

			iter = TreeIter.Zero;

			var node = parent.Equals(TreeIter.Zero) ? this.Tree.Root : this.Tree.GetNode((ulong)parent.UserData);
			if (node == null)
			{
				throw new ArgumentException("The given iter was not valid.", nameof(parent));
			}

			if (!node.HasChildren())
			{
				return false;
			}

			iter.UserData = new IntPtr((long)node.ChildOffsets.First());
			iter.Stamp = this.Stamp;
			return true;
		}

		/// <summary>
		/// Determines whether or not the given iter has any children.
		/// </summary>
		/// <param name="iter">The iter to check.</param>
		/// <returns>true if the iter has any children; false otherwise</returns>
		/// <exception cref="InvalidDataException">Thrown if the iter doesn't belong to the model.</exception>
		/// <exception cref="ArgumentException">Thrown if the iter is not valid.</exception>
		public bool IterHasChild(TreeIter iter)
		{
			if (iter.Stamp != this.Stamp)
			{
				throw new InvalidDataException("The given iter was not valid for this model.");
			}

			var node = iter.Equals(TreeIter.Zero) ? this.Tree.Root : this.Tree.GetNode((ulong)iter.UserData);
			if (node == null)
			{
				throw new ArgumentException("The given iter was not valid.", nameof(iter));
			}

			return node.HasChildren();
		}

		/// <summary>
		/// Determines the number of children an iter has.
		/// </summary>
		/// <param name="iter">The iter to count the children of.</param>
		/// <returns>The number of children that the iter has.</returns>
		/// <exception cref="InvalidDataException">Thrown if the iter doesn't belong to the model.</exception>
		/// <exception cref="ArgumentException">Thrown if the iter is not valid.</exception>
		public int IterNChildren(TreeIter iter)
		{
			if (iter.Equals(TreeIter.Zero))
			{
				return (int)this.Tree.Root.ChildCount;
			}

			if (iter.Stamp != this.Stamp)
			{
				throw new InvalidDataException("The given iter was not valid for this model.");
			}

			var node = this.Tree.GetNode((ulong)iter.UserData);
			if (node == null)
			{
				throw new ArgumentException("The given iter was not valid.", nameof(iter));
			}

			return (int)node.ChildCount;
		}

		/// <summary>
		/// Gets the nth child of the provided iter.
		/// </summary>
		/// <param name="iter">Will contain the nth child.</param>
		/// <param name="parent">The iter to get the child of.</param>
		/// <param name="n">The value of n.</param>
		/// <returns>true if the iter is now set to the nth child of the parent; false otherwise</returns>
		/// <exception cref="InvalidDataException">Thrown if the iter doesn't belong to the model.</exception>
		/// <exception cref="ArgumentException">Thrown if the iter is not valid.</exception>
		public bool IterNthChild(out TreeIter iter, TreeIter parent, int n)
		{
			if (parent.Stamp != this.Stamp && !parent.Equals(TreeIter.Zero))
			{
				throw new InvalidDataException("The given parent was not valid for this model.");
			}

			iter = TreeIter.Zero;

			if (n < 0)
			{
				return false;
			}

			var node = iter.Equals(TreeIter.Zero) ? this.Tree.Root : this.Tree.GetNode((ulong)iter.UserData);

			if (node == null)
			{
				throw new ArgumentException("The given iter was not valid.", nameof(parent));
			}

			if (!node.HasChildren() || n > (int)node.ChildCount - 1)
			{
				return false;
			}

			iter.UserData = new IntPtr((long)node.ChildOffsets[n]);
			iter.Stamp = this.Stamp;
			return true;
		}

		/// <summary>
		/// Gets the parent of the given iter.
		/// </summary>
		/// <param name="iter">Will contain the parent iter.</param>
		/// <param name="child">The iter to get the parent of.</param>
		/// <returns>true if the iter is now set to the parent iter of the child; false otherwise</returns>
		/// <exception cref="InvalidDataException">Thrown if the iter doesn't belong to the model.</exception>
		/// <exception cref="ArgumentException">Thrown if the iter is not valid.</exception>
		public bool IterParent(out TreeIter iter, TreeIter child)
		{
			if (child.Stamp != this.Stamp)
			{
				throw new InvalidDataException("The given child was not valid for this model.");
			}

			iter = TreeIter.Zero;

			var childNode = this.Tree.GetNode((ulong)child.UserData);
			if (childNode == null)
			{
				throw new ArgumentException("The given iter was not valid.", nameof(child));
			}

			var parentNode = this.Tree.GetNode((ulong)childNode.ParentOffset);
			if (parentNode == null)
			{
				return false;
			}

			iter.UserData = new IntPtr((long)this.Tree.GetNodeOffset(parentNode));
			iter.Stamp = this.Stamp;
			return true;
		}

		/// <summary>
		/// Loads the specified iter into the cache. Currently unused.
		/// </summary>
		/// <param name="iter">The iter to load.</param>
		public void RefNode(TreeIter iter)
		{
			// Ignored for now
		}

		/// <summary>
		/// Unloads the specified iter from the cache. Currently unused.
		/// </summary>
		/// <param name="iter">The iter to unload.</param>
		public void UnrefNode(TreeIter iter)
		{
			// Ignored for now
		}
	}
}
