//
//  FileTreeModel.cs
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
using System.Linq;
using GLib;
using Gtk;
using liblistfile;
using Object = GLib.Object;
using FileNode = liblistfile.NodeTree.Node;

namespace Everlook.Explorer
{
	/// <summary>
	/// GTK TreeModel which serves an <see cref="OptimizedNodeTree"/>.
	/// </summary>
	public class FileTreeModel : Object, ITreeModelImplementor
	{
		private readonly OptimizedNodeTree Tree;

		/// <summary>
		/// The flags of the model.
		/// </summary>
		public TreeModelFlags Flags
		{
			get { return TreeModelFlags.ItersPersist; }
		}

		/// <summary>
		/// The number of columns in the model.
		/// </summary>
		public int NColumns
		{
			get { return 1; }
		}

		/// <summary>
		/// Creates a new <see cref="FileTreeModel"/> and attaches it to an <see cref="OptimizedNodeTree"/>.
		/// </summary>
		/// <param name="nodeTree"></param>
		public FileTreeModel(OptimizedNodeTree nodeTree) : base()
		{
			this.Tree = nodeTree;
		}

		/// <summary>
		/// Gets the name of a given node.
		/// </summary>
		/// <param name="node"></param>
		/// <returns></returns>
		public string GetNodeName(FileNode node)
		{
			return this.Tree.GetNodeName(node);
		}

		/// <summary>
		/// Gets the type of the given column.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
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
		/// <param name="iter"></param>
		/// <param name="path"></param>
		/// <returns></returns>
		public bool GetIter(out TreeIter iter, TreePath path)
		{
			iter = TreeIter.Zero;

			ulong currentOffset = 0;
			FileNode currentNode = this.Tree.Root;
			foreach (int index in path.Indices)
			{
				ulong longIndex = (ulong) index;
				if (longIndex > currentNode.ChildCount - 1)
				{
					return false;
				}

				currentOffset = currentNode.ChildOffsets[index];
				currentNode = this.Tree.GetNode(currentOffset);
			}

			iter.UserData = new IntPtr((long)currentOffset);
			return true;
		}

		/// <summary>
		/// Gets the path to a specified iter.
		/// </summary>
		/// <param name="iter"></param>
		/// <returns></returns>
		public TreePath GetPath(TreeIter iter)
		{
			TreePath result = new TreePath();
			FileNode node = this.Tree.GetNode((ulong) iter.UserData);
			if (node == null)
			{
				return result;
			}

			while (node.ParentOffset > -1)
			{
				FileNode parentNode = this.Tree.GetNode((ulong)node.ParentOffset);
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
		/// <param name="iter"></param>
		/// <param name="column"></param>
		/// <param name="value"></param>
		public void GetValue(TreeIter iter, int column, ref Value value)
		{
			FileNode node = this.Tree.GetNode((ulong) iter.UserData);
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
		/// <param name="iter"></param>
		/// <returns></returns>
		public bool IterNext(ref TreeIter iter)
		{
			ulong currentOffset = (ulong) iter.UserData;
			FileNode currentNode = this.Tree.GetNode(currentOffset);
			FileNode parentNode = this.Tree.GetNode((ulong)currentNode.ParentOffset);

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
		/// <param name="iter"></param>
		/// <returns></returns>
		public bool IterPrevious(ref TreeIter iter)
		{
			ulong currentOffset = (ulong) iter.UserData;
			FileNode currentNode = this.Tree.GetNode(currentOffset);

			FileNode parentNode = this.Tree.GetNode((ulong)currentNode.ParentOffset);

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
		/// <param name="iter"></param>
		/// <param name="parent"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public bool IterChildren(out TreeIter iter, TreeIter parent)
		{
			iter = TreeIter.Zero;

			FileNode node = parent.Equals(TreeIter.Zero) ? this.Tree.Root : this.Tree.GetNode((ulong) parent.UserData);
			if (node == null)
			{
				throw new ArgumentException("The given iter was not valid.", nameof(parent));
			}

			if (!node.HasChildren())
			{
				return false;
			}

			iter.UserData = new IntPtr((long)node.ChildOffsets.First());
			return true;
		}

		/// <summary>
		/// Determines whether or not the given iter has any children.
		/// </summary>
		/// <param name="iter"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public bool IterHasChild(TreeIter iter)
		{
			FileNode node = iter.Equals(TreeIter.Zero) ? this.Tree.Root : this.Tree.GetNode((ulong) iter.UserData);
			if (node == null)
			{
				throw new ArgumentException("The given iter was not valid.", nameof(iter));
			}

			return node.HasChildren();
		}

		/// <summary>
		/// Determines the number of children an iter has.
		/// </summary>
		/// <param name="iter"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public int IterNChildren(TreeIter iter)
		{
			if (iter.Equals(TreeIter.Zero))
			{
				return (int)this.Tree.Root.ChildCount;
			}

			FileNode node = this.Tree.GetNode((ulong) iter.UserData);
			if (node == null)
			{
				throw new ArgumentException("The given iter was not valid.", nameof(iter));
			}

			return (int)node.ChildCount;
		}

		/// <summary>
		/// Gets the nth child of the provided iter.
		/// </summary>
		/// <param name="iter"></param>
		/// <param name="parent"></param>
		/// <param name="n"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public bool IterNthChild(out TreeIter iter, TreeIter parent, int n)
		{
			iter = TreeIter.Zero;

			if (n < 0)
			{
				return false;
			}

			FileNode node = iter.Equals(TreeIter.Zero) ? this.Tree.Root : this.Tree.GetNode((ulong) iter.UserData);

			if (node == null)
			{
				throw new ArgumentException("The given iter was not valid.", nameof(parent));
			}

			if (!node.HasChildren() || n > (int)node.ChildCount -1)
			{
				return false;
			}

			iter.UserData = new IntPtr((long)node.ChildOffsets[n]);
			return true;
		}

		/// <summary>
		/// Gets the parent of the given iter.
		/// </summary>
		/// <param name="iter"></param>
		/// <param name="child"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public bool IterParent(out TreeIter iter, TreeIter child)
		{
			iter = TreeIter.Zero;
			FileNode childNode = this.Tree.GetNode((ulong) child.UserData);
			if (childNode == null)
			{
				throw new ArgumentException("The given iter was not valid.", nameof(child));
			}

			FileNode parentNode = this.Tree.GetNode((ulong) childNode.ParentOffset);
			if (parentNode == null)
			{
				return false;
			}

			iter.UserData = new IntPtr((long)this.Tree.GetNodeOffset(parentNode));
			return true;
		}

		/// <summary>
		/// Loads the specified iter into the cache. Currently unused.
		/// </summary>
		/// <param name="iter"></param>
		public void RefNode(TreeIter iter)
		{
			// Ignored for now
		}

		/// <summary>
		/// Unloads the specified iter from the cache. Currently unused.
		/// </summary>
		/// <param name="iter"></param>
		public void UnrefNode(TreeIter iter)
		{
			// Ignored for now
		}

		/// <summary>
		/// Disposes the model and the underlying tree, releasing the data.
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				this.Tree.Dispose();
			}
		}
	}
}