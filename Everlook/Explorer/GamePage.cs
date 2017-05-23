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
using System.Threading;
using System.Threading.Tasks;
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
		/// The currently filtered file types.
		/// </summary>
		private WarcraftFileType FilteredFileTypes;

		/// <summary>
		/// Whether or not to filter based on the filtered file types.
		/// </summary>
		private bool IsFiltered;

		/// <summary>
		/// Creates a new <see cref="GamePage"/> for the given package group and node tree.
		/// </summary>
		/// <param name="packageGroup"></param>
		/// <param name="nodeTree"></param>
		public GamePage(PackageGroup packageGroup, OptimizedNodeTree nodeTree)
		{
			this.Packages = packageGroup;
			this.TreeModel = new FileTreeModel(nodeTree);

			this.TreeAlignment = new Alignment(0.5f, 0.5f, 1.0f, 1.0f)
			{
				TopPadding = 1,
				BottomPadding = 1
			};

			this.TreeFilter = new TreeModelFilter(new TreeModelAdapter(this.TreeModel), null)
			{
				VisibleFunc = TreeModelVisibilityFunc
			};

			this.TreeSorter = new TreeModelSort(this.TreeFilter);

			this.TreeSorter.SetSortFunc(0, SortGameTreeRow);
			this.TreeSorter.SetSortColumnId(0, SortType.Descending);

			this.Tree = new TreeView(this.TreeSorter)
			{
				HeadersVisible = true,
				EnableTreeLines = true
			};

			CellRendererPixbuf nodeIconRenderer = new CellRendererPixbuf
			{
				Xalign = 0.0f
			};
			CellRendererText nodeNameRenderer = new CellRendererText
			{
				Xalign = 0.0f
			};

			TreeViewColumn column = new TreeViewColumn
			{
				Title = "Data Files",
				Spacing = 4
			};
			column.PackStart(nodeIconRenderer, false);
			column.PackStart(nodeNameRenderer, false);

			column.SetCellDataFunc(nodeIconRenderer, RenderNodeIcon);
			column.SetCellDataFunc(nodeNameRenderer, RenderNodeName);

			this.Tree.AppendColumn(column);

			ScrolledWindow sw = new ScrolledWindow
			{
				this.Tree
			};

			this.TreeAlignment.Add(sw);

			this.Tree.RowActivated += OnRowActivated;
			this.Tree.ButtonPressEvent += OnButtonPressed;
			this.Tree.Selection.Changed += OnSelectionChanged;

			this.TreeContextMenu = new Menu();

			// Save item context button
			this.SaveItem = new ImageMenuItem
			{
				UseStock = true,
				Label = Stock.Save,
				CanFocus = false,
				TooltipText = "Save the currently selected item to disk.",
				UseUnderline = true
			};
			this.SaveItem.Activated += OnSaveItem;
			this.TreeContextMenu.Add(this.SaveItem);

			// Export item context button
			this.ExportItem = new ImageMenuItem("Export")
			{
				Image = new Image(Stock.Convert, IconSize.Button),
				CanFocus = false,
				TooltipText = "Exports the currently selected item to another format.",
			};
			this.ExportItem.Activated += OnExportItemRequested;
			this.TreeContextMenu.Add(this.ExportItem);

			// Open item context button
			this.OpenItem = new ImageMenuItem
			{
				UseStock = true,
				Label = Stock.Open,
				CanFocus = false,
				TooltipText = "Open the currently selected item.",
				UseUnderline = true
			};
			this.OpenItem.Activated += OnOpenItem;
			this.TreeContextMenu.Add(this.OpenItem);

			// Queue for export context button
			this.QueueForExportItem = new ImageMenuItem("Queue for export")
			{
				Image = new Image(Stock.Convert, IconSize.Button),
				CanFocus = false,
				TooltipText = "Queues the currently selected item for batch export.",
			};
			this.QueueForExportItem.Activated += OnQueueForExportRequested;
			this.TreeContextMenu.Add(this.QueueForExportItem);

			// Separator
			SeparatorMenuItem separator = new SeparatorMenuItem();
			this.TreeContextMenu.Add(separator);

			// Copy path context button
			this.CopyPathItem = new ImageMenuItem("Copy path")
			{
				Image = new Image(Stock.Copy, IconSize.Button),
				CanFocus = false,
				TooltipText = "Copy the path of the currently selected item.",
			};
			this.CopyPathItem.Activated += OnCopyPath;
			this.TreeContextMenu.Add(this.CopyPathItem);

			this.TreeAlignment.ShowAll();
		}

		/// <summary>
		/// Sets the tree sensitivity, i.e, if the user can interact with it.
		/// </summary>
		/// <param name="sensitive"></param>
		public void SetTreeSensitivity(bool sensitive)
		{
			this.Tree.Sensitive = sensitive;
		}

		/// <summary>
		/// Sets the filter of the tree view and asynchronously refilters it. The filter is a set of 
		/// <see cref="WarcraftFileType"/> flags.
		/// </summary>
		/// <param name="filteredFileTypes"></param>
		/// <returns></returns>
		public void SetFilter(WarcraftFileType filteredFileTypes)
		{
			this.FilteredFileTypes = filteredFileTypes;
		}

		/// <summary>
		/// Sets whether or not the tree is filtered.
		/// </summary>
		/// <param name="isFiltered">True if the tree should be filtered by the set file types, false otherwise.</param>
		/// <returns></returns>
		public void SetFilterState(bool isFiltered)
		{
			this.IsFiltered = isFiltered;
		}

		/// <summary>
		/// Asynchronously refilters the node tree.
		/// </summary>
		/// <returns></returns>
		public async Task RefilterAsync()
		{
			await Task.Factory.StartNew(() =>
			{
				this.TreeFilter.Refilter();
			}, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
		}

		/// <summary>
		/// This function is used internally by the tree filter to determine which rows are visible and which are not.
		/// </summary>
		/// <param name="model"></param>
		/// <param name="iter"></param>
		/// <returns></returns>
		private bool TreeModelVisibilityFunc(ITreeModel model, TreeIter iter)
		{
			if (!this.IsFiltered)
			{
				return true;
			}

			FileNode node = (FileNode)model.GetValue(iter, 0);
			WarcraftFileType nodeTypes = node.FileType;

			// If the file types of the node and the filtered types overlap in any way, then it should
			// be displayed.
			if ((nodeTypes & this.FilteredFileTypes) != 0)
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Handles rendering of the icon of a node in the tree.
		/// </summary>
		/// <param name="column"></param>
		/// <param name="cell"></param>
		/// <param name="model"></param>
		/// <param name="iter"></param>
		/// <exception cref="NotImplementedException"></exception>
		private void RenderNodeIcon(TreeViewColumn column, CellRenderer cell, ITreeModel model, TreeIter iter)
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

			if (node.Type.HasFlag(NodeType.Deleted))
			{
				cellIcon.Pixbuf = IconManager.GetIcon("package-broken");
				return;
			}

			if (node.Type.HasFlag(NodeType.Meta) && this.TreeModel.GetNodeName(node) == "Packages")
			{
				cellIcon.Pixbuf = IconManager.GetIcon("applications-other");
				return;
			}

			if (node.Type.HasFlag(NodeType.Package))
			{
				cellIcon.Pixbuf = IconManager.GetIcon("package-x-generic");
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
				cellText.Foreground = "#D74328";
			}
			else
			{
				cellText.Style = Style.Normal;
				cellText.Foreground = null;
			}
		}

		/// <summary>
		/// Handles queueing of a file or directory for exporting in the main UI.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="eventArgs"></param>
		private void OnQueueForExportRequested(object sender, EventArgs eventArgs)
		{
			FileReference fileReference = GetSelectedReference();
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
			FileReference fileReference = GetSelectedReference();
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
			FileReference fileReference = GetSelectedReference();
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
				TreeIter selectedIter;
				this.Tree.Selection.GetSelected(out selectedIter);

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
			FileReference fileReference = GetSelectedReference();
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
		/// <param name="eventArgs">E.</param>
		private void OnSaveItem(object sender, EventArgs eventArgs)
		{
			FileReference fileReference = GetSelectedReference();
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
		[ConnectBefore]
		private void OnButtonPressed(object o, ButtonPressEventArgs args)
		{
			if (args.Event.Type != EventType.ButtonPress || args.Event.Button != 3)
			{
				// Not a right-click, early return
				return;
			}

			TreePath sorterPath;
	        this.Tree.GetPathAtPos((int)args.Event.X, (int)args.Event.Y, out sorterPath);
			TreePath filterPath = this.TreeSorter.ConvertPathToChildPath(sorterPath);
			TreePath modelPath = this.TreeFilter.ConvertPathToChildPath(filterPath);


			if (modelPath == null)
			{
				this.SaveItem.Sensitive = false;
				this.ExportItem.Sensitive = false;
				this.OpenItem.Sensitive = false;
				this.QueueForExportItem.Sensitive = false;
				this.CopyPathItem.Sensitive = false;
				return;
			}

			FileReference currentFileReference = this.TreeModel.GetReferenceByPath(this.Packages, modelPath);
			if (currentFileReference.IsDirectory)
			{
				this.SaveItem.Sensitive = false;
				this.ExportItem.Sensitive = true;
				this.OpenItem.Sensitive = true;
				this.QueueForExportItem.Sensitive = true;
				this.CopyPathItem.Sensitive = true;
			}
			else if (currentFileReference.IsFile)
			{
				this.SaveItem.Sensitive = true;
				this.ExportItem.Sensitive = true;
				this.OpenItem.Sensitive = true;
				this.QueueForExportItem.Sensitive = true;
				this.CopyPathItem.Sensitive = true;
			}
			else
			{
				this.SaveItem.Sensitive = false;
				this.ExportItem.Sensitive = false;
				this.OpenItem.Sensitive = true;
				this.QueueForExportItem.Sensitive = false;
				this.CopyPathItem.Sensitive = false;
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
			FileReference fileReference = GetSelectedReference();
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
			FileReference fileReference = GetSelectedReference();
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
	            if (this.Tree.GetRowExpanded(args.Path))
	            {
		            this.Tree.CollapseRow(args.Path);
	            }
	            else
	            {
		            this.Tree.ExpandRow(args.Path, false);
	            }
            }
		}

		/// <summary>
		/// Gets the reference which maps to the currently selected node.
		/// </summary>
		/// <returns></returns>
		private FileReference GetSelectedReference()
		{
			TreeIter selectedIter;
			this.Tree.Selection.GetSelected(out selectedIter);

			TreeIter filterIter = this.TreeSorter.ConvertIterToChildIter(selectedIter);
			TreeIter modeliter = this.TreeFilter.ConvertIterToChildIter(filterIter);

			return this.TreeModel.GetReferenceByIter(this.Packages, modeliter);
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
				case WarcraftFileType.Web:
				case WarcraftFileType.Text:
				case WarcraftFileType.WaveAudio:
				case WarcraftFileType.MP3Audio:
				case WarcraftFileType.WMAAudio:
				case WarcraftFileType.VorbisAudio:
				case WarcraftFileType.BitmapImage:
				case WarcraftFileType.GIFImage:
				case WarcraftFileType.JPGImage:
				case WarcraftFileType.PNGImage:
				case WarcraftFileType.TargaImage:
				case WarcraftFileType.Subtitles:
				case WarcraftFileType.Font:
				case WarcraftFileType.Script:
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

			// Special case for meta nodes - if A is a meta node, but B is not
			if (typeofA.HasFlag(NodeType.Meta) && !typeofB.HasFlag(NodeType.Meta))
			{
				// Then it should always sort after B
				return sortAAfterB;
			}

			if (typeofB.HasFlag(NodeType.Meta) && !typeofA.HasFlag(NodeType.Meta))
			{
				// Then it should always sort before B
				return sortABeforeB;
			}

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