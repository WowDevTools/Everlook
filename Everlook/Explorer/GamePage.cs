//
//  GamePage.cs
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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Everlook.Package;
using Gtk;
using liblistfile;
using liblistfile.NodeTree;
using Warcraft.Core;
using FileNode = liblistfile.NodeTree.Node;

namespace Everlook.Explorer
{
	/// <summary>
	/// A <see cref="GamePage"/> encapsulates a <see cref="TreeView"/> with a bound node tree which the user
	/// can explore. It also handles events which the tree produces as the user navigates it.
	/// </summary>
	public class GamePage : IDisposable
	{
		/// <summary>
		/// The widget which is at the top level of the page.
		/// </summary>
		public Widget PageWidget => this.TreeAlignment;

		/// <summary>
		/// The alias of this page, that is, its name.
		/// </summary>
		public string Alias { get; set; }

		private readonly Alignment TreeAlignment;
		private TreeView Tree { get; }

		private readonly PackageGroup Packages;

		private readonly FileTreeModel TreeModel;
		private readonly TreeModelSort TreeSorter;
		private readonly TreeModelFilter TreeFilter;

		/// <summary>
		/// Creates a new <see cref="GamePage"/> for the given package group and node tree.
		/// </summary>
		/// <param name="packageGroup"></param>
		/// <param name="nodeTree"></param>
		public GamePage(PackageGroup packageGroup, OptimizedNodeTree nodeTree)
		{
			this.Packages = packageGroup;
			this.TreeModel = new FileTreeModel(nodeTree);

			this.TreeAlignment = new Alignment(0.5f, 0.5f, 1.0f, 1.0f);

			this.TreeFilter = new TreeModelFilter(new TreeModelAdapter(this.TreeModel), new TreePath());
			this.TreeSorter = new TreeModelSort(this.TreeFilter);

			this.TreeSorter.SetSortFunc(0, SortGameTreeRow);
			this.TreeSorter.SetSortColumnId(0, SortType.Descending);

			this.Tree = new TreeView(this.TreeSorter);
			this.TreeAlignment.Add(this.Tree);

			this.Tree.RowActivated += OnRowActivated;
		}

		/// <summary>
		/// Handles double-clicking on files in the explorer.
		/// </summary>
		/// <param name="o">The sending object.</param>
		/// <param name="args">Arguments describing the row that was activated.</param>
		private void OnRowActivated(object o, RowActivatedArgs args)
		{
			TreeIter selectedIter;
            this.Tree.Selection.GetSelected(out selectedIter);

            FileReference fileReference = this.TreeModel.GetReferenceByIter(this.Packages, selectedIter);
            if (fileReference == null)
            {
                return;
            }

            if (fileReference.IsFile)
            {
                if (string.IsNullOrEmpty(fileReference.FilePath))
                {
                    return;
                }

                switch (fileReference.GetReferencedFileType())
                {
                    // Warcraft-typed standard files
                    case WarcraftFileType.AddonManifest:
                    case WarcraftFileType.AddonManifestSignature:
                    case WarcraftFileType.ConfigurationFile:
                    case WarcraftFileType.Hashmap:
                    case WarcraftFileType.XML:
                    case WarcraftFileType.INI:
                    case WarcraftFileType.PDF:
                    case WarcraftFileType.HTML:
                    {
                        byte[] fileData = fileReference.Extract();
                        if (fileData != null)
                        {
                            // create a temporary file and write the data to it.
                            string tempPath = Path.GetTempPath() + fileReference.Filename;
                            if (File.Exists(tempPath))
                            {
                                File.Delete(tempPath);
                            }

                            using (Stream tempStream = File.Open(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
                            {
                                tempStream.Write(fileData, 0, fileData.Length);
                                tempStream.Flush();
                            }

                            // Hand off the file to the operating system.
                            System.Diagnostics.Process.Start(tempPath);
                        }

                        break;
                    }
                }
            }
            else
            {
                this.Tree.ExpandRow(args.Path, false);
            }
		}

		/// <summary>
		/// Sorts the game explorer row.
		/// </summary>
		/// <returns>The sorting priority of the row. This value can be -1, 0 or 1 if
		/// A sorts before B, A sorts with B or A sorts after B, respectively.</returns>
		/// <param name="model">Model.</param>
		/// <param name="a">Iter a.</param>
		/// <param name="b">Iter b.</param>
		private static int SortGameTreeRow(ITreeModel model, TreeIter a, TreeIter b)
		{
			const int sortABeforeB = -1;
			const int sortAWithB = 0;
			const int sortAAfterB = 1;

			NodeType typeofA = ((FileNode)model.GetValue(a, 0)).Type;
			NodeType typeofB = ((FileNode)model.GetValue(b, 0)).Type;

			if (typeofA < typeofB)
			{
				return sortAAfterB;
			}
			if (typeofA > typeofB)
			{
				return sortABeforeB;
			}

			string aComparisonString = (string)model.GetValue(a, 1);

			string bComparisonString = (string)model.GetValue(a, 1);

			int result = string.CompareOrdinal(aComparisonString, bComparisonString);

			if (result <= sortABeforeB)
			{
				return sortAAfterB;
			}

			if (result >= sortAAfterB)
			{
				return sortABeforeB;
			}

			return sortAWithB;
		}

		/// <summary>
		/// Disposes the game page, and all related items.
		/// </summary>
		public void Dispose()
		{
			this.TreeAlignment?.Dispose();
			this.Packages?.Dispose();
			this.TreeModel?.Dispose();
			this.TreeSorter?.Dispose();
			this.TreeFilter?.Dispose();
			this.Tree?.Dispose();
		}
	}
}