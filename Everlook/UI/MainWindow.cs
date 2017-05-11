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
using System.IO;
using System.Threading.Tasks;
using Everlook.Configuration;
using Everlook.Explorer;
using Everlook.Utility;
using Everlook.Viewport;
using Everlook.Viewport.Rendering.Interfaces;
using Gdk;
using GLib;
using Gtk;
using liblistfile.NodeTree;
using log4net;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Input;
using Warcraft.Core;
using Application = Gtk.Application;
using IOPath = System.IO.Path;
using FileNode = liblistfile.NodeTree.Node;

namespace Everlook.UI
{
	/// <summary>
	/// Main UI class for Everlook. The "partial" qualifier is not strictly needed, but prevents the compiler from
	/// generating errors about the autoconnected members that relate to UI elements.
	/// </summary>
	public sealed partial class MainWindow: Gtk.Window
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
		/// Whether or not the program is shutting down. This is used to remove callbacks and events.
		/// </summary>
		private bool IsShuttingDown;

		/// <summary>
		/// Task scheduler for the UI thread. This allows task-based code to have very simple UI callbacks.
		/// </summary>
		private readonly TaskScheduler UiThreadScheduler;

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
			this.DeleteEvent += OnDeleteEvent;

			this.UiThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();

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

			this.AboutButton.Clicked += OnAboutButtonClicked;
			this.PreferencesButton.Clicked += OnPreferencesButtonClicked;

			this.GameTabNotebook.ClearPages();

			foreach (GamePage gamePage in this.GamePages)
			{
				gamePage.FileLoadRequested += OnFileLoadRequested;
				gamePage.ExportItemRequested += OnExportItemRequested;
				gamePage.EnqueueFileExportRequested += OnEnqueueItemRequested;
			}

			this.ExportQueueTreeView.ButtonPressEvent += OnExportQueueButtonPressed;
			this.RemoveQueueItem.Activated += OnQueueRemoveContextItemActivated;

			/*
				Set up item control sections to default states
			*/

			EnableControlPage(ControlPage.None);
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
		/// Reloads visible runtime values that the user can change in the preferences, such as the colour
		/// of the viewport or the loaded packages.
		/// </summary>
		private void ReloadRuntimeValues()
		{
			this.ViewportWidget.OverrideBackgroundColor(StateFlags.Normal, this.Config.GetViewportBackgroundColour());
		}

		/// <summary>
		/// Begins loading routines for the specified fileReference, which is expected to point to a valid model.
		/// This function takes a delegate which will correctly load the file pointed to by the FileReference,
		/// and another delegate which will create a correct <see cref="IRenderable"/> object from the resulting
		/// object.
		/// </summary>
		/// <param name="fileReference">A <see cref="FileReference"/> which points to the desired file.</param>
		/// <param name="referenceLoadingRoutine">A delegate which correctly loads the desired file, returning a generic type T.</param>
		/// <param name="createRenderableDelegate">A delegate which accepts a generic type T and returns a renderable object.</param>
		/// <param name="associatedControlPage">The control page which the file is associated with, that is, the one with relevant controls.</param>
		/// <typeparam name="T">The type of model to load.</typeparam>
		private void BeginLoadingFile<T>(
			FileReference fileReference,
			DataLoadingDelegates.LoadReferenceDelegate<T> referenceLoadingRoutine,
			DataLoadingDelegates.CreateRenderableDelegate<T> createRenderableDelegate,
			ControlPage associatedControlPage)
		{
			Log.Info($"Loading \"{fileReference.FilePath}\".");

			this.StatusSpinner.Active = true;

			string modelName = fileReference.Filename;
			uint modelStatusMessageContextID = this.MainStatusBar.GetContextId($"itemLoad_{modelName}");
			uint modelStatusMessageID = this.MainStatusBar.Push(modelStatusMessageContextID,
				$"Loading \"{modelName}\"...");

			Task.Factory.StartNew(() => referenceLoadingRoutine(fileReference))
				.ContinueWith(modelLoadTask => createRenderableDelegate(modelLoadTask.Result, fileReference), this.UiThreadScheduler)
				.ContinueWith(createRenderableTask => this.RenderingEngine.SetRenderTarget(createRenderableTask.Result), this.UiThreadScheduler)
				.ContinueWith
				(
					result =>
					{
						this.StatusSpinner.Active = false;
						this.MainStatusBar.Remove(modelStatusMessageContextID, modelStatusMessageID);
						EnableControlPage(associatedControlPage);
					},
					this.UiThreadScheduler
				);
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
			if (args.Event.Type != EventType.ButtonPress || args.Event.Button != 3)
			{
				return;
			}

			// Hide the mouse pointer
			this.Window.Cursor = new Cursor(CursorType.BlankCursor);

			this.ViewportWidget.GrabFocus();

			this.RenderingEngine.WantsToMove = true;
			this.RenderingEngine.InitialMouseX = Mouse.GetCursorState().X;
			this.RenderingEngine.InitialMouseY = Mouse.GetCursorState().Y;
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
			if (args.Event.Type != EventType.ButtonRelease || args.Event.Button != 3)
			{
				return;
			}

			// Return the mouse pointer to its original appearance
			this.Window.Cursor = new Cursor(CursorType.Arrow);
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
		///
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
			// TODO: pixbuf for the export status
			this.ExportQueueListStore.AppendValues(fileReference, null);
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
		private void OnFileLoadRequested(GamePage page, FileReference fileReference)
		{
			switch (fileReference.GetReferencedFileType())
			{
				case WarcraftFileType.BinaryImage:
				{
					BeginLoadingFile(fileReference,
						DataLoadingRoutines.LoadBinaryImage,
						DataLoadingRoutines.CreateRenderableBinaryImage,
						ControlPage.Image);

					break;
				}
				case WarcraftFileType.WorldObjectModel:
				{
					BeginLoadingFile(fileReference,
						DataLoadingRoutines.LoadWorldModel,
						DataLoadingRoutines.CreateRenderableWorldModel,
						ControlPage.Model);

					break;
				}
				case WarcraftFileType.WorldObjectModelGroup:
				{
					BeginLoadingFile(fileReference,
						DataLoadingRoutines.LoadWorldModelGroup,
						DataLoadingRoutines.CreateRenderableWorldModel,
						ControlPage.Model);

					break;
				}
				case WarcraftFileType.GIFImage:
				case WarcraftFileType.PNGImage:
				case WarcraftFileType.JPGImage:
				{
					BeginLoadingFile(fileReference,
						DataLoadingRoutines.LoadBitmapImage,
						DataLoadingRoutines.CreateRenderableBitmapImage,
						ControlPage.Image);
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

			this.RenderingEngine.SetRenderTarget(null);
			this.RenderingEngine.Dispose();

			this.ViewportWidget.Destroy();

			Application.Quit();
			a.RetVal = true;
		}
	}
}