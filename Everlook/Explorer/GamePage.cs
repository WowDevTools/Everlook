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
using System.IO;
using Everlook.Configuration;
using Everlook.Package;
using Everlook.Utility;
using Gdk;
using GLib;
using static Everlook.Utility.CommunicationDelegates;
using Gtk;
using liblistfile;
using liblistfile.NodeTree;
using Warcraft.Core;
using FileNode = liblistfile.NodeTree.Node;
using Style = Pango.Style;

namespace Everlook.Explorer
{
	/// <summary>
	/// A <see cref="GamePage"/> encapsulates a <see cref="TreeView"/> with a bound node tree which the user
	/// can explore. It also handles events which the tree produces as the user navigates it.
	/// </summary>
	public class GamePage : IDisposable
	{
		/// <summary>
		/// Raised whenever a file is selected in the tree which can be displayed in the interface.
		/// </summary>
		public event FileActionDelegate FileLoadRequested;

		/// <summary>
		/// Raised whenever a file is requested to be queued for exporting.
		/// </summary>
		public event FileActionDelegate EnqueueFileExportRequested;

		/// <summary>
		/// Raised whenever a file is requeste to e
		/// </summary>
		public event FileActionDelegate ExportItemRequested;

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

		private readonly CellRendererPixbuf NodeIconRenderer;
		private readonly CellRendererText NodeNameRenderer;

		private readonly Menu TreeContextMenu;
		private readonly ImageMenuItem SaveItem;
		private readonly ImageMenuItem ExportItem;
		private readonly ImageMenuItem OpenItem;
		private readonly ImageMenuItem QueueForExportItem;
		private readonly ImageMenuItem CopyPathItem;

		/// <summary>
		/// Static reference to the configuration handler.
		/// </summary>
		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

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

			this.NodeIconRenderer = new CellRendererPixbuf();
			this.NodeNameRenderer = new CellRendererText();

			TreeViewColumn column = new TreeViewColumn();
			column.Title = "Data Files";
			column.Spacing = 4;
			column.PackStart(this.NodeIconRenderer, true);
			column.PackStart(this.NodeNameRenderer, true);

			column.SetCellDataFunc(this.NodeIconRenderer, RenderNodeIcon);
			column.SetCellDataFunc(this.NodeNameRenderer, RenderNodeName);

			this.TreeAlignment.Add(this.Tree);

			this.Tree.RowActivated += OnRowActivated;
			this.Tree.ButtonPressEvent += OnButtonPressed;
			this.Tree.Selection.Changed += OnSelectionChanged;

			this.TreeContextMenu = new Menu();

			// Save item context button
			this.SaveItem = new ImageMenuItem(Stock.Save);
			this.SaveItem.Activated += OnSaveItem;
			this.TreeContextMenu.Add(this.SaveItem);

			// Export item context button
			this.ExportItem = new ImageMenuItem("Export")
			{
				Image = new Image(Stock.Convert)
			};
			this.ExportItem.Activated += OnExportItemRequested;
			this.TreeContextMenu.Add(this.ExportItem);

			// Open item context button
			this.OpenItem = new ImageMenuItem(Stock.Open);
			this.OpenItem.Activated += OnOpenItem;
			this.TreeContextMenu.Add(this.OpenItem);

			// Queue for export context button
			this.QueueForExportItem = new ImageMenuItem("Queue for export")
			{
				Image = new Image(Stock.Convert)
			};
			this.QueueForExportItem.Activated += OnQueueForExportRequested;
			this.TreeContextMenu.Add(this.QueueForExportItem);

			// Separator
			SeparatorMenuItem separator = new SeparatorMenuItem();
			this.TreeContextMenu.Add(separator);

			// Copy path context button
			this.CopyPathItem = new ImageMenuItem("Copy path")
			{
				Image = new Image(Stock.Copy)
			};
			this.CopyPathItem.Activated += OnCopyPath;
			this.TreeContextMenu.Add(this.CopyPathItem);
		}

		/// <summary>
		/// Handles rendering of the icon of a node in the tree.
		/// </summary>
		/// <param name="column"></param>
		/// <param name="cell"></param>
		/// <param name="model"></param>
		/// <param name="iter"></param>
		/// <exception cref="NotImplementedException"></exception>
		private static void RenderNodeIcon(TreeViewColumn column, CellRenderer cell, ITreeModel model, TreeIter iter)
		{
			CellRendererPixbuf cellIcon = cell as CellRendererPixbuf;
			FileNode node = (FileNode) model.GetValue(iter, 0);

			if (node == null || cellIcon == null)
			{
				return;
			}

			if (node.Type.HasFlag(NodeType.Directory))
			{
				cellIcon.Pixbuf = IconManager.GetIconForFiletype(WarcraftFileType.Directory);
				return;
			}

			cellIcon.Pixbuf = IconManager.GetIconForFiletype(node.FileType);
		}

		/// <summary>
		/// Handles rendering of the name of a node in the tree.
		/// </summary>
		/// <param name="column"></param>
		/// <param name="cell"></param>
		/// <param name="model"></param>
		/// <param name="iter"></param>
		/// <exception cref="NotImplementedException"></exception>
		private void RenderNodeName(TreeViewColumn column, CellRenderer cell, ITreeModel model, TreeIter iter)
		{
			CellRendererText cellText = cell as CellRendererText;
			FileNode node = (FileNode) model.GetValue(iter, 0);

			if (node == null || cellText == null)
			{
				return;
			}

			cellText.Text = this.TreeModel.GetNodeName(node);

			if (node.Type.HasFlag(NodeType.Deleted))
			{
				cellText.Style = Style.Italic;
				cellText.ForegroundRgba = new RGBA
				{
					Red = 1.0,
					Green = 0.0,
					Blue = 0.0,
					Alpha = 1.0
				};
			}
		}

		/// <summary>
		/// Handles queueing of a file or directory for exporting in the main UI.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="eventArgs"></param>
		private void OnQueueForExportRequested(object sender, EventArgs eventArgs)
		{
			TreeIter selectedIter;
			this.Tree.Selection.GetSelected(out selectedIter);

			FileReference fileReference = this.TreeModel.GetReferenceByIter(this.Packages, selectedIter);
			if (fileReference == null)
			{
				return;
			}

			RequestEnqueueFileExport(fileReference);
		}

		/// <summary>
		/// Handles passing off export requests for files in the tree.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="eventArgs"></param>
		private void OnExportItemRequested(object sender, EventArgs eventArgs)
		{
			TreeIter selectedIter;
            this.Tree.Selection.GetSelected(out selectedIter);

            FileReference fileReference = this.TreeModel.GetReferenceByIter(this.Packages, selectedIter);
            if (fileReference == null)
            {
                return;
            }

			RequestFileExport(fileReference);
		}

		/// <summary>
		/// Handles opening of files from the archive triggered by a context menu press. If it's a directory, the
		/// row is expanded. If not, and the file is a "normal" file, it is extracted and handed off to the operating
		/// system.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="eventArgs">E.</param>
		private void OnOpenItem(object sender, EventArgs eventArgs)
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
				OpenReference(fileReference);
			}
			else
			{
				this.Tree.ExpandRow(this.TreeModel.GetPath(selectedIter), false);
			}
		}

		/// <summary>
		/// Handles copying of the file path of a selected item in the archive, triggered by a
		/// context menu press.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="eventArgs">E.</param>
		private void OnCopyPath(object sender, EventArgs eventArgs)
		{
			TreeIter selectedIter;
			this.Tree.Selection.GetSelected(out selectedIter);

			FileReference fileReference = this.TreeModel.GetReferenceByIter(this.Packages, selectedIter);
			if (fileReference == null)
			{
				return;
			}

			Clipboard clipboard = Clipboard.Get(Atom.Intern("CLIPBOARD", false));
            clipboard.Text = fileReference.FilePath;
		}

		/// <summary>
		/// Handles extraction of files from the archive triggered by a context menu press.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnSaveItem(object sender, EventArgs eventArgs)
		{
			TreeIter selectedIter;
			this.Tree.Selection.GetSelected(out selectedIter);

			FileReference fileReference = this.TreeModel.GetReferenceByIter(this.Packages, selectedIter);
			if (fileReference == null)
			{
				return;
			}

			string cleanFilepath = fileReference.FilePath.ConvertPathSeparatorsToCurrentNativeSeparator();
			string exportpath;
			if (this.Config.GetShouldKeepFileDirectoryStructure())
			{
				exportpath = this.Config.GetDefaultExportDirectory() + cleanFilepath;
				Directory.CreateDirectory(Directory.GetParent(exportpath).FullName);
			}
			else
			{
				string filename = Path.GetFileName(cleanFilepath);
				exportpath = this.Config.GetDefaultExportDirectory() + filename;
				Directory.CreateDirectory(Directory.GetParent(exportpath).FullName);
			}

			byte[] file = fileReference.Extract();
			if (file != null)
			{
				File.WriteAllBytes(exportpath, file);
			}
		}

		/// <summary>
		/// Handles spawning of the context menu.
		/// </summary>
		/// <param name="o"></param>
		/// <param name="args"></param>
		private void OnButtonPressed(object o, ButtonPressEventArgs args)
		{
			if (args.Event.Type != EventType.ButtonPress || args.Event.Button != 3)
			{
				// Not a right-click, early return
				return;
			}

			TreePath path;
	        this.Tree.GetPathAtPos((int)args.Event.X, (int)args.Event.Y, out path);

	        FileReference currentFileReference = null;
	        if (path != null)
	        {
	            currentFileReference = this.TreeModel.GetReferenceByPath(this.Packages, path);
	        }

			if (string.IsNullOrEmpty(currentFileReference?.FilePath))
			{
				this.SaveItem.Sensitive = false;
				this.ExportItem.Sensitive = false;
				this.OpenItem.Sensitive = false;
				this.QueueForExportItem.Sensitive = false;
				this.CopyPathItem.Sensitive = false;
			}
			else
			{
				if (!currentFileReference.IsFile)
				{
					this.SaveItem.Sensitive = false;
					this.ExportItem.Sensitive = true;
					this.OpenItem.Sensitive = true;
					this.QueueForExportItem.Sensitive = true;
					this.CopyPathItem.Sensitive = true;
				}
				else
				{
					this.SaveItem.Sensitive = true;
					this.ExportItem.Sensitive = true;
					this.OpenItem.Sensitive = true;
					this.QueueForExportItem.Sensitive = true;
					this.CopyPathItem.Sensitive = true;
				}
			}

			this.TreeContextMenu.ShowAll();
			this.TreeContextMenu.Popup();
		}

		/// <summary>
		/// Handles notifying subscribers that the selection in the tree has changed, requesting file loading
		/// if it is a file.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnSelectionChanged(object sender, EventArgs e)
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
				RequestFileLoad(fileReference);
			}
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
	            OpenReference(fileReference);
            }
            else
            {
                this.Tree.ExpandRow(args.Path, false);
            }
		}

		/// <summary>
		/// Opens the given reference by extracting it, saving it to a temporary file, and handing it off
		/// to the operating system.
		/// </summary>
		/// <param name="fileReference"></param>
		private static void OpenReference(FileReference fileReference)
		{
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

		/// <summary>
		/// Requests the loading of the specified <see cref="FileReference"/> in the main UI.
		/// </summary>
		/// <param name="fileReference"></param>
		private void RequestFileLoad(FileReference fileReference)
		{
			this.FileLoadRequested?.Invoke(this, fileReference);
		}

		/// <summary>
		/// Requests the exporting of the specified <see cref="FileReference"/> in the main UI.
		/// </summary>
		/// <param name="fileReference"></param>
		private void RequestFileExport(FileReference fileReference)
		{
			this.ExportItemRequested?.Invoke(this, fileReference);
		}

		/// <summary>
		/// Requests the enqueueing for export of the specified <see cref="FileReference"/> in the main UI.
		/// </summary>
		/// <param name="fileReference"></param>
		private void RequestEnqueueFileExport(FileReference fileReference)
		{
			this.EnqueueFileExportRequested?.Invoke(this, fileReference);
		}

		/// <summary>
		/// Sorts the game explorer row.
		/// </summary>
		/// <returns>The sorting priority of the row. This value can be -1, 0 or 1 if
		/// A sorts before B, A sorts with B or A sorts after B, respectively.</returns>
		/// <param name="model">Model.</param>
		/// <param name="a">Iter a.</param>
		/// <param name="b">Iter b.</param>
		private int SortGameTreeRow(ITreeModel model, TreeIter a, TreeIter b)
		{
			const int sortABeforeB = -1;
			const int sortAWithB = 0;
			const int sortAAfterB = 1;

			FileNode nodeA = (FileNode) model.GetValue(a, 0);
			FileNode nodeB = (FileNode) model.GetValue(b, 0);

			NodeType typeofA = nodeA.Type;
			NodeType typeofB = nodeB.Type;

			if (typeofA < typeofB)
			{
				return sortAAfterB;
			}
			if (typeofA > typeofB)
			{
				return sortABeforeB;
			}

			string aComparisonString = this.TreeModel.GetNodeName(nodeA);

			string bComparisonString = this.TreeModel.GetNodeName(nodeB);

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