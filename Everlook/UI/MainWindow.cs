//
//  MainWindow.cs
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Everlook.Configuration;
using Everlook.Explorer;
using Everlook.Utility;
using Everlook.Viewport;
using Everlook.Viewport.Rendering;
using Gdk;
using GLib;
using Gtk;
using log4net;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Input;
using Warcraft.BLP;
using Warcraft.Core;
using Application = Gtk.Application;
using IOPath = System.IO.Path;

namespace Everlook.UI
{
	/// <summary>
	/// Main UI class for Everlook. The "partial" qualifier is not strictly needed, but prevents the compiler from
	/// generating errors about the autoconnected members that relate to UI elements.
	/// </summary>
	public sealed partial class MainWindow: Gtk.Window
	{
		/// <summary>
		/// Static reference to the configuration handler.
		/// </summary>
		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(MainWindow));

		/// <summary>
		/// Background viewport renderer. Handles all rendering in the viewport.
		/// </summary>
		private readonly ViewportRenderer RenderingEngine;

		/// <summary>
		/// Background file explorer tree builder. Handles enumeration of files in the archives.
		/// </summary>
		private readonly ExplorerBuilder FiletreeBuilder;

		/// <summary>
		/// Whether or not the program is shutting down. This is used to remove callbacks and events.
		/// </summary>
		private bool IsShuttingDown;

		/// <summary>
		/// Task scheduler for the UI thread. This allows task-based code to have very simple UI callbacks.
		/// </summary>
		private readonly TaskScheduler UIThreadScheduler;

		/// <summary>
		/// Creates an instance of the MainWindow class, loading the glade XML UI as needed.
		/// </summary>
		public static MainWindow Create()
		{
			Builder builder = new Builder(null, "Everlook.interfaces.Everlook.glade", null);
			return new MainWindow(builder, builder.GetObject("MainWindow").Handle);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MainWindow"/> class.
		/// </summary>
		/// <param name="builder">Builder.</param>
		/// <param name="handle">Handle.</param>
		private MainWindow(Builder builder, IntPtr handle)
			: base(handle)
		{
			builder.Autoconnect(this);
			DeleteEvent += OnDeleteEvent;

			this.UIThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();

			this.ViewportWidget = new GLWidget
			{
				CanFocus = true,
				SingleBuffer = false,
				ColorBPP = 24,
				DepthBPP = 24,
				AccumulatorBPP = 24,
				Samples = 4,
				GLVersionMajor = 3,
				GLVersionMinor = 3,
				GraphicsContextFlags = GraphicsContextFlags.Default
			};

			this.ViewportWidget.Events |=
            				EventMask.ButtonPressMask |
            				EventMask.ButtonReleaseMask |
            				EventMask.KeyPressMask |
            				EventMask.KeyReleaseMask;

			this.ViewportWidget.Initialized += delegate
			{
				// Initialize all OpenGL rendering parameters
				this.RenderingEngine.Initialize();
				Idle.Add(OnIdleRenderFrame);
			};

			this.ViewportWidget.ButtonPressEvent += OnViewportButtonPressed;
			this.ViewportWidget.ButtonReleaseEvent += OnViewportButtonReleased;
			this.ViewportWidget.ConfigureEvent += OnViewportConfigured;

			this.RenderingEngine = new ViewportRenderer(this.ViewportWidget);
			this.ViewportAlignment.Add(this.ViewportWidget);
			this.ViewportAlignment.ShowAll();

			// Add a staggered idle handler for adding enumerated items to the interface
			//Timeout.Add(1, OnIdle, Priority.DefaultIdle);
			Idle.Add(OnIdle, Priority.DefaultIdle);

			this.AboutButton.Clicked += OnAboutButtonClicked;
			this.PreferencesButton.Clicked += OnPreferencesButtonClicked;

			this.GameExplorerTreeView.RowExpanded += OnGameExplorerRowExpanded;
			this.GameExplorerTreeView.RowActivated += OnGameExplorerRowActivated;
			this.GameExplorerTreeView.Selection.Changed += OnGameExplorerSelectionChanged;
			this.GameExplorerTreeView.ButtonPressEvent += OnGameExplorerButtonPressed;

			this.GameExplorerTreeSorter.SetSortFunc(1, SortGameExplorerRow);
			this.GameExplorerTreeSorter.SetSortColumnId(1, SortType.Descending);

			this.ExportQueueTreeView.ButtonPressEvent += OnExportQueueButtonPressed;

			this.ExtractItem.Activated += OnExtractContextItemActivated;
			this.ExportItem.Activated += OnExportItemContextItemActivated;
			this.OpenItem.Activated += OnOpenContextItemActivated;
			this.CopyItem.Activated += OnCopyContextItemActivated;
			this.QueueItem.Activated += OnQueueContextItemActivated;

			this.RemoveQueueItem.Activated += OnQueueRemoveContextItemActivated;

			this.FiletreeBuilder = new ExplorerBuilder
			(
				new ExplorerStore
				(
					this.GameExplorerTreeStore,
					this.GameExplorerTreeFilter,
					this.GameExplorerTreeSorter
				)
			);
			this.FiletreeBuilder.PackageGroupAdded += OnPackageGroupAdded;
			this.FiletreeBuilder.PackageEnumerated += OnPackageEnumerated;
			this.FiletreeBuilder.Start();

			/*
				Set up item control sections to default states
			*/

			foreach (ControlPage otherPage in Enum.GetValues(typeof(ControlPage)))
			{
				DisableControlPage(otherPage);
			}
		}

		/// <summary>
		/// Enables the specified control page and brings it to the front.
		/// </summary>
		/// <param name="pageToEnable">pageToEnable.</param>
		private void EnableControlPage(ControlPage pageToEnable)
		{
			if (Enum.IsDefined(typeof(ControlPage), pageToEnable))
			{
				// Set the page
				this.ItemControlNotebook.Page = (int)pageToEnable;

				// Disable the other pages
				foreach (ControlPage otherPage in Enum.GetValues(typeof(ControlPage)))
				{
					if (otherPage == pageToEnable)
					{
						continue;
					}

					DisableControlPage(otherPage);
				}


				switch (pageToEnable)
				{
					case ControlPage.Image:
					{
						this.RenderAlphaCheckButton.Sensitive = true;
						this.RenderRedCheckButton.Sensitive = true;
						this.RenderGreenCheckButton.Sensitive = true;
						this.RenderBlueCheckButton.Sensitive = true;
						break;
					}
					case ControlPage.Model:
					{
						break;
					}
					case ControlPage.Animation:
					{
						break;
					}
					case ControlPage.Audio:
					{
						break;
					}
				}
			}
		}

		/// <summary>
		/// Disables the specified control page.
		/// </summary>
		/// <param name="pageToDisable">pageToEnable.</param>
		private void DisableControlPage(ControlPage pageToDisable)
		{
			if (Enum.IsDefined(typeof(ControlPage), pageToDisable))
			{
				switch (pageToDisable)
				{
					case ControlPage.Image:
					{
						this.RenderAlphaCheckButton.Sensitive = false;
						this.RenderRedCheckButton.Sensitive = false;
						this.RenderGreenCheckButton.Sensitive = false;
						this.RenderBlueCheckButton.Sensitive = false;
						break;
					}
					case ControlPage.Model:
					{
						break;
					}
					case ControlPage.Animation:
					{
						break;
					}
					case ControlPage.Audio:
					{
						break;
					}
				}
			}
		}

		/// <summary>
		/// Sorts the game explorer row. If <paramref name="iterA"/> should be sorted before
		/// <paramref name="iterB"/>
		/// </summary>
		/// <returns>The sorting priority of the row. This value can be -1, 0 or 1 if
		/// A sorts before B, A sorts with B or A sorts after B, respectively.</returns>
		/// <param name="model">Model.</param>
		/// <param name="iterA">Iter a.</param>
		/// <param name="iterB">Iter b.</param>
		private static int SortGameExplorerRow(ITreeModel model, TreeIter iterA, TreeIter iterB)
		{
			const int sortABeforeB = -1;
			const int sortAWithB = 0;
			const int sortAAfterB = 1;

			NodeType typeofA = (NodeType)model.GetValue(iterA, 4);
			NodeType typeofB = (NodeType)model.GetValue(iterB, 4);

			if (typeofA < typeofB)
			{
				return sortAAfterB;
			}
			if (typeofA > typeofB)
			{
				return sortABeforeB;
			}

			string aComparisonString = (string)model.GetValue(iterA, 1);

			string bComparisonString = (string)model.GetValue(iterB, 1);

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
		/// Gets the current <see cref="FileReference"/> that is selected in the tree view.
		/// </summary>
		/// <returns></returns>
		private FileReference GetSelectedReference()
		{
			TreeIter selectedIter;
			this.GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			return this.FiletreeBuilder.NodeStorage.GetItemReferenceFromIter(selectedIter);
		}

		/// <summary>
		/// Reloads visible runtime values that the user can change in the preferences, such as the colour
		/// of the viewport or the loaded packages.
		/// </summary>
		private void ReloadRuntimeValues()
		{
			this.ViewportWidget.OverrideBackgroundColor(StateFlags.Normal, this.Config.GetViewportBackgroundColour());

			if (this.FiletreeBuilder.HasPackageDirectoryChanged())
			{
				this.GameExplorerTreeStore.Clear();
				this.FiletreeBuilder.Reload();
			}
		}

		private void BeginLoadingWorldModel(FileReference fileReference)
		{
			this.StatusSpinner.Active = true;

			string modelName = IOPath.GetFileNameWithoutExtension(fileReference.FilePath);
			uint modelStatusMessageContextID = this.MainStatusBar.GetContextId($"worldModelLoad_{modelName}");
			uint modelStatusMessageID = this.MainStatusBar.Push(modelStatusMessageContextID,
				$"Loading world model \"{modelName}\"...");

			Task.Factory.StartNew(() => ModelLoadingRoutines.LoadWorldModel(fileReference))
				.ContinueWith(modelLoadTask => ModelLoadingRoutines.CreateRenderableWorldModel(modelLoadTask.Result, fileReference.PackageGroup), this.UIThreadScheduler)
				.ContinueWith(createRenderableTask => this.RenderingEngine.SetRenderTarget(createRenderableTask.Result), this.UIThreadScheduler)
				.ContinueWith(result =>
					{
						this.StatusSpinner.Active = false;
						this.MainStatusBar.Remove(modelStatusMessageContextID, modelStatusMessageID);
						EnableControlPage(ControlPage.Model);
					},
					this.UIThreadScheduler);
		}

		private void BeginLoadingWorldModelGroup(FileReference fileReference)
		{
			this.StatusSpinner.Active = true;

			string modelName = IOPath.GetFileNameWithoutExtension(fileReference.FilePath);
			uint modelStatusMessageContextID = this.MainStatusBar.GetContextId($"worldModelGroupLoad_{modelName}");
			uint modelStatusMessageID = this.MainStatusBar.Push(modelStatusMessageContextID,
				$"Loading world model group \"{modelName}\"...");

			Task.Factory.StartNew(() => ModelLoadingRoutines.LoadWorldModelGroup(fileReference))
				.ContinueWith(modelLoadTask => ModelLoadingRoutines.CreateRenderableWorldModel(modelLoadTask.Result, fileReference.PackageGroup), this.UIThreadScheduler)
				.ContinueWith(createRenderableTask => this.RenderingEngine.SetRenderTarget(createRenderableTask.Result), this.UIThreadScheduler)
				.ContinueWith(result =>
					{
						this.StatusSpinner.Active = false;
						this.MainStatusBar.Remove(modelStatusMessageContextID, modelStatusMessageID);
						EnableControlPage(ControlPage.Model);
					},
					this.UIThreadScheduler);
		}

		/// <summary>
		/// Handles input inside the OpenGL viewport for mouse button presses.
		/// This function grabs focus for the viewport, and hides the mouse
		/// cursor during movement.
		/// </summary>
		[ConnectBefore]
		private void OnViewportButtonPressed(object o, ButtonPressEventArgs args)
		{
			if (this.RenderingEngine.IsMovementDisabled())
			{
				return;
			}

			// Right click is pressed
			if (args.Event.Type == EventType.ButtonPress && args.Event.Button == 3)
			{
				// Hide the mouse pointer
				this.Window.Cursor = new Cursor(CursorType.BlankCursor);

				this.ViewportWidget.GrabFocus();

				this.RenderingEngine.WantsToMove = true;
				this.RenderingEngine.InitialMouseX = Mouse.GetCursorState().X;
				this.RenderingEngine.InitialMouseY = Mouse.GetCursorState().Y;
			}
		}

		/// <summary>
		/// Handles input inside the OpenGL viewport for mouse button releases.
		/// This function restores input focus to the main UI and returns the
		/// cursor to its original appearance.
		/// </summary>
		[ConnectBefore]
		private void OnViewportButtonReleased(object o, ButtonReleaseEventArgs args)
		{
			// Right click is released
			if (args.Event.Type == EventType.ButtonRelease && args.Event.Button == 3)
			{
				// Return the mouse pointer to its original appearance
				this.Window.Cursor = new Cursor(CursorType.Arrow);
				GrabFocus();
				this.RenderingEngine.WantsToMove = false;
			}
		}

		/// <summary>
		/// This function lazily renders frames of the currently focused object.
		/// All rendering functionality is either in the viewport renderer, or in a
		/// renderable object currently hosted by it.
		/// </summary>
		private bool OnIdleRenderFrame()
		{
			const bool keepCalling = true;
			const bool stopCalling = false;

			if (this.IsShuttingDown)
			{
				return stopCalling;
			}

			if (!this.RenderingEngine.IsInitialized)
			{
				return stopCalling;
			}

			if (this.RenderingEngine.HasRenderTarget || this.viewportHasPendingRedraw)
			{
				this.RenderingEngine.RenderFrame();
				this.viewportHasPendingRedraw = false;
			}

			return keepCalling;
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="o">The sending object.</param>
		/// <param name="args">The configuration arguments.</param>
		private void OnViewportConfigured(object o, ConfigureEventArgs args)
		{
			this.viewportHasPendingRedraw = true;
		}

		/// <summary>
		/// Idle functionality. This code is called as a way of lazily loading rows into the UI
		/// without causing lockups due to sheer data volume.
		/// </summary>
		private bool OnIdle()
		{
			const bool keepCalling = true;
			const bool stopCalling = false;

			if (this.IsShuttingDown)
			{
				return stopCalling;
			}

			if (this.FiletreeBuilder.EnumeratedReferences.Count > 0)
			{
				// There's content to be added to the UI
				// Get the last reference in the list.
				FileReference newContent = this.FiletreeBuilder.EnumeratedReferences.Last();

				if (newContent == null)
				{
					this.FiletreeBuilder.EnumeratedReferences.RemoveAt(this.FiletreeBuilder.EnumeratedReferences.Count - 1);
					return keepCalling;
				}

				if (newContent.IsFile)
				{
					this.FiletreeBuilder.NodeStorage.AddFileNode(newContent);
				}
				else if (newContent.IsDirectory)
				{
					TreePath pathToParent = this.FiletreeBuilder.NodeStorage.GetPath(newContent.ParentReference.ReferenceIter);
					bool isParentExpanded = this.GameExplorerTreeView.GetRowExpanded(pathToParent);
					if (isParentExpanded && newContent.State == ReferenceState.NotEnumerated)
					{
						// This references was added to the UI after the user had opened the previous folder.
						// Therefore, it should be submitted back to the UI for enumeration.
						this.FiletreeBuilder.SubmitWork(newContent);
					}

					this.FiletreeBuilder.NodeStorage.AddDirectoryNode(newContent);
				}

				this.FiletreeBuilder.EnumeratedReferences.Remove(newContent);
			}

			return keepCalling;
		}

		/// <summary>
		/// Handles the export item context item activated event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnExportItemContextItemActivated(object sender, EventArgs e)
		{
			FileReference fileReference = GetSelectedReference();
			if (!string.IsNullOrEmpty(fileReference?.FilePath))
			{

				WarcraftFileType fileType = fileReference.GetReferencedFileType();
				switch (fileType)
				{
					case WarcraftFileType.Directory:
					{
						if (fileReference.IsFullyEnumerated)
						{
							using (EverlookDirectoryExportDialog exportDialog = EverlookDirectoryExportDialog.Create(fileReference))
							{
								if (exportDialog.Run() == (int)ResponseType.Ok)
								{
									exportDialog.RunExport();
								}
								exportDialog.Destroy();
							}
						}
						else
						{
							// TODO: Implement wait message when the directory and its subdirectories have not yet been enumerated.
						}
						break;
					}
					case WarcraftFileType.BinaryImage:
					{
						using (EverlookImageExportDialog exportDialog = EverlookImageExportDialog.Create(fileReference))
						{
							if (exportDialog.Run() == (int)ResponseType.Ok)
							{
								exportDialog.RunExport();
							}
							exportDialog.Destroy();
						}
						break;
					}
				}
			}
		}

		/// <summary>
		/// Handles extraction of files from the archive triggered by a context menu press.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnExtractContextItemActivated(object sender, EventArgs e)
		{
			FileReference fileReference = GetSelectedReference();

			string cleanFilepath = fileReference.FilePath.ConvertPathSeparatorsToCurrentNativeSeparator();
			string exportpath;
			if (this.Config.GetShouldKeepFileDirectoryStructure())
			{
				exportpath = this.Config.GetDefaultExportDirectory() + cleanFilepath;
				Directory.CreateDirectory(Directory.GetParent(exportpath).FullName);
			}
			else
			{
				string filename = IOPath.GetFileName(cleanFilepath);
				exportpath = this.Config.GetDefaultExportDirectory() + filename;
			}

			byte[] file = fileReference.Extract();
			if (file != null)
			{
				File.WriteAllBytes(exportpath, file);
			}
		}

		/// <summary>
		/// Handles opening of files from the archive triggered by a context menu press.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnOpenContextItemActivated(object sender, EventArgs e)
		{
			TreeIter selectedIter;
			this.GameExplorerTreeView.Selection.GetSelected(out selectedIter);
			FileReference fileReference = GetSelectedReference();
			if (!fileReference.IsFile)
			{
				this.GameExplorerTreeView.ExpandRow(this.GameExplorerTreeSorter.GetPath(selectedIter), false);
			}
		}

		/// <summary>
		/// Handles copying of the file path of a selected item in the archive, triggered by a
		/// context menu press.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnCopyContextItemActivated(object sender, EventArgs e)
		{
			Clipboard clipboard = Clipboard.Get(Atom.Intern("CLIPBOARD", false));

			TreeIter selectedIter;
			this.GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			clipboard.Text = this.FiletreeBuilder.NodeStorage.GetItemReferenceFromIter(selectedIter).FilePath;
		}

		/// <summary>
		/// Handles queueing of a selected file in the archive, triggered by a context
		/// menu press.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnQueueContextItemActivated(object sender, EventArgs e)
		{
			FileReference fileReference = GetSelectedReference();

			string cleanFilepath = fileReference.FilePath.ConvertPathSeparatorsToCurrentNativeSeparator();

			if (string.IsNullOrEmpty(cleanFilepath))
			{
				cleanFilepath = fileReference.PackageName;
			}
			else if (string.IsNullOrEmpty(IOPath.GetFileName(cleanFilepath)))
			{
				cleanFilepath = Directory.GetParent(cleanFilepath).FullName.Replace(Directory.GetCurrentDirectory(), "");
			}

			this.ExportQueueListStore.AppendValues(cleanFilepath, cleanFilepath, "Queued");
		}

		/// <summary>
		/// Displays the About dialog to the user.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnAboutButtonClicked(object sender, EventArgs e)
		{
			this.AboutDialog.Run();
			this.AboutDialog.Hide();
		}

		/// <summary>
		/// Displays the preferences dialog to the user.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnPreferencesButtonClicked(object sender, EventArgs e)
		{
			using (EverlookPreferences preferencesDialog = EverlookPreferences.Create())
			{
				preferencesDialog.TransientFor = this;
				if (preferencesDialog.Run() == (int)ResponseType.Ok)
				{
					preferencesDialog.SavePreferences();
					ReloadRuntimeValues();
				}

				preferencesDialog.Destroy();
			}
		}

		/// <summary>
		/// Handles expansion of rows in the game explorer, enumerating any subfolders and
		/// files present under that row.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnGameExplorerRowExpanded(object sender, RowExpandedArgs e)
		{
			// Whenever a row is expanded, enumerate the subfolders of that row.
			FileReference parentReference = this.FiletreeBuilder.NodeStorage.GetItemReferenceFromPath(e.Path);
			foreach (FileReference childReference in parentReference.ChildReferences)
			{
				if (childReference.IsDirectory && childReference.State != ReferenceState.Enumerated)
				{
					this.FiletreeBuilder.SubmitWork(childReference);
				}
			}
		}

		/// <summary>
		/// Handles double-clicking on files in the explorer.
		/// </summary>
		/// <param name="o">The sending object.</param>
		/// <param name="args">Arguments describing the row that was activated.</param>
		private void OnGameExplorerRowActivated(object o, RowActivatedArgs args)
		{
			TreeIter selectedIter;
			this.GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			FileReference fileReference = this.FiletreeBuilder.NodeStorage.GetItemReferenceFromIter(selectedIter);
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
							string tempPath = IOPath.GetTempPath() + fileReference.Filename;
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
				this.GameExplorerTreeView.ExpandRow(this.FiletreeBuilder.NodeStorage.GetPath(selectedIter), false);
			}
		}

		/// <summary>
		/// Handles selection of files in the game explorer, displaying them to the user and routing
		/// whatever rendering functionality the file needs to the viewport.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnGameExplorerSelectionChanged(object sender, EventArgs e)
		{
			TreeIter selectedIter;
			this.GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			FileReference fileReference = this.FiletreeBuilder.NodeStorage.GetItemReferenceFromIter(selectedIter);
			if (fileReference != null && fileReference.IsFile)
			{
				if (string.IsNullOrEmpty(fileReference.FilePath))
				{
					return;
				}

				switch (fileReference.GetReferencedFileType())
				{
					case WarcraftFileType.BinaryImage:
					{
						byte[] fileData = fileReference.Extract();
						if (fileData != null)
						{
							try
							{
								BLP image = new BLP(fileData);
								RenderableBLP renderableImage = new RenderableBLP(image, fileReference.FilePath);

								this.RenderingEngine.SetRenderTarget(renderableImage);
								EnableControlPage(ControlPage.Image);
							}
							catch (FileLoadException fex)
							{
								Log.Warn($"FileLoadException when opening BLP image: {fex.Message}\n" +
								         $"Please report this on GitHub or via email.");
							}
						}

						break;
					}
					case WarcraftFileType.WorldObjectModel:
					{
						BeginLoadingWorldModel(fileReference);
						break;
					}
					case WarcraftFileType.WorldObjectModelGroup:
					{
						BeginLoadingWorldModelGroup(fileReference);
						break;
					}
					case WarcraftFileType.GIFImage:
					case WarcraftFileType.PNGImage:
					case WarcraftFileType.JPGImage:
					{
						byte[] fileData = fileReference.Extract();
						if (fileData != null)
						{
							using (MemoryStream ms = new MemoryStream(fileData))
							{
								RenderableBitmap renderableImage = new RenderableBitmap(new Bitmap(ms), fileReference.FilePath);
								this.RenderingEngine.SetRenderTarget(renderableImage);
							}

							EnableControlPage(ControlPage.Image);
						}
						break;
					}
				}
			}
		}

		/// <summary>
		/// Handles context menu spawning for the game explorer.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		[ConnectBefore]
		private void OnGameExplorerButtonPressed(object sender, ButtonPressEventArgs e)
		{
			TreePath path;
			this.GameExplorerTreeView.GetPathAtPos((int)e.Event.X, (int)e.Event.Y, out path);

			FileReference currentFileReference = null;
			if (path != null)
			{
				currentFileReference = this.FiletreeBuilder.NodeStorage.GetItemReferenceFromPath(path);
			}

			if (e.Event.Type == EventType.ButtonPress && e.Event.Button == 3)
			{
				if (string.IsNullOrEmpty(currentFileReference?.FilePath))
				{
					this.ExtractItem.Sensitive = false;
					this.ExportItem.Sensitive = false;
					this.OpenItem.Sensitive = false;
					this.QueueItem.Sensitive = false;
					this.CopyItem.Sensitive = false;
				}
				else
				{
					if (!currentFileReference.IsFile)
					{
						this.ExtractItem.Sensitive = false;
						this.ExportItem.Sensitive = true;
						this.OpenItem.Sensitive = true;
						this.QueueItem.Sensitive = true;
						this.CopyItem.Sensitive = true;
					}
					else
					{
						this.ExtractItem.Sensitive = true;
						this.ExportItem.Sensitive = true;
						this.OpenItem.Sensitive = true;
						this.QueueItem.Sensitive = true;
						this.CopyItem.Sensitive = true;
					}
				}


				this.FileContextMenu.ShowAll();
				this.FileContextMenu.Popup();
			}
		}

		/// <summary>
		/// Handles context menu spawning for the export queue.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		[ConnectBefore]
		private void OnExportQueueButtonPressed(object sender, ButtonPressEventArgs e)
		{
			TreePath path;
			this.ExportQueueTreeView.GetPathAtPos((int)e.Event.X, (int)e.Event.Y, out path);

			FileReference currentReference = null;
			if (path != null)
			{
				TreeIter iter;
				this.ExportQueueListStore.GetIterFromString(out iter, path.ToString());
				currentReference = this.FiletreeBuilder.NodeStorage.GetItemReferenceFromIter(iter);
			}

			if (e.Event.Type == EventType.ButtonPress && e.Event.Button == 3)
			{
				if (string.IsNullOrEmpty(currentReference?.FilePath))
				{
					this.RemoveQueueItem.Sensitive = false;
				}
				else
				{
					this.RemoveQueueItem.Sensitive = true;
				}

				this.QueueContextMenu.ShowAll();
				this.QueueContextMenu.Popup();
			}
		}

		/// <summary>
		/// Handles removal of items from the export queue, triggered by a context menu press.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnQueueRemoveContextItemActivated(object sender, EventArgs e)
		{
			TreeIter selectedIter;
			this.ExportQueueTreeView.Selection.GetSelected(out selectedIter);

			this.ExportQueueListStore.Remove(ref selectedIter);
		}

		/// <summary>
		/// Handles the package group added event from the explorer builder.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnPackageGroupAdded(object sender, ReferenceEnumeratedEventArgs e)
		{
			Application.Invoke(delegate
			{
				this.FiletreeBuilder.NodeStorage.AddPackageGroupNode(e.Reference);
			});
		}

		/// <summary>
		/// Handles the package enumerated event from the explorer builder.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnPackageEnumerated(object sender, ReferenceEnumeratedEventArgs e)
		{
			Application.Invoke(delegate
			{
				this.FiletreeBuilder.NodeStorage.AddPackageNode(e.Reference);
			});
		}

		/// <summary>
		/// Handles application shutdown procedures - terminating render threads, cleaning
		/// up the UI, etc.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="a">The alpha component.</param>
		private void OnDeleteEvent(object sender, DeleteEventArgs a)
		{
			this.IsShuttingDown = true;

			if (this.FiletreeBuilder.IsActive)
			{
				this.FiletreeBuilder.Stop();
				this.FiletreeBuilder.Dispose();
			}

			this.RenderingEngine.SetRenderTarget(null);
			this.RenderingEngine.Dispose();

			this.ViewportWidget.Destroy();

			Application.Quit();
			a.RetVal = true;
		}
	}
}