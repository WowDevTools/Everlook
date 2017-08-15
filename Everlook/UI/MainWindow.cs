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
using System.Threading;
using System.Threading.Tasks;
using Everlook.Audio;
using Everlook.Configuration;
using Everlook.Explorer;
using Everlook.Package;
using Everlook.Utility;
using Everlook.Viewport;
using Everlook.Viewport.Rendering;
using Everlook.Viewport.Rendering.Interfaces;
using Gdk;
using GLib;
using Gtk;
using liblistfile;
using liblistfile.NodeTree;
using log4net;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Input;
using Warcraft.Core;

using Application = Gtk.Application;
using EventArgs = System.EventArgs;
using FileNode = liblistfile.NodeTree.Node;
using IOPath = System.IO.Path;
using Task = System.Threading.Tasks.Task;
using WindowState = Gdk.WindowState;

namespace Everlook.UI
{
	/// <summary>
	/// Main UI class for Everlook. The "partial" qualifier is not strictly needed, but prevents the compiler from
	/// generating errors about the autoconnected members that relate to UI elements.
	/// </summary>
	public sealed partial class MainWindow : Gtk.Window
	{
		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(MainWindow));

		/// <summary>
		/// Static reference to the configuration handler.
		/// </summary>
		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

		/// <summary>
		/// Background viewport renderer. Handles all rendering in the viewport.
		/// </summary>
		private readonly ViewportRenderer RenderingEngine;

		/// <summary>
		/// Task scheduler for the UI thread. This allows task-based code to have very simple UI callbacks.
		/// </summary>
		private readonly TaskScheduler UiThreadScheduler;

		/// <summary>
		/// Whether or not the program is shutting down. This is used to remove callbacks and events.
		/// </summary>
		private bool IsShuttingDown;

		/// <summary>
		/// Cancellation token source for file loading operations.
		/// </summary>
		private CancellationTokenSource FileLoadingCancellationSource;

		/// <summary>
		/// A single global audio source. Used for playing individual files.
		/// </summary>
		private AudioSource GlobalAudio;

		/// <summary>
		/// Creates an instance of the <see cref="MainWindow"/> class, loading the glade XML UI as needed.
		/// </summary>
		/// <returns>An initialized instance of the MainWindow class.</returns>
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
			this.DeleteEvent += OnDeleteEvent;
			this.Shown += OnMainWindowShown;
			this.WindowStateEvent += OnWindowStateChanged;

			this.UiThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
			this.FileLoadingCancellationSource = new CancellationTokenSource();

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
				GraphicsContextFlags = GraphicsContextFlags.Default,
			};

			this.ViewportWidget.Events |=
				EventMask.ButtonPressMask |
				EventMask.ButtonReleaseMask |
				EventMask.EnterNotifyMask |
				EventMask.LeaveNotifyMask |
				EventMask.KeyPressMask |
				EventMask.KeyReleaseMask;

			this.ViewportWidget.Initialized += (sender, args) =>
			{
				// Initialize all OpenGL rendering parameters
				this.RenderingEngine.Initialize();
				Idle.Add(OnIdleRenderFrame);
			};

			this.ViewportWidget.ButtonPressEvent += OnViewportButtonPressed;
			this.ViewportWidget.ButtonReleaseEvent += OnViewportButtonReleased;
			this.ViewportWidget.EnterNotifyEvent += OnViewportMouseEnter;
			this.ViewportWidget.LeaveNotifyEvent += OnViewportMouseLeave;
			this.ViewportWidget.ConfigureEvent += OnViewportConfigured;

			this.RenderingEngine = new ViewportRenderer(this.ViewportWidget);
			this.ViewportAlignment.Add(this.ViewportWidget);
			this.ViewportAlignment.ShowAll();

			this.AboutButton.Clicked += OnAboutButtonClicked;
			this.PreferencesButton.Clicked += OnPreferencesButtonClicked;

			this.GameTabNotebook.ClearPages();

			this.ExportQueueTreeView.ButtonPressEvent += OnExportQueueButtonPressed;
			this.ExportQueueTreeView.GetColumn(0).SetCellDataFunc
			(
				this.ExportQueueTreeView.GetColumn(0).Cells[0],
				RenderExportQueueReferenceIcon
			);

			this.ExportQueueTreeView.GetColumn(0).Expand = true;
			this.ExportQueueTreeView.GetColumn(0).SetCellDataFunc
			(
				this.ExportQueueTreeView.GetColumn(0).Cells[1],
				RenderExportQueueReferenceName
			);

			this.RemoveQueueItem.Activated += OnQueueRemoveContextItemActivated;

			this.FileFilterComboBox.Changed += OnFilterChanged;

			/*
				Set up item control sections to default states
			*/

			EnableControlPage(ControlPage.None);

			/*
				Bind item control events
			*/

			this.RenderAlphaCheckButton.Toggled += (sender, args) =>
			{
				RenderableImage image = this.RenderingEngine.RenderTarget as RenderableImage;
				if (image == null)
				{
					return;
				}

				image.RenderAlphaChannel = this.RenderAlphaCheckButton.Active;
			};

			this.RenderRedCheckButton.Toggled += (sender, args) =>
			{
				RenderableImage image = this.RenderingEngine.RenderTarget as RenderableImage;
				if (image == null)
				{
					return;
				}

				image.RenderRedChannel = this.RenderRedCheckButton.Active;
			};

			this.RenderGreenCheckButton.Toggled += (sender, args) =>
			{
				RenderableImage image = this.RenderingEngine.RenderTarget as RenderableImage;
				if (image == null)
				{
					return;
				}

				image.RenderGreenChannel = this.RenderGreenCheckButton.Active;
			};

			this.RenderBlueCheckButton.Toggled += (sender, args) =>
			{
				RenderableImage image = this.RenderingEngine.RenderTarget as RenderableImage;
				if (image == null)
				{
					return;
				}

				image.RenderBlueChannel = this.RenderBlueCheckButton.Active;
			};

			this.RenderBoundsCheckButton.Toggled += (sender, args) =>
			{
				RenderableWorldModel wmo = this.RenderingEngine.RenderTarget as RenderableWorldModel;
				if (wmo != null)
				{
					wmo.ShouldRenderBounds = this.RenderBoundsCheckButton.Active;
				}

				RenderableGameModel mdx = this.RenderingEngine.RenderTarget as RenderableGameModel;
				if (mdx != null)
				{
					mdx.ShouldRenderBounds = this.RenderBoundsCheckButton.Active;
				}
			};

			this.RenderWireframeCheckButton.Toggled += (sender, args) =>
			{
				RenderableWorldModel wmo = this.RenderingEngine.RenderTarget as RenderableWorldModel;
				if (wmo != null)
				{
					wmo.ShouldRenderWireframe = this.RenderWireframeCheckButton.Active;
				}
			};
		}

		/// <summary>
		/// Handles expansion of the viewport pane when the window is maximized.
		/// </summary>
		/// <param name="o">The sending object.</param>
		/// <param name="args">The event arguments.</param>
		[ConnectBefore]
		private void OnWindowStateChanged(object o, WindowStateEventArgs args)
		{
			if (args.Event.NewWindowState.HasFlag(WindowState.Maximized))
			{
				this.ViewportPaned.Position =
					this.ViewportPaned.AllocatedHeight +
					this.LowerBoxPaned.AllocatedHeight +
					(int)this.ViewportAlignment.BottomPadding +
					(int)this.LowerBoxAlignment.TopPadding;
			}

			this.ViewportHasPendingRedraw = true;
		}

		/// <summary>
		/// Handles changing the cursor when leaving the viewport.
		/// </summary>
		/// <param name="o">The sending object.</param>
		/// <param name="args">The event arguments.</param>
		[ConnectBefore]
		private void OnViewportMouseLeave(object o, LeaveNotifyEventArgs args)
		{
			this.Window.Cursor = new Cursor(CursorType.Arrow);
		}

		/// <summary>
		/// Handles changing the cursor when hovering over the viewport
		/// </summary>
		/// <param name="o">The sending object.</param>
		/// <param name="args">The event arguments.</param>
		[ConnectBefore]
		private void OnViewportMouseEnter(object o, EnterNotifyEventArgs args)
		{
			if (this.RenderingEngine.RenderTarget?.Projection == ProjectionType.Orthographic)
			{
				this.Window.Cursor = new Cursor(CursorType.Hand2);
			}
		}

		/// <summary>
		/// Renders the name of a file reference in the export queue.
		/// </summary>
		/// <param name="column">The column which the cell is in.</param>
		/// <param name="cell">The cell which the reference is in.</param>
		/// <param name="model">The model of the treeview.</param>
		/// <param name="iter">The <see cref="TreeIter"/> pointing to the row the reference is in.</param>
		private static void RenderExportQueueReferenceName(TreeViewColumn column, CellRenderer cell, ITreeModel model, TreeIter iter)
		{
			CellRendererText cellText = cell as CellRendererText;
			FileReference reference = (FileReference)model.GetValue(iter, 0);

			if (reference == null || cellText == null)
			{
				return;
			}

			cellText.Text = reference.FilePath.Replace('\\', IOPath.DirectorySeparatorChar);
		}

		/// <summary>
		/// Renders the icon of a file reference in the export queue.
		/// </summary>
		/// <param name="column">The column which the cell is in.</param>
		/// <param name="cell">The cell which the icon is in.</param>
		/// <param name="model">The model of the treeview.</param>
		/// <param name="iter">The <see cref="TreeIter"/> pointing to the row the icon is in.</param>
		private static void RenderExportQueueReferenceIcon(TreeViewColumn column, CellRenderer cell, ITreeModel model, TreeIter iter)
		{
			CellRendererPixbuf cellIcon = cell as CellRendererPixbuf;
			FileReference reference = (FileReference)model.GetValue(iter, 0);

			if (reference == null || cellIcon == null)
			{
				return;
			}

			if (reference.Node.Type.HasFlag(NodeType.Directory))
			{
				cellIcon.Pixbuf = IconManager.GetIconForFiletype(WarcraftFileType.Directory);
				return;
			}

			cellIcon.Pixbuf = IconManager.GetIconForFiletype(reference.Node.FileType);
		}

		/// <summary>
		/// Handles updating the filter state for the game pages when the user changes it in the UI.
		/// </summary>
		/// <param name="sender">The sending object.</param>
		/// <param name="e">The event arguments.</param>
		private async void OnFilterChanged(object sender, EventArgs e)
		{
			ComboBox box = sender as ComboBox;
			if (box == null)
			{
				return;
			}

			this.StatusSpinner.Active = true;
			uint refilterStatusContextID = this.MainStatusBar.GetContextId("refreshFilter");
			uint refilterStatusMessageID = this.MainStatusBar.Push
			(
				refilterStatusContextID,
				"Refiltering node trees..."
			);

			FilterType filterType = (FilterType)box.Active;
			foreach (GamePage page in this.GamePages)
			{
				if (filterType == FilterType.All)
				{
					page.SetFilterState(false);

					page.SetTreeSensitivity(false);
					await page.RefilterAsync();
					page.SetTreeSensitivity(true);
				}
				else
				{
					page.SetFilterState(true);
					page.SetFilter(filterType.GetFileTypeSet());

					page.SetTreeSensitivity(false);
					await page.RefilterAsync();
					page.SetTreeSensitivity(true);
				}
			}

			this.StatusSpinner.Active = false;
			this.MainStatusBar.Remove(refilterStatusContextID, refilterStatusMessageID);
		}

		/// <summary>
		/// Performs any actions which should occur after the window is visible to the user.
		/// </summary>
		/// <param name="sender">The sending object.</param>
		/// <param name="eventArgs">The event arguments.</param>
		private async void OnMainWindowShown(object sender, EventArgs eventArgs)
		{
			await LoadGames();
		}

		/// <summary>
		/// Loads the games stored in the preferences into the UI.
		/// </summary>
		private async Task LoadGames()
		{
			GameLoader loader = new GameLoader();
			EverlookGameLoadingDialog dialog = EverlookGameLoadingDialog.Create(this);
			dialog.ShowAll();

			foreach (var gameTarget in GamePathStorage.Instance.GamePaths)
			{
				try
				{
					(PackageGroup group, OptimizedNodeTree nodeTree) = await loader.LoadGameAsync
					(
						gameTarget.Alias,
						gameTarget.Path,
						dialog.CancellationSource.Token,
						dialog.ProgressNotifier
					);

					AddGamePage(gameTarget.Alias, gameTarget.Version, group, nodeTree);
				}
				catch (OperationCanceledException)
				{
					Log.Info("Cancelled game loading operation.");
				}
			}

			dialog.Destroy();
		}

		private void AddGamePage(string alias, WarcraftVersion version, PackageGroup group, OptimizedNodeTree nodeTree)
		{
			GamePage page = new GamePage(group, nodeTree, version);
			page.Alias = alias;

			page.FileLoadRequested += OnFileLoadRequested;
			page.ExportItemRequested += OnExportItemRequested;
			page.EnqueueFileExportRequested += OnEnqueueItemRequested;

			this.GamePages.Add(page);
			this.GameTabNotebook.AppendPage(page.PageWidget, new Label(page.Alias));
			this.GameTabNotebook.SetTabReorderable(page.PageWidget, true);

			this.GameTabNotebook.ShowAll();
		}

		/// <summary>
		/// Enables the specified control page and brings it to the front. If the <paramref name="pageToEnable"/>
		/// parameter is <see cref="ControlPage.None"/>, this is interpreted as disabling all pages.
		/// </summary>
		/// <param name="pageToEnable">pageToEnable.</param>
		private void EnableControlPage(ControlPage pageToEnable)
		{
			if (pageToEnable == ControlPage.None)
			{
				foreach (ControlPage otherPage in Enum.GetValues(typeof(ControlPage)))
				{
					DisableControlPage(otherPage);
				}

				return;
			}

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
						RenderableImage image = this.RenderingEngine.RenderTarget as RenderableImage;
						if (image == null)
						{
							return;
						}

						this.MipCountLabel.Text = image.MipCount.ToString();

						this.RenderAlphaCheckButton.Sensitive = true;
						this.RenderRedCheckButton.Sensitive = true;
						this.RenderGreenCheckButton.Sensitive = true;
						this.RenderBlueCheckButton.Sensitive = true;

						image.RenderAlphaChannel = this.RenderAlphaCheckButton.Active;
						image.RenderRedChannel = this.RenderRedCheckButton.Active;
						image.RenderGreenChannel = this.RenderGreenCheckButton.Active;
						image.RenderBlueChannel = this.RenderBlueCheckButton.Active;
						break;
					}
					case ControlPage.Model:
					{
						this.RenderBoundsCheckButton.Sensitive = true;
						this.RenderWireframeCheckButton.Sensitive = true;
						RenderableWorldModel wmo = this.RenderingEngine.RenderTarget as RenderableWorldModel;
						if (wmo != null)
						{
							wmo.ShouldRenderBounds = this.RenderBoundsCheckButton.Active;
							wmo.ShouldRenderWireframe = this.RenderWireframeCheckButton.Active;
						}

						RenderableGameModel mdx = this.RenderingEngine.RenderTarget as RenderableGameModel;
						if (mdx != null)
						{
							mdx.ShouldRenderBounds = this.RenderBoundsCheckButton.Active;
						}

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
						this.RenderBoundsCheckButton.Sensitive = false;
						this.RenderWireframeCheckButton.Sensitive = false;
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
		/// Reloads visible runtime values that the user can change in the preferences, such as the colour
		/// of the viewport or the loaded packages.
		/// </summary>
		private async void ReloadRuntimeValues()
		{
			this.ViewportWidget.OverrideBackgroundColor(StateFlags.Normal, this.Config.GetViewportBackgroundColour());

			this.GamePages.Clear();
			this.GameTabNotebook.ClearPages();
			await LoadGames();
		}

		/// <summary>
		/// Loads and displays the specified fileReference in the UI, which is expected to point to a valid object.
		/// This function takes a delegate which will correctly load the file pointed to by the FileReference,
		/// and another delegate which will create a correct <see cref="IRenderable"/> object from the resulting
		/// object.
		/// </summary>
		/// <param name="gamePage">The game page that the renderable originated from.</param>
		/// <param name="fileReference">A <see cref="FileReference"/> which points to the desired file.</param>
		/// <param name="referenceLoadingRoutine">A delegate which correctly loads the desired file, returning a generic type T.</param>
		/// <param name="createRenderableDelegate">A delegate which accepts a generic type T and returns a renderable object.</param>
		/// <param name="associatedControlPage">The control page which the file is associated with, that is, the one with relevant controls.</param>
		/// <param name="ct">A cancellation token for this operation.</param>
		/// <typeparam name="T">The type of object to load.</typeparam>
		private async Task DisplayRenderableFile<T>(
			GamePage gamePage,
			FileReference fileReference,
			DataLoadingDelegates.LoadReferenceDelegate<T> referenceLoadingRoutine,
			DataLoadingDelegates.CreateRenderableDelegate<T> createRenderableDelegate,
			ControlPage associatedControlPage,
			CancellationToken ct)
		{
			if (fileReference == null)
			{
				throw new ArgumentNullException(nameof(fileReference));
			}

			Log.Info($"Loading \"{fileReference.FilePath}\".");

			this.StatusSpinner.Active = true;

			string modelName = fileReference.Filename;
			uint modelStatusMessageContextID = this.MainStatusBar.GetContextId($"itemLoad_{modelName}");
			uint modelStatusMessageID = this.MainStatusBar.Push
			(
				modelStatusMessageContextID,
				$"Loading \"{modelName}\"..."
			);

			try
			{
				T item = await Task.Run
				(
					() => referenceLoadingRoutine(fileReference),
					ct
				);

				IRenderable renderable = await Task.Factory.StartNew
				(
					() => createRenderableDelegate(item, fileReference, gamePage.Version),
					ct,
					TaskCreationOptions.None,
					this.UiThreadScheduler
				);

				if (renderable != null)
				{
					ct.ThrowIfCancellationRequested();

					// Replace the renderable on the UI thread
					await Task.Factory.StartNew
					(
						() => this.RenderingEngine.SetRenderTarget(renderable),
						ct,
						TaskCreationOptions.None,
						this.UiThreadScheduler
					);

					EnableControlPage(associatedControlPage);
				}
			}
			catch (OperationCanceledException)
			{
				Log.Info($"Cancelled loading of {fileReference.Filename}");
			}
			finally
			{
				this.StatusSpinner.Active = false;
				this.MainStatusBar.Remove(modelStatusMessageContextID, modelStatusMessageID);
			}
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

			if (args.Event.Type != EventType.ButtonPress)
			{
				return;
			}

			bool validButtonIsPressed = false;
			if (this.RenderingEngine.RenderTarget.Projection == ProjectionType.Perspective)
			{
				// Exclusively check for right click
				if (args.Event.Button == 3)
				{
					validButtonIsPressed = true;

					// Hide the mouse pointer
					this.Window.Cursor = new Cursor(CursorType.BlankCursor);
				}
			}
			else
			{
				// Allow both right and left
				if (args.Event.Button == 1 || args.Event.Button == 3)
				{
					validButtonIsPressed = true;
				}
			}

			if (!validButtonIsPressed)
			{
				return;
			}

			this.ViewportWidget.GrabFocus();

			this.RenderingEngine.InitialMouseX = Mouse.GetCursorState().X;
			this.RenderingEngine.InitialMouseY = Mouse.GetCursorState().Y;

			this.RenderingEngine.WantsToMove = true;
		}

		/// <summary>
		/// Handles input inside the OpenGL viewport for mouse button releases.
		/// This function restores input focus to the main UI and returns the
		/// cursor to its original appearance.
		/// </summary>
		[ConnectBefore]
		private void OnViewportButtonReleased(object o, ButtonReleaseEventArgs args)
		{
			if (args.Event.Type != EventType.ButtonRelease)
			{
				return;
			}

			bool validButtonIsPressed = false;
			if (this.RenderingEngine.RenderTarget?.Projection == ProjectionType.Perspective)
			{
				// Exclusively check for right click
				if (args.Event.Button == 3)
				{
					validButtonIsPressed = true;

					// Return the mouse pointer to its original appearance
					this.Window.Cursor = new Cursor(CursorType.Arrow);
				}
			}
			else
			{
				// Allow both right and left
				if (args.Event.Button == 1 || args.Event.Button == 3)
				{
					validButtonIsPressed = true;
				}
			}

			if (!validButtonIsPressed)
			{
				return;
			}

			GrabFocus();
			this.RenderingEngine.WantsToMove = false;
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

			if (this.RenderingEngine.HasRenderTarget || this.ViewportHasPendingRedraw)
			{
				this.RenderingEngine.RenderFrame();
				this.ViewportHasPendingRedraw = false;
			}

			return keepCalling;
		}

		/// <summary>
		/// Sets the viewport to redraw whenever the widget gets a configure event.
		/// </summary>
		/// <param name="o">The sending object.</param>
		/// <param name="args">The configuration arguments.</param>
		private void OnViewportConfigured(object o, ConfigureEventArgs args)
		{
			this.ViewportHasPendingRedraw = true;
		}

		/// <summary>
		/// Handles the export item context item activated event.
		/// </summary>
		/// <param name="page">Sender.</param>
		/// <param name="fileReference">E.</param>
		private static void OnExportItemRequested(GamePage page, FileReference fileReference)
		{
			// TODO: Create a better exporter (EverlookExporter.Export(<>)?)
			switch (fileReference.GetReferencedFileType())
			{
				case WarcraftFileType.Directory:
				{
					/*
					using (EverlookDirectoryExportDialog exportDialog = EverlookDirectoryExportDialog.Create(fileReference))
					{
						if (exportDialog.Run() == (int)ResponseType.Ok)
						{
							exportDialog.RunExport();
						}
						exportDialog.Destroy();
					}
					*/
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

		/// <summary>
		/// Handles queueing of a selected file in the archive, triggered by a context
		/// menu press.
		/// </summary>
		/// <param name="page">Sender.</param>
		/// <param name="fileReference">E.</param>
		private void OnEnqueueItemRequested(GamePage page, FileReference fileReference)
		{
			this.ExportQueueListStore.AppendValues(fileReference, IconManager.GetIcon("package-downgrade"));
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
		/// Handles selection of files in the game explorer, displaying them to the user and routing
		/// whatever rendering functionality the file needs to the viewport.
		/// </summary>
		/// <param name="page">The <see cref="GamePage"/> in which the event originated.</param>
		/// <param name="fileReference">The file reference to load.</param>
		private async void OnFileLoadRequested(GamePage page, FileReference fileReference)
		{
			WarcraftFileType referencedType = fileReference.GetReferencedFileType();
			switch (referencedType)
			{
				case WarcraftFileType.BinaryImage:
				{
					this.FileLoadingCancellationSource.Cancel();
					this.FileLoadingCancellationSource = new CancellationTokenSource();

					await DisplayRenderableFile
					(
						page,
						fileReference,
						DataLoadingRoutines.LoadBinaryImage,
						DataLoadingRoutines.CreateRenderableBinaryImage,
						ControlPage.Image,
						this.FileLoadingCancellationSource.Token
					);

					break;
				}
				case WarcraftFileType.WorldObjectModel:
				{
					this.FileLoadingCancellationSource.Cancel();
					this.FileLoadingCancellationSource = new CancellationTokenSource();

					await DisplayRenderableFile
					(
						page,
						fileReference,
						DataLoadingRoutines.LoadWorldModel,
						DataLoadingRoutines.CreateRenderableWorldModel,
						ControlPage.Model,
						this.FileLoadingCancellationSource.Token
					);

					break;
				}
				case WarcraftFileType.WorldObjectModelGroup:
				{
					this.FileLoadingCancellationSource.Cancel();
					this.FileLoadingCancellationSource = new CancellationTokenSource();

					await DisplayRenderableFile
					(
						page,
						fileReference,
						DataLoadingRoutines.LoadWorldModelGroup,
						DataLoadingRoutines.CreateRenderableWorldModel,
						ControlPage.Model,
						this.FileLoadingCancellationSource.Token
					);

					break;
				}
				case WarcraftFileType.GIFImage:
				case WarcraftFileType.PNGImage:
				case WarcraftFileType.JPGImage:
				{
					this.FileLoadingCancellationSource.Cancel();
					this.FileLoadingCancellationSource = new CancellationTokenSource();

					await DisplayRenderableFile
					(
						page,
						fileReference,
						DataLoadingRoutines.LoadBitmapImage,
						DataLoadingRoutines.CreateRenderableBitmapImage,
						ControlPage.Image,
						this.FileLoadingCancellationSource.Token
					);

					break;
				}
				case WarcraftFileType.WaveAudio:
				case WarcraftFileType.MP3Audio:
				{
					AudioManager.UnregisterSource(this.GlobalAudio);

					this.GlobalAudio = AudioSource.CreateNew();
					await this.GlobalAudio.SetAudioAsync(fileReference);

					AudioManager.RegisterSource(this.GlobalAudio);

					this.GlobalAudio.Play();
					break;
				}
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
			if (e.Event.Type != EventType.ButtonPress || e.Event.Button != 3)
			{
				// Return if not a right click
				return;
			}

			TreePath path;
			this.ExportQueueTreeView.GetPathAtPos((int)e.Event.X, (int)e.Event.Y, out path);

			if (path == null)
			{
				return;
			}

			TreeIter iter;
			this.ExportQueueListStore.GetIter(out iter, path);

			FileReference queuedReference = (FileReference)this.ExportQueueListStore.GetValue(iter, 0);

			if (string.IsNullOrEmpty(queuedReference?.FilePath))
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
		/// Handles application shutdown procedures - terminating render threads, cleaning
		/// up the UI, etc.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="a">The alpha component.</param>
		private void OnDeleteEvent(object sender, DeleteEventArgs a)
		{
			this.IsShuttingDown = true;

			Idle.Remove(OnIdleRenderFrame);

			this.RenderingEngine.SetRenderTarget(null);
			this.RenderingEngine.Dispose();

			this.ViewportWidget.Dispose();

			Application.Quit();
			a.RetVal = true;
		}
	}
}
