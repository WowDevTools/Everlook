//
//  GamePage.cs
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
using System.Threading;
using System.Threading.Tasks;
using Everlook.Package;
using Everlook.Utility;
using FileTree.Tree.Nodes;
using FileTree.Tree.Serialized;
using Gdk;
using GLib;
using Gtk;
using Warcraft.Core;
using EventArgs = System.EventArgs;
using Menu = Gtk.Menu;
using Style = Pango.Style;
using Task = System.Threading.Tasks.Task;

namespace Everlook.Explorer
{
    /// <summary>
    /// A <see cref="GamePage"/> encapsulates a <see cref="TreeView"/> with a bound node tree which the user
    /// can explore. It also handles events which the tree produces as the user navigates it.
    /// </summary>
    public sealed class GamePage : IDisposable
    {
        /// <summary>
        /// Raised whenever a file is selected in the tree which can be displayed in the interface.
        /// </summary>
        public event Func<GamePage, FileReference, Task>? FileLoadRequested;

        /// <summary>
        /// Raised whenever a file or folder is requested to be saved.
        /// </summary>
        public event Func<GamePage, IEnumerable<FileReference>, Task>? SaveRequested;

        /// <summary>
        /// Raised whenever a file is requested to be queued for exporting.
        /// </summary>
        public event Func<GamePage, FileReference, Task>? EnqueueFileExportRequested;

        /// <summary>
        /// Raised whenever a file is requeste to be exported.
        /// </summary>
        public event Func<GamePage, FileReference, Task>? ExportItemRequested;

        /// <summary>
        /// Gets the widget which is at the top level of the page.
        /// </summary>
        public Widget PageWidget => _treeAlignment;

        /// <summary>
        /// Gets or sets the alias of this page, that is, its name.
        /// </summary>
        public string Alias { get; set; }

        private readonly Alignment _treeAlignment;

        private readonly TaskScheduler _uiTaskScheduler;

        private TreeView Tree { get; }

        private readonly FileTreeModel _treeModel;
        private readonly TreeModelSort _treeSorter;
        private readonly TreeModelFilter _treeFilter;

        private readonly Menu _treeContextMenu;
        private readonly ImageMenuItem _saveItem;
        private readonly ImageMenuItem _exportItem;
        private readonly ImageMenuItem _openItem;
        private readonly ImageMenuItem _queueForExportItem;
        private readonly ImageMenuItem _copyPathItem;

        /// <summary>
        /// Gets the game context associated with this page.
        /// </summary>
        public WarcraftGameContext GameContext { get; }

        /// <summary>
        /// The currently filtered file types.
        /// </summary>
        private WarcraftFileType _filteredFileTypes;

        /// <summary>
        /// Whether or not to filter based on the filtered file types.
        /// </summary>
        private bool _isFiltered;

        /// <summary>
        /// Gets or sets a value indicating whether to show unknown file formats in the tree view.
        /// </summary>
        public bool ShouldDisplayUnknownFiles { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GamePage"/> class. The given package group and node tree are
        /// wrapped by the page.
        /// </summary>
        /// <param name="packageGroup">The package group which the node tree maps to.</param>
        /// <param name="nodeTree">The prebuilt node tree to display.</param>
        /// <param name="version">The Warcraft version that the game page is contextually relevant for.</param>
        /// <param name="alias">The name of the page.</param>
        public GamePage(PackageGroup packageGroup, SerializedTree nodeTree, WarcraftVersion version, string alias)
        {
            this.Alias = alias;
            _uiTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            _treeModel = new FileTreeModel(nodeTree);
            this.GameContext = new WarcraftGameContext(version, packageGroup, _treeModel);

            _treeAlignment = new Alignment(0.5f, 0.5f, 1.0f, 1.0f)
            {
                TopPadding = 1,
                BottomPadding = 1
            };

            _treeFilter = new TreeModelFilter(new TreeModelAdapter(_treeModel), null)
            {
                VisibleFunc = TreeModelVisibilityFunc
            };

            _treeSorter = new TreeModelSort(_treeFilter);

            _treeSorter.SetSortFunc(0, SortGameTreeRow);
            _treeSorter.SetSortColumnId(0, SortType.Descending);

            this.Tree = new TreeView(_treeSorter)
            {
                HeadersVisible = true,
                EnableTreeLines = true
            };

            var nodeIconRenderer = new CellRendererPixbuf
            {
                Xalign = 0.0f
            };
            var nodeNameRenderer = new CellRendererText
            {
                Xalign = 0.0f
            };

            var column = new TreeViewColumn
            {
                Title = "Data Files",
                Spacing = 4
            };
            column.PackStart(nodeIconRenderer, false);
            column.PackStart(nodeNameRenderer, false);

            column.SetCellDataFunc(nodeIconRenderer, RenderNodeIcon);
            column.SetCellDataFunc(nodeNameRenderer, RenderNodeName);

            this.Tree.AppendColumn(column);

            var sw = new ScrolledWindow
            {
                this.Tree
            };

            _treeAlignment.Add(sw);

            this.Tree.RowActivated += OnRowActivated;
            this.Tree.ButtonPressEvent += OnButtonPressed;
            this.Tree.Selection.Changed += OnSelectionChanged;

            _treeContextMenu = new Menu();

            // Save item context button
            _saveItem = new ImageMenuItem
            {
                UseStock = true,
                Label = Stock.Save,
                CanFocus = false,
                TooltipText = "Save the currently selected item to disk.",
                UseUnderline = true
            };
            _saveItem.Activated += OnSaveItem;
            _treeContextMenu.Add(_saveItem);

            // Export item context button
            _exportItem = new ImageMenuItem("Export")
            {
                Image = new Image(Stock.Convert, IconSize.Button),
                CanFocus = false,
                TooltipText = "Exports the currently selected item to another format.",
            };
            _exportItem.Activated += OnExportItemRequested;
            _treeContextMenu.Add(_exportItem);

            // Open item context button
            _openItem = new ImageMenuItem
            {
                UseStock = true,
                Label = Stock.Open,
                CanFocus = false,
                TooltipText = "Open the currently selected item.",
                UseUnderline = true
            };
            _openItem.Activated += OnOpenItem;
            _treeContextMenu.Add(_openItem);

            // Queue for export context button
            _queueForExportItem = new ImageMenuItem("Queue for export")
            {
                Image = new Image(Stock.Convert, IconSize.Button),
                CanFocus = false,
                TooltipText = "Queues the currently selected item for batch export.",
            };
            _queueForExportItem.Activated += OnQueueForExportRequested;
            _treeContextMenu.Add(_queueForExportItem);

            // Separator
            var separator = new SeparatorMenuItem();
            _treeContextMenu.Add(separator);

            // Copy path context button
            _copyPathItem = new ImageMenuItem("Copy path")
            {
                Image = new Image(Stock.Copy, IconSize.Button),
                CanFocus = false,
                TooltipText = "Copy the path of the currently selected item.",
            };
            _copyPathItem.Activated += OnCopyPath;
            _treeContextMenu.Add(_copyPathItem);

            _treeAlignment.ShowAll();
        }

        /// <summary>
        /// Sets the tree sensitivity, i.e, if the user can interact with it.
        /// </summary>
        /// <param name="sensitive">Whether or not the tree should be sensitive.</param>
        public void SetTreeSensitivity(bool sensitive)
        {
            this.Tree.Sensitive = sensitive;
        }

        /// <summary>
        /// Sets the filter of the tree view and asynchronously refilters it. The filter is a set of
        /// <see cref="WarcraftFileType"/> flags.
        /// </summary>
        /// <param name="filteredFileTypes">The file types to filter out.</param>
        public void SetFilter(WarcraftFileType filteredFileTypes)
        {
            _filteredFileTypes = filteredFileTypes;
        }

        /// <summary>
        /// Sets whether or not the tree is filtered.
        /// </summary>
        /// <param name="isFiltered">True if the tree should be filtered by the set file types, false otherwise.</param>
        public void SetFilterState(bool isFiltered)
        {
            _isFiltered = isFiltered;
        }

        /// <summary>
        /// Asynchronously refilters the node tree.
        /// </summary>
        /// <returns>A task which when finished signifies that the tree has been filtered.</returns>
        public Task RefilterAsync()
        {
            return Task.Factory.StartNew
            (
                _treeFilter.Refilter,
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.FromCurrentSynchronizationContext()
            );
        }

        /// <summary>
        /// This function is used internally by the tree filter to determine which rows are visible and which are not.
        /// </summary>
        /// <param name="model">The model of the tree.</param>
        /// <param name="iter">The iter to determine visibility for.</param>
        /// <returns>true if the iter should be visible; false otherwise.</returns>
        private bool TreeModelVisibilityFunc(ITreeModel model, TreeIter iter)
        {
            if (!_isFiltered)
            {
                return true;
            }

            var node = (SerializedNode)model.GetValue(iter, 0);
            var nodeTypes = node.FileType;

            if (!this.ShouldDisplayUnknownFiles)
            {
                // If the node is strictly an unknown file, don't display it.
                if (nodeTypes == WarcraftFileType.Unknown)
                {
                    return false;
                }
            }

            // If the file types of the node and the filtered types overlap in any way, then it should
            // be displayed.
            if ((nodeTypes & _filteredFileTypes) != 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles rendering of the icon of a node in the tree.
        /// </summary>
        /// <param name="column">The column which the icon is in.</param>
        /// <param name="cell">The cell which the reference is in.</param>
        /// <param name="model">The model of the treeview.</param>
        /// <param name="iter">The <see cref="TreeIter"/> pointing to the row the icon is in.</param>
        private void RenderNodeIcon(TreeViewColumn column, CellRenderer cell, ITreeModel model, TreeIter iter)
        {
            var cellIcon = cell as CellRendererPixbuf;
            var node = (SerializedNode)model.GetValue(iter, 0);

            if (node is null || cellIcon is null)
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

            if (node.Type.HasFlag(NodeType.Meta) && _treeModel.GetNodeName(node) == "Packages")
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
        /// <param name="column">The column which the cell is in.</param>
        /// <param name="cell">The cell which the name is in.</param>
        /// <param name="model">The model of the treeview.</param>
        /// <param name="iter">The <see cref="TreeIter"/> pointing to the row the name is in.</param>
        private void RenderNodeName(TreeViewColumn column, CellRenderer cell, ITreeModel model, TreeIter iter)
        {
            var cellText = cell as CellRendererText;
            var node = (SerializedNode)model.GetValue(iter, 0);

            if (node is null || cellText is null)
            {
                return;
            }

            cellText.Text = _treeModel.GetNodeName(node);

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
        /// <param name="sender">The sending object.</param>
        /// <param name="eventArgs">Arguments describing the row that was activated.</param>
        private void OnQueueForExportRequested(object? sender, EventArgs eventArgs)
        {
            var fileReference = GetSelectedReference();
            if (fileReference is null)
            {
                return;
            }

            RequestEnqueueFileExport(fileReference);
        }

        /// <summary>
        /// Handles passing off export requests for files in the tree.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="eventArgs">Arguments describing the row that was activated.</param>
        private void OnExportItemRequested(object? sender, EventArgs eventArgs)
        {
            var fileReference = GetSelectedReference();
            if (fileReference is null)
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
        /// <param name="sender">The sending object.</param>
        /// <param name="eventArgs">Arguments describing the row that was activated.</param>
        private void OnOpenItem(object? sender, EventArgs eventArgs)
        {
            var fileReference = GetSelectedReference();
            if (fileReference is null)
            {
                return;
            }

            if (fileReference.IsFile)
            {
                OpenReference(fileReference);
            }
            else
            {
                this.Tree.Selection.GetSelected(out var selectedIter);

                this.Tree.ExpandRow(_treeModel.GetPath(selectedIter), false);
            }
        }

        /// <summary>
        /// Handles copying of the file path of a selected item in the archive, triggered by a
        /// context menu press.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="eventArgs">Arguments describing the row that was activated.</param>
        private void OnCopyPath(object? sender, EventArgs eventArgs)
        {
            var fileReference = GetSelectedReference();
            if (fileReference is null)
            {
                return;
            }

            var clipboard = Clipboard.Get(Atom.Intern("CLIPBOARD", false));
            clipboard.Text = fileReference.FilePath;
        }

        /// <summary>
        /// TODO: Move elsewhere and add UI notification
        /// Handles extraction of files from the archive triggered by a context menu press.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="eventArgs">Arguments describing the row that was activated.</param>
        private void OnSaveItem(object? sender, EventArgs eventArgs)
        {
            var fileReference = GetSelectedReference();
            if (fileReference is null)
            {
                return;
            }

            var exportTargets = new List<FileReference>();
            if (fileReference.IsDirectory)
            {
                foreach (var subfile in _treeModel.EnumerateFilesOfReference(fileReference))
                {
                    exportTargets.Add(subfile);
                }
            }
            else
            {
                exportTargets.Add(fileReference);
            }

            RequestSave(exportTargets);
        }

        /// <summary>
        /// Handles spawning of the context menu.
        /// </summary>
        /// <param name="o">The sending object.</param>
        /// <param name="args">Arguments describing the row that was activated.</param>
        [ConnectBefore]
        private void OnButtonPressed(object o, ButtonPressEventArgs args)
        {
            if (args.Event.Type != EventType.ButtonPress || args.Event.Button != 3)
            {
                // Not a right-click, early return
                return;
            }

            this.Tree.GetPathAtPos((int)args.Event.X, (int)args.Event.Y, out var sorterPath);
            var filterPath = _treeSorter.ConvertPathToChildPath(sorterPath);
            var modelPath = _treeFilter.ConvertPathToChildPath(filterPath);

            if (modelPath is null)
            {
                _saveItem.Sensitive = false;
                _exportItem.Sensitive = false;
                _openItem.Sensitive = false;
                _queueForExportItem.Sensitive = false;
                _copyPathItem.Sensitive = false;
                return;
            }

            var currentFileReference = _treeModel.GetReferenceByPath(this.GameContext, modelPath);
            if (currentFileReference.IsFile || currentFileReference.IsDirectory)
            {
                _saveItem.Sensitive = true;
                _exportItem.Sensitive = true;
                _openItem.Sensitive = true;
                _queueForExportItem.Sensitive = true;
                _copyPathItem.Sensitive = true;
            }
            else
            {
                _saveItem.Sensitive = false;
                _exportItem.Sensitive = false;
                _openItem.Sensitive = true;
                _queueForExportItem.Sensitive = false;
                _copyPathItem.Sensitive = false;
            }

            _treeContextMenu.ShowAll();
            _treeContextMenu.PopupAtPointer(args.Event);
        }

        /// <summary>
        /// Handles notifying subscribers that the selection in the tree has changed, requesting file loading
        /// if it is a file.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="eventArgs">Arguments describing the row that was activated.</param>
        private void OnSelectionChanged(object? sender, EventArgs eventArgs)
        {
            var fileReference = GetSelectedReference();
            if (fileReference is null)
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
            var fileReference = GetSelectedReference();
            if (fileReference is null)
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
        /// <returns>The currently selected reference.</returns>
        private FileReference GetSelectedReference()
        {
            this.Tree.Selection.GetSelected(out var selectedIter);

            var filterIter = _treeSorter.ConvertIterToChildIter(selectedIter);
            var modeliter = _treeFilter.ConvertIterToChildIter(filterIter);

            return _treeModel.GetReferenceByIter(this.GameContext, modeliter);
        }

        /// <summary>
        /// Opens the given reference by extracting it, saving it to a temporary file, and handing it off
        /// to the operating system.
        /// </summary>
        /// <param name="fileReference">The reference to open.</param>
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
                    if (fileReference.TryExtract(out var fileData))
                    {
                        // create a temporary file and write the data to it.
                        var tempPath = Path.Combine(Path.GetTempPath(), fileReference.Filename);
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }

                        using
                        (
                            Stream tempStream = File.Open
                            (
                                tempPath,
                                FileMode.CreateNew,
                                FileAccess.ReadWrite,
                                FileShare.Read
                            )
                        )
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

            var nodeA = (SerializedNode)model.GetValue(a, 0);
            var nodeB = (SerializedNode)model.GetValue(b, 0);

            var typeofA = nodeA.Type;
            var typeofB = nodeB.Type;

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

            var nodeAName = _treeModel.GetNodeName(nodeA);

            var nodeBName = _treeModel.GetNodeName(nodeB);

            var result = string.CompareOrdinal(nodeAName, nodeBName);

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
        /// <param name="fileReference">The reference to request a load operation for.</param>
        private async void RequestFileLoad(FileReference fileReference)
        {
            if (this.FileLoadRequested is null)
            {
                return;
            }

            await Task.Factory.StartNew
            (
                async () => await this.FileLoadRequested.Invoke(this, fileReference),
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                _uiTaskScheduler
            );
        }

        /// <summary>
        /// Requests the saving of the specified list of <see cref="FileReference"/> objects in the main UI.
        /// </summary>
        /// <param name="fileReferences">The references to save.</param>
        private async void RequestSave(IEnumerable<FileReference> fileReferences)
        {
            if (this.SaveRequested is null)
            {
                return;
            }

            await Task.Factory.StartNew
            (
                async () => await this.SaveRequested.Invoke(this, fileReferences),
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                _uiTaskScheduler
            );
        }

        /// <summary>
        /// Requests the exporting of the specified <see cref="FileReference"/> in the main UI.
        /// </summary>
        /// <param name="fileReference">The reference to request an  export operation for.</param>
        private async void RequestFileExport(FileReference fileReference)
        {
            if (this.ExportItemRequested is null)
            {
                return;
            }

            await Task.Factory.StartNew
            (
                async () => await this.ExportItemRequested.Invoke(this, fileReference),
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                _uiTaskScheduler
            );
        }

        /// <summary>
        /// Requests the enqueueing for export of the specified <see cref="FileReference"/> in the main UI.
        /// </summary>
        /// <param name="fileReference">The reference to request an enqueued export operation for.</param>
        private async void RequestEnqueueFileExport(FileReference fileReference)
        {
            if (this.EnqueueFileExportRequested is null)
            {
                return;
            }

            await Task.Factory.StartNew
            (
                async () => await this.EnqueueFileExportRequested.Invoke(this, fileReference),
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                _uiTaskScheduler
            );
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _treeAlignment?.Dispose();
            this.GameContext.Assets?.Dispose();
            _treeModel?.Dispose();
            _treeSorter?.Dispose();
            _treeFilter?.Dispose();
            this.Tree?.Dispose();
        }
    }
}
