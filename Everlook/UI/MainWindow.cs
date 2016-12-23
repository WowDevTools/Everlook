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
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Input;
using Warcraft.BLP;
using Warcraft.Core;
using Application = Gtk.Application;
using ExtensionMethods = Everlook.Utility.ExtensionMethods;
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
		/// Background viewport renderer. Handles all rendering in the viewport.
		/// </summary>
		private readonly ViewportRenderer viewportRenderer;

		/// <summary>
		/// Background file explorer tree builder. Handles enumeration of files in the archives.
		/// </summary>
		private readonly ExplorerBuilder explorerBuilder = new ExplorerBuilder();

		/// <summary>
		/// Whether or not the program is shutting down. This is used to remove callbacks and events.
		/// </summary>
		private bool shuttingDown;

		/// <summary>
		/// Task scheduler for the UI thread. This allows task-based code to have very simple UI callbacks.
		/// </summary>
		private TaskScheduler UIThreadScheduler;

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

			this.ViewportWidget = new GLWidget(GraphicsMode.Default)
			{
				CanFocus = true,
				SingleBuffer = false,
				ColorBPP = 24,
				DepthBPP = 32,
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

			this.ViewportWidget.Initialized += OnViewportInitialized;
			this.ViewportWidget.ButtonPressEvent += OnViewportButtonPressed;
			this.ViewportWidget.ButtonReleaseEvent += OnViewportButtonReleased;
			this.ViewportWidget.ConfigureEvent += OnViewportConfigured;

			this.ViewportAlignment.Add(this.ViewportWidget);
			this.ViewportAlignment.ShowAll();

			this.viewportRenderer = new ViewportRenderer(this.ViewportWidget);

			// Add a staggered idle handler for adding enumerated items to the interface
			Timeout.Add(10, OnGLibLoopIdle, Priority.DefaultIdle);

			this.AboutButton.Clicked += OnAboutButtonClicked;
			this.PreferencesButton.Clicked += OnPreferencesButtonClicked;

			this.GameExplorerTreeView.RowExpanded += OnGameExplorerRowExpanded;
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

			this.explorerBuilder.PackageGroupAdded += OnPackageGroupAdded;
			this.explorerBuilder.PackageEnumerated += OnPackageEnumerated;
			this.explorerBuilder.Start(); // TODO: This is a performance hog (and the whole damn thing should be rewritten)

			/*
				Set up item control sections
			*/

			// Image
			this.RenderAlphaCheckButton.Sensitive = false;
			this.RenderRedCheckButton.Sensitive = false;
			this.RenderGreenCheckButton.Sensitive = false;
			this.RenderBlueCheckButton.Sensitive = false;

			// Model

			// Animation

			// Audio
		}

		/// <summary>
		/// Handles input inside the OpenGL viewport for mouse button presses.
		/// This function grabs focus for the viewport, and hides the mouse
		/// cursor during movement.
		/// </summary>
		[ConnectBefore]
		private void OnViewportButtonPressed(object o, ButtonPressEventArgs args)
		{
			if (this.viewportRenderer.IsMovementDisabled())
			{
				return;
			}

			// Right click is pressed
			if (args.Event.Type == EventType.ButtonPress && args.Event.Button == 3)
			{
				// Hide the mouse pointer
				this.Window.Cursor = new Cursor(CursorType.BlankCursor);

				this.ViewportWidget.GrabFocus();

				this.viewportRenderer.WantsToMove = true;
				this.viewportRenderer.InitialMouseX = Mouse.GetCursorState().X;
				this.viewportRenderer.InitialMouseY = Mouse.GetCursorState().Y;
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
				this.viewportRenderer.WantsToMove = false;
			}
		}

		/// <summary>
		/// Handles OpenGL initialization post-context creation. This function
		/// passes the main OpenGL initialization to the viewport renderer, and
		/// adds an idle function for rendering.
		/// </summary>
		private void OnViewportInitialized(object sender, EventArgs e)
		{
			// Initialize all OpenGL rendering parameters
			this.viewportRenderer.Initialize();
			Idle.Add(OnIdleRenderFrame);
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

			if (this.shuttingDown)
			{
				return stopCalling;
			}

			if (!this.viewportRenderer.IsInitialized)
			{
				return stopCalling;
			}

			if (this.viewportRenderer.HasRenderTarget || this.viewportHasPendingRedraw)
			{
				this.viewportRenderer.RenderFrame();
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


				if (pageToEnable == ControlPage.Image)
				{
					this.RenderAlphaCheckButton.Sensitive = true;
					this.RenderRedCheckButton.Sensitive = true;
					this.RenderGreenCheckButton.Sensitive = true;
					this.RenderBlueCheckButton.Sensitive = true;
				}
				else if (pageToEnable == ControlPage.Model)
				{

				}
				else if (pageToEnable == ControlPage.Animation)
				{

				}
				else if (pageToEnable == ControlPage.Audio)
				{

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
				if (pageToDisable == ControlPage.Image)
				{
					this.RenderAlphaCheckButton.Sensitive = false;
					this.RenderRedCheckButton.Sensitive = false;
					this.RenderGreenCheckButton.Sensitive = false;
					this.RenderBlueCheckButton.Sensitive = false;
				}
				else if (pageToDisable == ControlPage.Model)
				{

				}
				else if (pageToDisable == ControlPage.Animation)
				{

				}
				else if (pageToDisable == ControlPage.Audio)
				{

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

			int result = String.CompareOrdinal(aComparisonString, bComparisonString);

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
		/// Idle functionality. This code is called as a way of lazily loading rows into the UI
		/// without causing lockups due to sheer data volume.
		/// </summary>
		private bool OnGLibLoopIdle()
		{
			const bool keepCalling = true;
			const bool stopCalling = false;

			if (this.shuttingDown)
			{
				return stopCalling;
			}

			if (this.explorerBuilder.EnumeratedReferences.Count > 0)
			{
				// There's content to be added to the UI
				// Get the last reference in the list.
				FileReference newContent = this.explorerBuilder.EnumeratedReferences[this.explorerBuilder.EnumeratedReferences.Count - 1];

				if (newContent == null)
				{
					this.explorerBuilder.EnumeratedReferences.RemoveAt(this.explorerBuilder.EnumeratedReferences.Count - 1);
					return keepCalling;
				}

				if (newContent.IsFile)
				{
					AddFileNode(newContent.ParentReference, newContent);
				}
				else if (newContent.IsDirectory)
				{
					AddDirectoryNode(newContent.ParentReference, newContent);
				}

				this.explorerBuilder.EnumeratedReferences.Remove(newContent);
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
			if (fileReference != null && !string.IsNullOrEmpty(fileReference.ItemPath))
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
					default:
					{
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

			string cleanFilepath = fileReference.ItemPath.ConvertPathSeparatorsToCurrentNativeSeparator();
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

			clipboard.Text = GetItemReferenceFromStoreIter(GetStoreIterFromSorterIter(selectedIter)).ItemPath;
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

			string cleanFilepath = fileReference.ItemPath.ConvertPathSeparatorsToCurrentNativeSeparator();

			if (String.IsNullOrEmpty(cleanFilepath))
			{
				cleanFilepath = fileReference.PackageName;
			}
			else if (String.IsNullOrEmpty(IOPath.GetFileName(cleanFilepath)))
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
				if (preferencesDialog.Run() == (int)ResponseType.Ok)
				{
					preferencesDialog.SavePreferences();
					ReloadRuntimeValues();
				}

				preferencesDialog.Destroy();
			}
		}

		/// <summary>
		/// Gets the current <see cref="FileReference"/> that is selected in the tree view.
		/// </summary>
		/// <returns></returns>
		private FileReference GetSelectedReference()
		{
			TreeIter selectedIter;
			this.GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			return GetItemReferenceFromStoreIter(GetStoreIterFromSorterIter(selectedIter));
		}

		/// <summary>
		/// Reloads visible runtime values that the user can change in the preferences, such as the colour
		/// of the viewport or the loaded packages.
		/// </summary>
		private void ReloadRuntimeValues()
		{
			this.ViewportWidget.OverrideBackgroundColor(StateFlags.Normal, this.Config.GetViewportBackgroundColour());

			if (this.explorerBuilder.HasPackageDirectoryChanged())
			{
				this.GameExplorerTreeStore.Clear();
				this.explorerBuilder.Reload();
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
			FileReference parentReference = GetItemReferenceFromStoreIter(GetStoreIterFromVisiblePath(e.Path));
			foreach (FileReference childReference in parentReference.ChildReferences)
			{
				if (childReference.IsDirectory && childReference.State != ReferenceState.Enumerated)
				{
					this.explorerBuilder.SubmitWork(childReference);
				}
			}
		}

		/// <summary>
		/// Gets a <see cref="TreeIter"/> that's valid for the <see cref="GameExplorerTreeStore"/> from a
		/// <see cref="TreePath"/> visible to the user in the UI.
		/// </summary>
		/// <param name="path">The TreePath.</param>
		/// <returns>A <see cref="TreeIter"/>.</returns>
		private TreeIter GetStoreIterFromVisiblePath(TreePath path)
		{
			TreeIter sorterIter;
			this.GameExplorerTreeSorter.GetIter(out sorterIter, path);
			return GetStoreIterFromSorterIter(sorterIter);
		}

		/// <summary>
		/// Gets a <see cref="TreeIter"/> that's valid for the <see cref="GameExplorerTreeStore"/> from a TreeIter
		/// valid for the <see cref="GameExplorerTreeSorter"/>.
		/// </summary>
		/// <param name="sorterIter">The GameExplorerTreeSorter iter.</param>
		/// <returns>A <see cref="TreeIter"/>.</returns>
		private TreeIter GetStoreIterFromSorterIter(TreeIter sorterIter)
		{
			TreeIter filterIter = this.GameExplorerTreeSorter.ConvertIterToChildIter(sorterIter);
			return GetStoreIterFromFilterIter(filterIter);
		}

		/// <summary>
		/// Gets a <see cref="TreeIter"/> that's valid for the <see cref="GameExplorerTreeStore"/> from a TreeIter
		/// valid for the <see cref="GameExplorerTreeFilter"/>.
		/// </summary>
		/// <param name="filterIter">The GameExplorerTreeFilter iter.</param>
		/// <returns>A <see cref="TreeIter"/>.</returns>
		private TreeIter GetStoreIterFromFilterIter(TreeIter filterIter)
		{
			return this.GameExplorerTreeFilter.ConvertIterToChildIter(filterIter);
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

			FileReference fileReference = GetItemReferenceFromStoreIter(GetStoreIterFromSorterIter(selectedIter));
			if (fileReference != null && fileReference.IsFile)
			{
				if (string.IsNullOrEmpty(fileReference.ItemPath))
				{
					return;
				}

				switch (fileReference.GetReferencedFileType())
				{
					case WarcraftFileType.AddonManifest:
					{
						break;
					}
					case WarcraftFileType.AddonManifestSignature:
					{
						break;
					}
					case WarcraftFileType.MoPaQArchive:
					{
						break;
					}
					case WarcraftFileType.ConfigurationFile:
					{
						break;
					}
					case WarcraftFileType.DatabaseContainer:
					{
						break;
					}
					case WarcraftFileType.Shader:
					{
						break;
					}
					case WarcraftFileType.TerrainWater:
					{
						break;
					}
					case WarcraftFileType.TerrainLiquid:
					{
						break;
					}
					case WarcraftFileType.TerrainLevel:
					{
						break;
					}
					case WarcraftFileType.TerrainTable:
					{
						break;
					}
					case WarcraftFileType.TerrainData:
					{
						break;
					}
					case WarcraftFileType.BinaryImage:
					{
						byte[] fileData = fileReference.Extract();
						if (fileData != null)
						{
							try
							{
								BLP image = new BLP(fileData);
								RenderableBLP renderableImage = new RenderableBLP(image, fileReference.ItemPath);

								this.viewportRenderer.SetRenderTarget(renderableImage);
								EnableControlPage(ControlPage.Image);
							}
							catch (FileLoadException fex)
							{
								Console.WriteLine("FileLoadException when opening BLP: " + fex.Message);
							}
						}

						break;
					}
					case WarcraftFileType.Hashmap:
					{
						break;
					}
					case WarcraftFileType.GameObjectModel:
					{
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
				}

				// Try some "normal" files
				if (fileReference.ItemPath.EndsWith(".jpg") || fileReference.ItemPath.EndsWith(".gif") || fileReference.ItemPath.EndsWith(".png"))
				{
					byte[] fileData = fileReference.Extract();
					if (fileData != null)
					{
						using (MemoryStream ms = new MemoryStream(fileData))
						{
							RenderableBitmap renderableImage = new RenderableBitmap(new Bitmap(ms), fileReference.ItemPath);
							this.viewportRenderer.SetRenderTarget(renderableImage);
						}

						EnableControlPage(ControlPage.Image);
					}
				}
			}
		}

		private void BeginLoadingWorldModel(FileReference fileReference)
		{
			this.StatusSpinner.Active = true;

			string modelName = IOPath.GetFileNameWithoutExtension(fileReference.ItemPath);
			uint modelStatusMessageContextID = this.MainStatusBar.GetContextId($"worldModelLoad_{modelName}");
			uint modelStatusMessageID = this.MainStatusBar.Push(modelStatusMessageContextID,
				$"Loading world model \"{modelName}\"...");

			Task.Factory.StartNew(() => ModelLoadingRoutines.LoadWorldModel(fileReference))
				.ContinueWith(modelLoadTask => ModelLoadingRoutines.CreateRenderableWorldModel(modelLoadTask.Result, fileReference.PackageGroup), this.UIThreadScheduler)
				.ContinueWith(createRenderableTask => this.viewportRenderer.SetRenderTarget(createRenderableTask.Result), this.UIThreadScheduler)
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

			string modelName = IOPath.GetFileNameWithoutExtension(fileReference.ItemPath);
			uint modelStatusMessageContextID = this.MainStatusBar.GetContextId($"worldModelGroupLoad_{modelName}");
			uint modelStatusMessageID = this.MainStatusBar.Push(modelStatusMessageContextID,
				$"Loading world model group \"{modelName}\"...");

			Task.Factory.StartNew(() => ModelLoadingRoutines.LoadWorldModelGroup(fileReference))
				.ContinueWith(modelLoadTask => ModelLoadingRoutines.CreateRenderableWorldModel(modelLoadTask.Result, fileReference.PackageGroup), this.UIThreadScheduler)
				.ContinueWith(createRenderableTask => this.viewportRenderer.SetRenderTarget(createRenderableTask.Result), this.UIThreadScheduler)
				.ContinueWith(result =>
					{
						this.StatusSpinner.Active = false;
						this.MainStatusBar.Remove(modelStatusMessageContextID, modelStatusMessageID);
						EnableControlPage(ControlPage.Model);
					},
					this.UIThreadScheduler);
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
				TreeIter iter = GetStoreIterFromVisiblePath(path);
				currentFileReference = GetItemReferenceFromStoreIter(iter);
			}

			if (e.Event.Type == EventType.ButtonPress && e.Event.Button == 3)
			{
				if (currentFileReference == null || string.IsNullOrEmpty(currentFileReference.ItemPath))
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
				currentReference = GetItemReferenceFromStoreIter(GetStoreIterFromSorterIter(iter));
			}

			if (e.Event.Type == EventType.ButtonPress && e.Event.Button == 3)
			{
				if (currentReference == null || string.IsNullOrEmpty(currentReference.ItemPath))
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
					AddPackageGroupNode(e.Reference);
				});
		}

		/// <summary>
		/// Adds a package group node to the game explorer view
		/// </summary>
		/// <param name="groupReference">PackageGroup reference.</param>
		private void AddPackageGroupNode(FileReference groupReference)
		{
			// Add the group node
			Pixbuf packageGroupIcon = IconTheme.Default.LoadIcon("user-home", 16, 0);
			TreeIter packageGroupNode = this.GameExplorerTreeStore.AppendValues(packageGroupIcon,
				                            groupReference.PackageGroup.GroupName, "", "Virtual file tree", (int)NodeType.PackageGroup);
			this.explorerBuilder.PackageItemNodeMapping.Add(groupReference, packageGroupNode);
			this.explorerBuilder.PackageNodeItemMapping.Add(packageGroupNode, groupReference);

			VirtualFileReference virtualGroupReference = groupReference as VirtualFileReference;
			if (virtualGroupReference != null)
			{
				this.explorerBuilder.PackageGroupVirtualNodeMapping.Add(groupReference.PackageGroup, virtualGroupReference);
			}

			// Add the package folder subnode
			Pixbuf packageFolderIcon = IconTheme.Default.LoadIcon("applications-other", 16, 0);
			TreeIter packageFolderNode = this.GameExplorerTreeStore.AppendValues(packageGroupNode,
				                             packageFolderIcon, "Packages", "", "Individual packages", (int)NodeType.PackageFolder);
			this.explorerBuilder.PackageItemNodeMapping.Add(groupReference.ChildReferences.First(), packageFolderNode);
			this.explorerBuilder.PackageNodeItemMapping.Add(packageFolderNode, groupReference.ChildReferences.First());
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
					AddPackageNode(e.Reference.ParentReference, e.Reference);
				});
		}

		/// <summary>
		/// Adds a package node to the game explorer view.
		/// </summary>
		/// <param name="parentReference">Parent reference where the package should be added.</param>
		/// <param name="packageReference">File reference pointing to the package.</param>
		private void AddPackageNode(FileReference parentReference, FileReference packageReference)
		{
			// I'm a new root node
			TreeIter parentNode;
			this.explorerBuilder.PackageItemNodeMapping.TryGetValue(parentReference, out parentNode);

			if (this.GameExplorerTreeStore.IterIsValid(parentNode))
			{
				// Add myself to that node
				if (!this.explorerBuilder.PackageItemNodeMapping.ContainsKey(packageReference))
				{
					TreeIter packageNode = this.GameExplorerTreeStore.AppendValues(parentNode,
						                       new Gtk.Image("package-x-generic", IconSize.Button), packageReference.PackageName, "", "", (int)NodeType.Package);
					this.explorerBuilder.PackageItemNodeMapping.Add(packageReference, packageNode);
					this.explorerBuilder.PackageNodeItemMapping.Add(packageNode, packageReference);
				}
			}

			// Map package nodes to virtual root nodes
			VirtualFileReference virtualGroupReference;
			if (this.explorerBuilder.PackageGroupVirtualNodeMapping.TryGetValue(packageReference.PackageGroup, out virtualGroupReference))
			{
				this.explorerBuilder.AddVirtualMapping(packageReference, virtualGroupReference);
			}
		}

		/// <summary>
		/// Adds a directory node to the game explorer view, attachedt to the provided parent
		/// package and directory.
		/// </summary>
		/// <param name="parentReference">Parent reference where the new directory should be added.</param>
		/// <param name="childReference">Child reference representing the directory.</param>
		private void AddDirectoryNode(FileReference parentReference, FileReference childReference)
		{
			TreeIter parentNode;
			this.explorerBuilder.PackageItemNodeMapping.TryGetValue(parentReference, out parentNode);

			if (this.GameExplorerTreeStore.IterIsValid(parentNode))
			{
				// Add myself to that node
				if (!this.explorerBuilder.PackageItemNodeMapping.ContainsKey(childReference))
				{
					if (parentReference.State == ReferenceState.Enumerating && childReference.State == ReferenceState.NotEnumerated)
					{
						// This references was added to the UI after the user had opened the previous folder.
						// Therefore, it should be submitted back to the UI for enumeration.
						this.explorerBuilder.SubmitWork(childReference);
					}

					TreeIter node = CreateDirectoryTreeNode(parentNode, childReference);
					this.explorerBuilder.PackageItemNodeMapping.Add(childReference, node);
					this.explorerBuilder.PackageNodeItemMapping.Add(node, childReference);
				}
			}

			// Now, let's add (or append to) the virtual node
			VirtualFileReference virtualParentReference = this.explorerBuilder.GetVirtualReference(parentReference);

			if (virtualParentReference != null)
			{
				TreeIter virtualParentNode;
				this.explorerBuilder.PackageItemNodeMapping.TryGetValue(virtualParentReference, out virtualParentNode);

				if (this.GameExplorerTreeStore.IterIsValid(virtualParentNode))
				{

					VirtualFileReference virtualChildReference = this.explorerBuilder.GetVirtualReference(childReference);

					if (virtualChildReference != null)
					{
						// Append this directory reference as an additional overridden hard reference
						virtualChildReference.OverriddenHardReferences.Add(childReference);

						if (virtualChildReference.State == ReferenceState.Enumerating)
						{
							// If it's currently enumerating, add it to the waiting queue
							this.explorerBuilder.SubmitWork(childReference);
						}
					}
					else
					{
						if (childReference.GetReferencedItemName() == "WTF")
						{
							Console.WriteLine("");
						}
						virtualChildReference = new VirtualFileReference(virtualParentReference, childReference.PackageGroup, childReference);

						if (!virtualParentReference.ChildReferences.Contains(virtualChildReference))
						{
							virtualParentReference.ChildReferences.Add(virtualChildReference);

							// Create a new virtual reference and a node that maps to it.
							if (!this.explorerBuilder.PackageItemNodeMapping.ContainsKey(virtualChildReference))
							{

								TreeIter node = CreateDirectoryTreeNode(virtualParentNode, virtualChildReference);

								this.explorerBuilder.PackageItemNodeMapping.Add(virtualChildReference, node);
								this.explorerBuilder.PackageNodeItemMapping.Add(node, virtualChildReference);

								// Needs to be a path, not a reference
								this.explorerBuilder.AddVirtualMapping(childReference, virtualChildReference);
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Creates a node in the <see cref="GameExplorerTreeView"/> for the specified directory reference, as
		/// a child below the specified parent node.
		/// </summary>
		/// <param name="parentNode">The parent node where the new node should be attached.</param>
		/// <param name="directory">The <see cref="FileReference"/> describing the directory.</param>
		/// <returns>A <see cref="TreeIter"/> pointing to the new directory node.</returns>
		private TreeIter CreateDirectoryTreeNode(TreeIter parentNode, FileReference directory)
		{
			Pixbuf directoryIcon = IconTheme.Default.LoadIcon(Stock.Directory, 16, 0);
			return this.GameExplorerTreeStore.AppendValues(parentNode,
				directoryIcon, directory.GetReferencedItemName(), "", "", (int)NodeType.Directory);
		}

		/// <summary>
		/// Adds a file node to the game explorer view, attached to the provided parent
		/// package and directory.
		/// </summary>
		/// <param name="parentReference">Parent file reference</param>
		/// <param name="childReference">Child file reference.</param>
		private void AddFileNode(FileReference parentReference, FileReference childReference)
		{
			TreeIter parentNode;
			this.explorerBuilder.PackageItemNodeMapping.TryGetValue(parentReference, out parentNode);

			if (this.GameExplorerTreeStore.IterIsValid(parentNode))
			{
				// Add myself to that node
				if (!this.explorerBuilder.PackageItemNodeMapping.ContainsKey(childReference))
				{
					parentReference.ChildReferences.Add(childReference);

					TreeIter node = CreateFileTreeNode(parentNode, childReference);

					this.explorerBuilder.PackageItemNodeMapping.Add(childReference, node);
					this.explorerBuilder.PackageNodeItemMapping.Add(node, childReference);
				}
			}

			// Now, let's add (or append to) the virtual node
			VirtualFileReference virtualParentReference = this.explorerBuilder.GetVirtualReference(parentReference);

			if (virtualParentReference != null)
			{
				TreeIter virtualParentNode;
				this.explorerBuilder.PackageItemNodeMapping.TryGetValue(virtualParentReference, out virtualParentNode);

				if (this.GameExplorerTreeStore.IterIsValid(virtualParentNode))
				{

					VirtualFileReference virtualChildReference = this.explorerBuilder.GetVirtualReference(childReference);

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
							if (!this.explorerBuilder.PackageItemNodeMapping.ContainsKey(virtualChildReference))
							{
								TreeIter node = CreateFileTreeNode(virtualParentNode, virtualChildReference);

								this.explorerBuilder.PackageItemNodeMapping.Add(virtualChildReference, node);
								this.explorerBuilder.PackageNodeItemMapping.Add(node, virtualChildReference);

								this.explorerBuilder.AddVirtualMapping(childReference, virtualChildReference);
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Creates a node in the <see cref="GameExplorerTreeView"/> for the specified file reference, as
		/// a child below the specified parent node.
		/// </summary>
		/// <param name="parentNode">The parent node where the new node should be attached.</param>
		/// <param name="file">The <see cref="FileReference"/> describing the file.</param>
		/// <returns>A <see cref="TreeIter"/> pointing to the new directory node.</returns>
		private TreeIter CreateFileTreeNode(TreeIter parentNode, FileReference file)
		{
			return this.GameExplorerTreeStore.AppendValues(parentNode, ExtensionMethods.GetIconForFiletype(file.ItemPath),
				file.GetReferencedItemName(), "", "", (int)NodeType.File);
		}

		/// <summary>
		/// Converts a <see cref="TreeIter"/> into an <see cref="FileReference"/>. The reference object is queried
		/// from the explorerBuilder's internal store.
		/// </summary>
		/// <returns>The FileReference object pointed to by the TreeIter.</returns>
		/// <param name="iter">The TreeIter.</param>
		private FileReference GetItemReferenceFromStoreIter(TreeIter iter)
		{
			FileReference reference;
			if (this.explorerBuilder.PackageNodeItemMapping.TryGetValue(iter, out reference))
			{
				return reference;
			}

			return null;
		}

		/// <summary>
		/// Handles application shutdown procedures - terminating render threads, cleaning
		/// up the UI, etc.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="a">The alpha component.</param>
		private void OnDeleteEvent(object sender, DeleteEventArgs a)
		{
			this.shuttingDown = true;

			if (this.explorerBuilder.IsActive)
			{
				this.explorerBuilder.Stop();
				this.explorerBuilder.Dispose();
			}

			this.viewportRenderer.SetRenderTarget(null);
			this.viewportRenderer.Dispose();

			Application.Quit();
			a.RetVal = true;
		}
	}

	/// <summary>
	/// Available control pages in the Everlook UI.
	/// </summary>
	public enum ControlPage
	{
		/// <summary>
		/// Image control page. Handles mip levels and rendered channels.
		/// </summary>
		Image = 0,

		/// <summary>
		/// Model control page. Handles vertex joining, geoset rendering and other model
		/// settings.
		/// </summary>
		Model = 1,

		/// <summary>
		/// Animation control page. Handles active animations and their settings.
		/// </summary>
		Animation = 2,

		/// <summary>
		/// Audio control page. Handles playback of audio.
		/// </summary>
		Audio = 3
	}
}