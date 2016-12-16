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
using System.Security.Policy;
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
using Warcraft.WMO;
using Warcraft.WMO.GroupFile;
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

			this.ViewportWidget = new GLWidget(GraphicsMode.Default)
			{
				CanFocus = true,
				SingleBuffer = false,
				ColorBPP = 24,
				DepthBPP = 32,
				Samples = 4,
				GLVersionMajor = 3,
				GLVersionMinor = 3,
				GraphicsContextFlags = GraphicsContextFlags.Default,
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

			AboutButton.Clicked += OnAboutButtonClicked;
			PreferencesButton.Clicked += OnPreferencesButtonClicked;

			GameExplorerTreeView.RowExpanded += OnGameExplorerRowExpanded;
			GameExplorerTreeView.Selection.Changed += OnGameExplorerSelectionChanged;
			GameExplorerTreeView.ButtonPressEvent += OnGameExplorerButtonPressed;

			GameExplorerTreeSorter.SetSortFunc(1, SortGameExplorerRow);
			GameExplorerTreeSorter.SetSortColumnId(1, SortType.Descending);

			ExportQueueTreeView.ButtonPressEvent += OnExportQueueButtonPressed;

			ExtractItem.Activated += OnExtractContextItemActivated;
			ExportItem.Activated += OnExportItemContextItemActivated;
			OpenItem.Activated += OnOpenContextItemActivated;
			CopyItem.Activated += OnCopyContextItemActivated;
			QueueItem.Activated += OnQueueContextItemActivated;

			RemoveQueueItem.Activated += OnQueueRemoveContextItemActivated;

			explorerBuilder.PackageGroupAdded += OnPackageGroupAdded;
			explorerBuilder.PackageEnumerated += OnPackageEnumerated;
			explorerBuilder.Start(); // TODO: This is a performance hog (and the whole damn thing should be rewritten)

			/*
				Set up item control sections
			*/

			// Image
			RenderAlphaCheckButton.Sensitive = false;
			RenderRedCheckButton.Sensitive = false;
			RenderGreenCheckButton.Sensitive = false;
			RenderBlueCheckButton.Sensitive = false;

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
				this.GrabFocus();
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

			if (shuttingDown)
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
			viewportHasPendingRedraw = true;
		}

		/// <summary>
		/// Enables the specified control page and brings it to the front.
		/// </summary>
		/// <param name="controlPage">controlPage.</param>
		private void EnableControlPage(ControlPage controlPage)
		{
			if (Enum.IsDefined(typeof(ControlPage), controlPage))
			{
				ItemControlNotebook.Page = (int)controlPage;

				if (controlPage == ControlPage.Image)
				{
					RenderAlphaCheckButton.Sensitive = true;
					RenderRedCheckButton.Sensitive = true;
					RenderGreenCheckButton.Sensitive = true;
					RenderBlueCheckButton.Sensitive = true;
				}
				else if (controlPage == ControlPage.Model)
				{

				}
				else if (controlPage == ControlPage.Animation)
				{

				}
				else if (controlPage == ControlPage.Audio)
				{

				}
			}
		}

		/// <summary>
		/// Disables the specified control page.
		/// </summary>
		/// <param name="controlPage">controlPage.</param>
		private void DisableControlPage(ControlPage controlPage)
		{
			if (Enum.IsDefined(typeof(ControlPage), controlPage))
			{
				if (controlPage == ControlPage.Image)
				{
					RenderAlphaCheckButton.Sensitive = false;
					RenderRedCheckButton.Sensitive = false;
					RenderGreenCheckButton.Sensitive = false;
					RenderBlueCheckButton.Sensitive = false;
				}
				else if (controlPage == ControlPage.Model)
				{

				}
				else if (controlPage == ControlPage.Animation)
				{

				}
				else if (controlPage == ControlPage.Audio)
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
			const int SORT_A_BEFORE_B = -1;
			const int SORT_A_WITH_B = 0;
			const int SORT_A_AFTER_B = 1;

			NodeType typeofA = (NodeType)model.GetValue(iterA, 4);
			NodeType typeofB = (NodeType)model.GetValue(iterB, 4);

			if (typeofA < typeofB)
			{
				return SORT_A_AFTER_B;
			}
			if (typeofA > typeofB)
			{
				return SORT_A_BEFORE_B;
			}

			string aComparisonString = (string)model.GetValue(iterA, 1);

			string bComparisonString = (string)model.GetValue(iterB, 1);

			int result = String.CompareOrdinal(aComparisonString, bComparisonString);

			if (result <= SORT_A_BEFORE_B)
			{
				return SORT_A_AFTER_B;
			}

			if (result >= SORT_A_AFTER_B)
			{
				return SORT_A_BEFORE_B;
			}

			return SORT_A_WITH_B;

		}

		/// <summary>
		/// Idle functionality. This code is called as a way of lazily loading rows into the UI
		/// without causing lockups due to sheer data volume.
		/// </summary>
		private bool OnGLibLoopIdle()
		{
			const bool keepCalling = true;
			const bool stopCalling = false;

			if (shuttingDown)
			{
				return stopCalling;
			}

			if (explorerBuilder.EnumeratedReferences.Count > 0)
			{
				// There's content to be added to the UI
				// Get the last reference in the list.
				ItemReference newContent = explorerBuilder.EnumeratedReferences[explorerBuilder.EnumeratedReferences.Count - 1];

				if (newContent == null)
				{
					explorerBuilder.EnumeratedReferences.RemoveAt(explorerBuilder.EnumeratedReferences.Count - 1);
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

				explorerBuilder.EnumeratedReferences.Remove(newContent);
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
			ItemReference fileReference = GetSelectedReference();
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
			ItemReference fileReference = GetSelectedReference();

			string cleanFilepath = fileReference.ItemPath.ConvertPathSeparatorsToCurrentNativeSeparator();
			string exportpath;
			if (Config.GetShouldKeepFileDirectoryStructure())
			{
				exportpath = Config.GetDefaultExportDirectory() + cleanFilepath;
				Directory.CreateDirectory(Directory.GetParent(exportpath).FullName);
			}
			else
			{
				string filename = IOPath.GetFileName(cleanFilepath);
				exportpath = Config.GetDefaultExportDirectory() + filename;
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
			GameExplorerTreeView.Selection.GetSelected(out selectedIter);
			ItemReference itemReference = GetSelectedReference();
			if (!itemReference.IsFile)
			{
				GameExplorerTreeView.ExpandRow(GameExplorerTreeSorter.GetPath(selectedIter), false);
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
			GameExplorerTreeView.Selection.GetSelected(out selectedIter);

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
			ItemReference itemReference = GetSelectedReference();

			string cleanFilepath = itemReference.ItemPath.ConvertPathSeparatorsToCurrentNativeSeparator();

			if (String.IsNullOrEmpty(cleanFilepath))
			{
				cleanFilepath = itemReference.PackageName;
			}
			else if (String.IsNullOrEmpty(IOPath.GetFileName(cleanFilepath)))
			{
				cleanFilepath = Directory.GetParent(cleanFilepath).FullName.Replace(Directory.GetCurrentDirectory(), "");
			}

			ExportQueueListStore.AppendValues(cleanFilepath, cleanFilepath, "Queued");
		}

		/// <summary>
		/// Displays the About dialog to the user.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnAboutButtonClicked(object sender, EventArgs e)
		{
			AboutDialog.Run();
			AboutDialog.Hide();
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
		/// Gets the current <see cref="ItemReference"/> that is selected in the tree view.
		/// </summary>
		/// <returns></returns>
		private ItemReference GetSelectedReference()
		{
			TreeIter selectedIter;
			GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			return GetItemReferenceFromStoreIter(GetStoreIterFromSorterIter(selectedIter));
		}

		/// <summary>
		/// Reloads visible runtime values that the user can change in the preferences, such as the colour
		/// of the viewport or the loaded packages.
		/// </summary>
		private void ReloadRuntimeValues()
		{
			ViewportWidget.OverrideBackgroundColor(StateFlags.Normal, Config.GetViewportBackgroundColour());

			if (explorerBuilder.HasPackageDirectoryChanged())
			{
				GameExplorerTreeStore.Clear();
				explorerBuilder.Reload();
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
			ItemReference parentReference = GetItemReferenceFromStoreIter(GetStoreIterFromVisiblePath(e.Path));
			foreach (ItemReference childReference in parentReference.ChildReferences)
			{
				if (childReference.IsDirectory && childReference.State != ReferenceState.Enumerated)
				{
					explorerBuilder.SubmitWork(childReference);
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
			GameExplorerTreeSorter.GetIter(out sorterIter, path);
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
			TreeIter filterIter = GameExplorerTreeSorter.ConvertIterToChildIter(sorterIter);
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
			return GameExplorerTreeFilter.ConvertIterToChildIter(filterIter);
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
			GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			ItemReference fileReference = GetItemReferenceFromStoreIter(GetStoreIterFromSorterIter(selectedIter));
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
								BLP blp = new BLP(fileData);
								RenderableBLP image = new RenderableBLP(blp, fileReference.ItemPath);
								viewportRenderer.SetRenderTarget(image);
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
						byte[] fileData = fileReference.Extract();
						if (fileData != null)
						{
							WMO worldModel = new WMO(fileData);

							string modelPathWithoutExtension = IOPath.GetFileNameWithoutExtension(fileReference.ItemPath);
							for (int i = 0; i < worldModel.GroupCount; ++i)
							{
								// Extract the groups as well
								string modelGroupPath = $"{modelPathWithoutExtension}_{i:D3}.wmo";
								byte[] modelGroupData = fileReference.PackageGroup.ExtractFile(modelGroupPath);

								if (modelGroupData != null)
								{
									worldModel.AddModelGroup(new ModelGroup(modelGroupData));
								}

								RenderableWorldModel renderableWorldModel = new RenderableWorldModel(worldModel, fileReference.PackageGroup);
								this.viewportRenderer.SetRenderTarget(renderableWorldModel);
							}
						}

						break;
					}
					case WarcraftFileType.WorldObjectModelGroup:
					{
						// Get the file name of the root object
						string modelRootPath = fileReference.ItemPath.Remove(fileReference.ItemPath.Length - 8, 4);

						// Extract it and load just this model group
						byte[] fileData = fileReference.PackageGroup.ExtractFile(modelRootPath);
						if (fileData != null)
						{
							WMO worldModel = new WMO(fileData);
							byte[] modelGroupData = fileReference.Extract();
							if (modelGroupData != null)
							{
								worldModel.AddModelGroup(new ModelGroup(modelGroupData));
							}

							RenderableWorldModel renderableWorldModel = new RenderableWorldModel(worldModel, fileReference.PackageGroup);
							this.viewportRenderer.SetRenderTarget(renderableWorldModel);
						}

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
							RenderableBitmap renderable = new RenderableBitmap(new Bitmap(ms), fileReference.ItemPath);
							viewportRenderer.SetRenderTarget(renderable);
						}

						EnableControlPage(ControlPage.Image);
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
			GameExplorerTreeView.GetPathAtPos((int)e.Event.X, (int)e.Event.Y, out path);

			ItemReference currentItemReference = null;
			if (path != null)
			{
				TreeIter iter = GetStoreIterFromVisiblePath(path);
				currentItemReference = GetItemReferenceFromStoreIter(iter);
			}

			if (e.Event.Type == EventType.ButtonPress && e.Event.Button == 3)
			{
				if (currentItemReference == null || string.IsNullOrEmpty(currentItemReference.ItemPath))
				{
					ExtractItem.Sensitive = false;
					ExportItem.Sensitive = false;
					OpenItem.Sensitive = false;
					QueueItem.Sensitive = false;
					CopyItem.Sensitive = false;
				}
				else
				{
					if (!currentItemReference.IsFile)
					{
						ExtractItem.Sensitive = false;
						ExportItem.Sensitive = true;
						OpenItem.Sensitive = true;
						QueueItem.Sensitive = true;
						CopyItem.Sensitive = true;
					}
					else
					{
						ExtractItem.Sensitive = true;
						ExportItem.Sensitive = true;
						OpenItem.Sensitive = true;
						QueueItem.Sensitive = true;
						CopyItem.Sensitive = true;
					}
				}


				FileContextMenu.ShowAll();
				FileContextMenu.Popup();
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
			ExportQueueTreeView.GetPathAtPos((int)e.Event.X, (int)e.Event.Y, out path);

			ItemReference currentReference = null;
			if (path != null)
			{
				TreeIter iter;
				ExportQueueListStore.GetIterFromString(out iter, path.ToString());
				currentReference = GetItemReferenceFromStoreIter(GetStoreIterFromSorterIter(iter));
			}

			if (e.Event.Type == EventType.ButtonPress && e.Event.Button == 3)
			{
				if (currentReference == null || string.IsNullOrEmpty(currentReference.ItemPath))
				{
					RemoveQueueItem.Sensitive = false;
				}
				else
				{
					RemoveQueueItem.Sensitive = true;
				}

				QueueContextMenu.ShowAll();
				QueueContextMenu.Popup();
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
			ExportQueueTreeView.Selection.GetSelected(out selectedIter);

			ExportQueueListStore.Remove(ref selectedIter);
		}

		/// <summary>
		/// Handles the package group added event from the explorer builder.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnPackageGroupAdded(object sender, ItemEnumeratedEventArgs e)
		{
			Application.Invoke(delegate
				{
					AddPackageGroupNode(e.Item);
				});
		}

		/// <summary>
		/// Adds a package group node to the game explorer view
		/// </summary>
		/// <param name="groupReference">PackageGroup reference.</param>
		private void AddPackageGroupNode(ItemReference groupReference)
		{
			// Add the group node
			Pixbuf packageGroupIcon = IconTheme.Default.LoadIcon("user-home", 16, 0);
			TreeIter packageGroupNode = GameExplorerTreeStore.AppendValues(packageGroupIcon,
				                            groupReference.PackageGroup.GroupName, "", "Virtual file tree", (int)NodeType.PackageGroup);
			explorerBuilder.PackageItemNodeMapping.Add(groupReference, packageGroupNode);
			explorerBuilder.PackageNodeItemMapping.Add(packageGroupNode, groupReference);

			VirtualItemReference virtualGroupReference = groupReference as VirtualItemReference;
			if (virtualGroupReference != null)
			{
				explorerBuilder.PackageGroupVirtualNodeMapping.Add(groupReference.PackageGroup, virtualGroupReference);
			}

			// Add the package folder subnode
			Pixbuf packageFolderIcon = IconTheme.Default.LoadIcon("applications-other", 16, 0);
			TreeIter packageFolderNode = GameExplorerTreeStore.AppendValues(packageGroupNode,
				                             packageFolderIcon, "Packages", "", "Individual packages", (int)NodeType.PackageFolder);
			explorerBuilder.PackageItemNodeMapping.Add(groupReference.ChildReferences.First(), packageFolderNode);
			explorerBuilder.PackageNodeItemMapping.Add(packageFolderNode, groupReference.ChildReferences.First());
		}

		/// <summary>
		/// Handles the package enumerated event from the explorer builder.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnPackageEnumerated(object sender, ItemEnumeratedEventArgs e)
		{
			Application.Invoke(delegate
				{
					AddPackageNode(e.Item.ParentReference, e.Item);
				});
		}

		/// <summary>
		/// Adds a package node to the game explorer view.
		/// </summary>
		/// <param name="parentReference">Parent reference where the package should be added.</param>
		/// <param name="packageReference">Item reference pointing to the package.</param>
		private void AddPackageNode(ItemReference parentReference, ItemReference packageReference)
		{
			// I'm a new root node
			TreeIter parentNode;
			explorerBuilder.PackageItemNodeMapping.TryGetValue(parentReference, out parentNode);

			if (GameExplorerTreeStore.IterIsValid(parentNode))
			{
				// Add myself to that node
				if (!explorerBuilder.PackageItemNodeMapping.ContainsKey(packageReference))
				{
					TreeIter packageNode = GameExplorerTreeStore.AppendValues(parentNode,
						                       new Gtk.Image("package-x-generic", IconSize.Button), packageReference.PackageName, "", "", (int)NodeType.Package);
					explorerBuilder.PackageItemNodeMapping.Add(packageReference, packageNode);
					explorerBuilder.PackageNodeItemMapping.Add(packageNode, packageReference);
				}
			}

			// Map package nodes to virtual root nodes
			VirtualItemReference virtualGroupReference;
			if (explorerBuilder.PackageGroupVirtualNodeMapping.TryGetValue(packageReference.PackageGroup, out virtualGroupReference))
			{
				explorerBuilder.AddVirtualMapping(packageReference, virtualGroupReference);
			}
		}

		/// <summary>
		/// Adds a directory node to the game explorer view, attachedt to the provided parent
		/// package and directory.
		/// </summary>
		/// <param name="parentReference">Parent reference where the new directory should be added.</param>
		/// <param name="childReference">Child reference representing the directory.</param>
		private void AddDirectoryNode(ItemReference parentReference, ItemReference childReference)
		{
			TreeIter parentNode;
			explorerBuilder.PackageItemNodeMapping.TryGetValue(parentReference, out parentNode);

			if (GameExplorerTreeStore.IterIsValid(parentNode))
			{
				// Add myself to that node
				if (!explorerBuilder.PackageItemNodeMapping.ContainsKey(childReference))
				{
					if (parentReference.State == ReferenceState.Enumerating && childReference.State == ReferenceState.NotEnumerated)
					{
						// This references was added to the UI after the user had opened the previous folder.
						// Therefore, it should be submitted back to the UI for enumeration.
						this.explorerBuilder.SubmitWork(childReference);
					}

					TreeIter node = CreateDirectoryTreeNode(parentNode, childReference);
					explorerBuilder.PackageItemNodeMapping.Add(childReference, node);
					explorerBuilder.PackageNodeItemMapping.Add(node, childReference);
				}
			}

			// Now, let's add (or append to) the virtual node
			VirtualItemReference virtualParentReference = explorerBuilder.GetVirtualReference(parentReference);

			if (virtualParentReference != null)
			{
				TreeIter virtualParentNode;
				explorerBuilder.PackageItemNodeMapping.TryGetValue(virtualParentReference, out virtualParentNode);

				if (GameExplorerTreeStore.IterIsValid(virtualParentNode))
				{

					VirtualItemReference virtualChildReference = explorerBuilder.GetVirtualReference(childReference);

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
						virtualChildReference = new VirtualItemReference(virtualParentReference, childReference.PackageGroup, childReference);

						if (!virtualParentReference.ChildReferences.Contains(virtualChildReference))
						{
							virtualParentReference.ChildReferences.Add(virtualChildReference);

							// Create a new virtual reference and a node that maps to it.
							if (!explorerBuilder.PackageItemNodeMapping.ContainsKey(virtualChildReference))
							{

								TreeIter node = CreateDirectoryTreeNode(virtualParentNode, virtualChildReference);

								explorerBuilder.PackageItemNodeMapping.Add(virtualChildReference, node);
								explorerBuilder.PackageNodeItemMapping.Add(node, virtualChildReference);

								// Needs to be a path, not a reference
								explorerBuilder.AddVirtualMapping(childReference, virtualChildReference);
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
		/// <param name="directory">The <see cref="ItemReference"/> describing the directory.</param>
		/// <returns>A <see cref="TreeIter"/> pointing to the new directory node.</returns>
		private TreeIter CreateDirectoryTreeNode(TreeIter parentNode, ItemReference directory)
		{
			Pixbuf directoryIcon = IconTheme.Default.LoadIcon(Stock.Directory, 16, 0);
			return GameExplorerTreeStore.AppendValues(parentNode,
				directoryIcon, directory.GetReferencedItemName(), "", "", (int)NodeType.Directory);
		}

		/// <summary>
		/// Adds a file node to the game explorer view, attached to the provided parent
		/// package and directory.
		/// </summary>
		/// <param name="parentReference">Parent file reference</param>
		/// <param name="childReference">Child file reference.</param>
		private void AddFileNode(ItemReference parentReference, ItemReference childReference)
		{
			TreeIter parentNode;
			explorerBuilder.PackageItemNodeMapping.TryGetValue(parentReference, out parentNode);

			if (GameExplorerTreeStore.IterIsValid(parentNode))
			{
				// Add myself to that node
				if (!explorerBuilder.PackageItemNodeMapping.ContainsKey(childReference))
				{
					parentReference.ChildReferences.Add(childReference);

					TreeIter node = CreateFileTreeNode(parentNode, childReference);

					explorerBuilder.PackageItemNodeMapping.Add(childReference, node);
					explorerBuilder.PackageNodeItemMapping.Add(node, childReference);
				}
			}

			// Now, let's add (or append to) the virtual node
			VirtualItemReference virtualParentReference = explorerBuilder.GetVirtualReference(parentReference);

			if (virtualParentReference != null)
			{
				TreeIter virtualParentNode;
				explorerBuilder.PackageItemNodeMapping.TryGetValue(virtualParentReference, out virtualParentNode);

				if (GameExplorerTreeStore.IterIsValid(virtualParentNode))
				{

					VirtualItemReference virtualChildReference = explorerBuilder.GetVirtualReference(childReference);

					if (virtualChildReference != null)
					{
						// Append this directory reference as an additional overridden hard reference
						virtualChildReference.OverriddenHardReferences.Add(childReference);
					}
					else
					{
						virtualChildReference = new VirtualItemReference(virtualParentReference, childReference.PackageGroup, childReference);

						if (!virtualParentReference.ChildReferences.Contains(virtualChildReference))
						{
							virtualParentReference.ChildReferences.Add(virtualChildReference);

							// Create a new virtual reference and a node that maps to it.
							if (!explorerBuilder.PackageItemNodeMapping.ContainsKey(virtualChildReference))
							{
								TreeIter node = CreateFileTreeNode(virtualParentNode, virtualChildReference);

								explorerBuilder.PackageItemNodeMapping.Add(virtualChildReference, node);
								explorerBuilder.PackageNodeItemMapping.Add(node, virtualChildReference);

								explorerBuilder.AddVirtualMapping(childReference, virtualChildReference);
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
		/// <param name="file">The <see cref="ItemReference"/> describing the file.</param>
		/// <returns>A <see cref="TreeIter"/> pointing to the new directory node.</returns>
		private TreeIter CreateFileTreeNode(TreeIter parentNode, ItemReference file)
		{
			return GameExplorerTreeStore.AppendValues(parentNode, ExtensionMethods.GetIconForFiletype(file.ItemPath),
				file.GetReferencedItemName(), "", "", (int)NodeType.File);
		}

		/// <summary>
		/// Converts a <see cref="TreeIter"/> into an <see cref="ItemReference"/>. The reference object is queried
		/// from the explorerBuilder's internal store.
		/// </summary>
		/// <returns>The ItemReference object pointed to by the TreeIter.</returns>
		/// <param name="iter">The TreeIter.</param>
		private ItemReference GetItemReferenceFromStoreIter(TreeIter iter)
		{
			ItemReference reference;
			if (explorerBuilder.PackageNodeItemMapping.TryGetValue(iter, out reference))
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
			shuttingDown = true;

			if (explorerBuilder.IsActive)
			{
				explorerBuilder.Stop();
				explorerBuilder.Dispose();
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