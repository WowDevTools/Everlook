//
//  MainWindow.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Everlook.Audio;
using Everlook.Configuration;
using Everlook.Explorer;
using Everlook.Package;
using Everlook.UI.Helpers;
using Everlook.UI.Widgets;
using Everlook.Utility;
using Everlook.Viewport;
using Everlook.Viewport.Rendering;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using FileTree.Tree.Serialized;
using Gdk;
using GLib;
using Gtk;
using log4net;
using OpenTK.Graphics;
using OpenTK.Input;
using Warcraft.Core;

using static Everlook.Utility.DataLoadingDelegates;

using Application = Gtk.Application;
using EventArgs = System.EventArgs;
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
        private readonly EverlookConfiguration _config = EverlookConfiguration.Instance;

        /// <summary>
        /// Background viewport renderer. Handles all rendering in the viewport.
        /// </summary>
        private readonly ViewportRenderer _renderingEngine;

        /// <summary>
        /// Task scheduler for the UI thread. This allows task-based code to have very simple UI callbacks.
        /// </summary>
        private readonly TaskScheduler _uiTaskScheduler;

        /// <summary>
        /// Whether or not the program is shutting down. This is used to remove callbacks and events.
        /// </summary>
        private bool _isShuttingDown;

        /// <summary>
        /// Cancellation token source for file loading operations.
        /// </summary>
        private CancellationTokenSource _fileLoadingCancellationSource;

        /// <summary>
        /// A single global audio source. Used for playing individual files.
        /// </summary>
        private AudioSource _globalAudio;

        /// <summary>
        /// Creates an instance of the <see cref="MainWindow"/> class, loading the glade XML UI as needed.
        /// </summary>
        /// <returns>An initialized instance of the MainWindow class.</returns>
        public static MainWindow Create()
        {
            using (var builder = new Builder(null, "Everlook.interfaces.Everlook.glade", null))
            {
                return new MainWindow(builder, builder.GetObject("MainWindow").Handle);
            }
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

            this._uiTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            this._fileLoadingCancellationSource = new CancellationTokenSource();

            var graphicsMode = new GraphicsMode
            (
                new ColorFormat(24),
                24,
                0,
                4,
                0,
                2,
                false
            );

            this._viewportWidget = new ViewportArea(graphicsMode, 3, 3, GraphicsContextFlags.Default)
            {
                AutoRender = true,
                CanFocus = true
            };

            this._viewportWidget.Events |=
                EventMask.ButtonPressMask |
                EventMask.ButtonReleaseMask |
                EventMask.EnterNotifyMask |
                EventMask.LeaveNotifyMask |
                EventMask.KeyPressMask |
                EventMask.KeyReleaseMask;

            this._viewportWidget.Initialized += (sender, args) =>
            {
                this._renderingEngine.Initialize();
            };

            this._viewportWidget.Render += (sender, args) =>
            {
                if (this._isShuttingDown)
                {
                    return;
                }

                if (!this._renderingEngine.IsInitialized)
                {
                    return;
                }

                this._renderingEngine.RenderFrame();

                this._viewportWidget.QueueRender();
            };

            this._viewportWidget.ButtonPressEvent += OnViewportButtonPressed;
            this._viewportWidget.ButtonReleaseEvent += OnViewportButtonReleased;
            this._viewportWidget.EnterNotifyEvent += OnViewportMouseEnter;
            this._viewportWidget.LeaveNotifyEvent += OnViewportMouseLeave;

            this._renderingEngine = new ViewportRenderer(this._viewportWidget);
            this._viewportAlignment.Add(this._viewportWidget);
            this._viewportAlignment.ShowAll();

            this._aboutButton.Clicked += OnAboutButtonClicked;
            this._preferencesButton.Clicked += OnPreferencesButtonClicked;

            this._gameTabNotebook.ClearPages();

            this._exportQueueTreeView.ButtonPressEvent += OnExportQueueButtonPressed;
            this._exportQueueTreeView.GetColumn(0).SetCellDataFunc
            (
                this._exportQueueTreeView.GetColumn(0).Cells[0],
                CellRenderers.RenderExportQueueReferenceIcon
            );

            this._exportQueueTreeView.GetColumn(0).Expand = true;
            this._exportQueueTreeView.GetColumn(0).SetCellDataFunc
            (
                this._exportQueueTreeView.GetColumn(0).Cells[1],
                CellRenderers.RenderExportQueueReferenceName
            );

            this._modelVariationComboBox.SetCellDataFunc
            (
                this._modelVariationTextRenderer,
                CellRenderers.RenderModelVariationName
            );

            this._removeQueueItem.Activated += OnQueueRemoveContextItemActivated;

            this._clearExportQueueButton.Clicked += OnClearExportQueueButtonClicked;
            this._runExportQueueButton.Clicked += OnRunExportQueueButtonClicked;

            this._fileFilterComboBox.Changed += OnFilterChanged;

            this._cancelCurrentActionButton.Clicked += OnCancelCurrentActionClicked;

            /*
                Set up item control sections to default states
            */

            EnableControlPage(ControlPage.None);

            /*
                Bind item control events
            */

            BindImageControlEvents();

            BindModelControlEvents();
        }

        /// <summary>
        /// Binds event handlers to the model control UI elements.
        /// </summary>
        private void BindModelControlEvents()
        {
            this._renderBoundsCheckButton.Toggled += (sender, args) =>
            {
                switch (this._renderingEngine.RenderTarget)
                {
                    case RenderableWorldModel wmo:
                    {
                        wmo.ShouldRenderBounds = this._renderBoundsCheckButton.Active;
                        break;
                    }
                    case RenderableGameModel mdx:
                    {
                        mdx.ShouldRenderBounds = this._renderBoundsCheckButton.Active;
                        break;
                    }
                }
            };

            this._renderWireframeCheckButton.Toggled += (sender, args) =>
            {
                switch (this._renderingEngine.RenderTarget)
                {
                    case RenderableWorldModel wmo:
                    {
                        wmo.ShouldRenderWireframe = this._renderWireframeCheckButton.Active;
                        break;
                    }
                    case RenderableGameModel mdx:
                    {
                        mdx.ShouldRenderWireframe = this._renderWireframeCheckButton.Active;
                        break;
                    }
                }
            };

            this._renderDoodadsCheckButton.Toggled += (sender, args) =>
            {
                switch (this._renderingEngine.RenderTarget)
                {
                    case RenderableWorldModel wmo:
                    {
                        wmo.ShouldRenderDoodads = this._renderDoodadsCheckButton.Active;
                        break;
                    }
                }

                this._modelVariationComboBox.Sensitive = this._renderDoodadsCheckButton.Active;
            };

            this._modelVariationComboBox.Changed += (sender, args) =>
            {
                switch (this._renderingEngine.RenderTarget)
                {
                    case RenderableWorldModel wmo:
                    {
                        this._modelVariationComboBox.GetActiveIter(out var activeIter);
                        var doodadSetName = (string)this._modelVariationListStore.GetValue(activeIter, 0);

                        wmo.DoodadSet = doodadSetName;
                        break;
                    }
                    case RenderableGameModel mdx:
                    {
                        this._modelVariationComboBox.GetActiveIter(out var activeIter);

                        var variationObject = this._modelVariationListStore.GetValue(activeIter, 1);
                        if (variationObject != null)
                        {
                            int variationID = (int)variationObject;

                            mdx.SetDisplayInfoByID(variationID);
                        }
                        break;
                    }
                }
            };
        }

        /// <summary>
        /// Binds event handlers to the image control UI elements.
        /// </summary>
        private void BindImageControlEvents()
        {
            this._renderAlphaCheckButton.Toggled += (sender, args) =>
            {
                RenderableImage image = this._renderingEngine.RenderTarget as RenderableImage;
                if (image == null)
                {
                    return;
                }

                image.RenderAlphaChannel = this._renderAlphaCheckButton.Active;
            };

            this._renderRedCheckButton.Toggled += (sender, args) =>
            {
                RenderableImage image = this._renderingEngine.RenderTarget as RenderableImage;
                if (image == null)
                {
                    return;
                }

                image.RenderRedChannel = this._renderRedCheckButton.Active;
            };

            this._renderGreenCheckButton.Toggled += (sender, args) =>
            {
                RenderableImage image = this._renderingEngine.RenderTarget as RenderableImage;
                if (image == null)
                {
                    return;
                }

                image.RenderGreenChannel = this._renderGreenCheckButton.Active;
            };

            this._renderBlueCheckButton.Toggled += (sender, args) =>
            {
                RenderableImage image = this._renderingEngine.RenderTarget as RenderableImage;
                if (image == null)
                {
                    return;
                }

                image.RenderBlueChannel = this._renderBlueCheckButton.Active;
            };
        }

        /// <summary>
        /// Runs the export queue.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="e">The event arguments.</param>
        [ConnectBefore]
        private void OnRunExportQueueButtonClicked(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clears the export queue.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="e">The event arguments.</param>
        [ConnectBefore]
        private void OnClearExportQueueButtonClicked(object sender, EventArgs e)
        {
            this._exportQueueListStore.Clear();
        }

        /// <summary>
        /// Handles cancelling the current topmost cancellable action in the UI.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="e">The event arguments.</param>
        [ConnectBefore]
        private void OnCancelCurrentActionClicked(object sender, EventArgs e)
        {
            this._fileLoadingCancellationSource.Cancel();
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
                this._viewportPaned.Position =
                    this._viewportPaned.AllocatedHeight +
                    this._lowerBoxPaned.AllocatedHeight +
                    (int)this._viewportAlignment.BottomPadding +
                    (int)this._lowerBoxAlignment.TopPadding;
            }
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
        /// Handles changing the cursor when hovering over the viewport.
        /// </summary>
        /// <param name="o">The sending object.</param>
        /// <param name="args">The event arguments.</param>
        [ConnectBefore]
        private void OnViewportMouseEnter(object o, EnterNotifyEventArgs args)
        {
            if (this._renderingEngine.RenderTarget?.Projection == ProjectionType.Orthographic)
            {
                this.Window.Cursor = new Cursor(CursorType.Hand2);
            }
        }

        /// <summary>
        /// Handles updating the filter state for the game pages when the user changes it in the UI.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="e">The event arguments.</param>
        private async void OnFilterChanged(object sender, EventArgs e)
        {
            await RefilterTrees();
        }

        /// <summary>
        /// Rerun the filtering operation on all loaded game trees.
        /// </summary>
        /// <returns>A task wrapping the refiltering of the loaded game trees.</returns>
        private async Task RefilterTrees()
        {
            ComboBox box = this._fileFilterComboBox;

            this._statusSpinner.Active = true;
            uint refilterStatusContextID = this._mainStatusBar.GetContextId("refreshFilter");
            uint refilterStatusMessageID = this._mainStatusBar.Push
            (
                refilterStatusContextID,
                "Refiltering node trees..."
            );

            // Disable the pages
            foreach (var page in this._gamePages)
            {
                page.SetTreeSensitivity(false);
            }

            FilterType filterType = (FilterType)box.Active;
            foreach (var page in this._gamePages)
            {
                page.ShouldDisplayUnknownFiles = this._config.ShowUnknownFilesWhenFiltering;

                if (filterType == FilterType.All)
                {
                    page.SetFilterState(false);
                    await page.RefilterAsync();
                }
                else
                {
                    page.SetFilterState(true);
                    page.SetFilter(filterType.GetFileTypeSet());

                    await page.RefilterAsync();
                }
            }

            // Reenable the pages
            foreach (var page in this._gamePages)
            {
                page.SetTreeSensitivity(true);
            }

            this._statusSpinner.Active = false;
            this._mainStatusBar.Remove(refilterStatusContextID, refilterStatusMessageID);
        }

        /// <summary>
        /// Performs any actions which should occur after the window is visible to the user.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="eventArgs">The event arguments.</param>
        private async void OnMainWindowShown(object sender, EventArgs eventArgs)
        {
            await LoadGames();
            //OnDeleteEvent(sender, null); // DEBUG
        }

        /// <summary>
        /// Loads the games stored in the preferences into the UI.
        /// </summary>
        private async Task LoadGames()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            GameLoader loader = new GameLoader();
            EverlookGameLoadingDialog dialog = EverlookGameLoadingDialog.Create(this);
            dialog.ShowAll();

            var loadingProgress = default(OverallLoadingProgress);
            loadingProgress.OperationCount = GamePathStorage.Instance.GamePaths.Count;
            int loadedGames = 0;

            foreach (var gameTarget in GamePathStorage.Instance.GamePaths)
            {
                loadedGames++;
                loadingProgress.FinishedOperations = loadedGames;
                dialog.OverallProgressNotifier.Report(loadingProgress);

                try
                {
                    (PackageGroup group, SerializedTree nodeTree) = await loader.LoadGameAsync
                    (
                        gameTarget.Alias,
                        gameTarget.Path,
                        dialog.CancellationSource.Token,
                        dialog.GameLoadProgressNotifier
                    );

                    AddGamePage(gameTarget.Alias, gameTarget.Version, group, nodeTree);
                }
                catch (OperationCanceledException)
                {
                    Log.Info("Cancelled game loading operation.");
                    break;
                }
            }

            dialog.Destroy();

            sw.Stop();
            Log.Debug($"Game loading took {sw.Elapsed.TotalMilliseconds}ms ({sw.Elapsed.TotalSeconds}s)");
        }

        private void AddGamePage(string alias, WarcraftVersion version, PackageGroup group, SerializedTree nodeTree)
        {
            GamePage page = new GamePage(group, nodeTree, version)
            {
                Alias = alias
            };

            page.FileLoadRequested += OnFileLoadRequested;
            page.SaveRequested += OnSaveRequested;
            page.ExportItemRequested += OnExportItemRequested;
            page.EnqueueFileExportRequested += OnEnqueueItemRequested;

            this._gamePages.Add(page);
            this._gameTabNotebook.AppendPage(page.PageWidget, new Label(page.Alias));
            this._gameTabNotebook.SetTabReorderable(page.PageWidget, true);

            this._gameTabNotebook.ShowAll();
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

            // Set the page
            this._itemControlNotebook.Page = (int)pageToEnable;

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
                    RenderableImage image = this._renderingEngine.RenderTarget as RenderableImage;
                    if (image == null)
                    {
                        return;
                    }

                    this._mipCountLabel.Text = image.MipCount.ToString();

                    this._renderAlphaCheckButton.Sensitive = true;
                    this._renderRedCheckButton.Sensitive = true;
                    this._renderGreenCheckButton.Sensitive = true;
                    this._renderBlueCheckButton.Sensitive = true;

                    image.RenderAlphaChannel = this._renderAlphaCheckButton.Active;
                    image.RenderRedChannel = this._renderRedCheckButton.Active;
                    image.RenderGreenChannel = this._renderGreenCheckButton.Active;
                    image.RenderBlueChannel = this._renderBlueCheckButton.Active;
                    break;
                }
                case ControlPage.Model:
                {
                    this._renderBoundsCheckButton.Sensitive = true;
                    this._renderWireframeCheckButton.Sensitive = true;
                    this._renderDoodadsCheckButton.Sensitive = true;

                    this._modelVariationComboBox.Sensitive = true;

                    if (this._renderingEngine.RenderTarget is RenderableWorldModel wmo)
                    {
                        wmo.ShouldRenderBounds = this._renderBoundsCheckButton.Active;
                        wmo.ShouldRenderWireframe = this._renderWireframeCheckButton.Active;
                        wmo.ShouldRenderDoodads = this._renderDoodadsCheckButton.Active;

                        var doodadSetNames = wmo.GetDoodadSetNames().ToList();
                        this._modelVariationListStore.Clear();
                        for (int i = 0; i < doodadSetNames.Count; ++i)
                        {
                            this._modelVariationListStore.AppendValues(doodadSetNames[i], i);
                        }

                        this._modelVariationComboBox.Active = 0;
                        this._modelVariationComboBox.Sensitive = this._renderDoodadsCheckButton.Active && doodadSetNames.Count > 1;
                        this._renderDoodadsCheckButton.Sensitive = true;
                    }

                    if (this._renderingEngine.RenderTarget is RenderableGameModel mdx)
                    {
                        mdx.ShouldRenderBounds = this._renderBoundsCheckButton.Active;
                        mdx.ShouldRenderWireframe = this._renderWireframeCheckButton.Active;

                        var skinVariations = mdx.GetSkinVariations().ToList();
                        this._modelVariationListStore.Clear();
                        foreach (var variation in skinVariations)
                        {
                            var firstTextureName = variation.TextureVariation1.Value;
                            if (!string.IsNullOrEmpty(firstTextureName))
                            {
                                this._modelVariationListStore.AppendValues(variation.TextureVariation1.Value, variation.ID);
                            }
                        }

                        this._modelVariationComboBox.Active = 0;
                        this._modelVariationComboBox.Sensitive = skinVariations.Count > 1;
                        this._renderDoodadsCheckButton.Sensitive = false;
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
                case ControlPage.None:
                {
                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(pageToEnable));
                }
            }
        }

        /// <summary>
        /// Disables the specified control page.
        /// </summary>
        /// <param name="pageToDisable">pageToEnable.</param>
        private void DisableControlPage(ControlPage pageToDisable)
        {
            switch (pageToDisable)
            {
                case ControlPage.Image:
                {
                    this._renderAlphaCheckButton.Sensitive = false;
                    this._renderRedCheckButton.Sensitive = false;
                    this._renderGreenCheckButton.Sensitive = false;
                    this._renderBlueCheckButton.Sensitive = false;
                    break;
                }
                case ControlPage.Model:
                {
                    this._renderBoundsCheckButton.Sensitive = false;
                    this._renderWireframeCheckButton.Sensitive = false;
                    this._renderDoodadsCheckButton.Sensitive = false;

                    this._modelVariationListStore.Clear();
                    this._modelVariationComboBox.Sensitive = false;
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
                case ControlPage.None:
                {
                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(pageToDisable));
                }
            }
        }

        /// <summary>
        /// Clears the loaded games and loads new ones from the configuration.
        /// </summary>
        /// <returns>A task wrapping the game reload operation.</returns>
        private Task ReloadGames()
        {
            this._gamePages.Clear();
            this._gameTabNotebook.ClearPages();
            return LoadGames();
        }

        /// <summary>
        /// Reloads the viewport background colour from the configuration.
        /// </summary>
        private void ReloadViewportBackground()
        {
            this._viewportWidget.MakeCurrent();

            this._renderingEngine.SetClearColour(this._config.ViewportBackgroundColour);
            this._viewportWidget.QueueRender();
        }

        /// <summary>
        /// Loads and displays the specified fileReference in the UI, which is expected to point to a valid object.
        /// This function takes a delegate which will correctly load the file pointed to by the FileReference,
        /// and another delegate which will create a correct <see cref="IRenderable"/> object from the resulting
        /// object.
        /// </summary>
        /// <param name="fileReference">A <see cref="FileReference"/> which points to the desired file.</param>
        /// <param name="referenceLoadingRoutine">A delegate which correctly loads the desired file, returning a generic type T.</param>
        /// <param name="createRenderable">A delegate which accepts a generic type T and returns a renderable object.</param>
        /// <param name="associatedControlPage">The control page which the file is associated with, that is, the one with relevant controls.</param>
        /// <param name="ct">A cancellation token for this operation.</param>
        /// <typeparam name="T">The type of object to load.</typeparam>
        private async Task DisplayRenderableFile<T>(FileReference fileReference, LoadReference<T> referenceLoadingRoutine, CreateRenderable<T> createRenderable, ControlPage associatedControlPage, CancellationToken ct)
        {
            if (fileReference == null)
            {
                throw new ArgumentNullException(nameof(fileReference));
            }

            Log.Info($"Loading \"{fileReference.FilePath}\".");

            this._statusSpinner.Active = true;

            string modelName = fileReference.Filename;
            uint modelStatusMessageContextID = this._mainStatusBar.GetContextId($"itemLoad_{modelName}");
            uint modelStatusMessageID = this._mainStatusBar.Push
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

                IRenderable renderable = await Task.Run
                (
                    () => createRenderable(item, fileReference),
                    ct
                );

                if (renderable != null)
                {
                    ct.ThrowIfCancellationRequested();

                    this._viewportWidget.MakeCurrent();
                    this._viewportWidget.AttachBuffers();
                    renderable.Initialize();

                    this._renderingEngine.SetRenderTarget(renderable);

                    EnableControlPage(associatedControlPage);

                    if (renderable is IModelInfoProvider infoProvider)
                    {
                        this._polyCountLabel.Text = infoProvider.PolygonCount.ToString();
                        this._vertexCountLabel.Text = infoProvider.VertexCount.ToString();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Info($"Cancelled loading of {fileReference.Filename}");
            }
            finally
            {
                this._statusSpinner.Active = false;
                this._mainStatusBar.Remove(modelStatusMessageContextID, modelStatusMessageID);
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
            if (this._renderingEngine.IsMovementDisabled())
            {
                return;
            }

            if (args.Event.Type != EventType.ButtonPress)
            {
                return;
            }

            bool validButtonIsPressed = false;
            if (this._renderingEngine.RenderTarget.Projection == ProjectionType.Perspective)
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

            this._viewportWidget.GrabFocus();

            this._renderingEngine.InitialMouseX = Mouse.GetCursorState().X;
            this._renderingEngine.InitialMouseY = Mouse.GetCursorState().Y;

            this._renderingEngine.WantsToMove = true;
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
            if (this._renderingEngine.RenderTarget?.Projection == ProjectionType.Perspective)
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
            this._renderingEngine.WantsToMove = false;
        }

        /// <summary>
        /// Handles the export item context item activated event.
        /// </summary>
        /// <param name="page">Sender.</param>
        /// <param name="fileReference">E.</param>
        private static Task OnExportItemRequested(GamePage page, FileReference fileReference)
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
                    using (var exportDialog = EverlookImageExportDialog.Create(fileReference))
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

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles queueing of a selected file in the archive, triggered by a context
        /// menu press.
        /// </summary>
        /// <param name="page">Sender.</param>
        /// <param name="fileReference">E.</param>
        private Task OnEnqueueItemRequested(GamePage page, FileReference fileReference)
        {
            this._exportQueueListStore.AppendValues(fileReference, IconManager.GetIcon("package-downgrade"));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Displays the About dialog to the user.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        private void OnAboutButtonClicked(object sender, EventArgs e)
        {
            this._aboutDialog.Run();
            this._aboutDialog.Hide();
        }

        /// <summary>
        /// Displays the preferences dialog to the user.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        private async void OnPreferencesButtonClicked(object sender, EventArgs e)
        {
            using (var preferencesDialog = EverlookPreferences.Create())
            {
                preferencesDialog.TransientFor = this;
                if (preferencesDialog.Run() == (int)ResponseType.Ok)
                {
                    preferencesDialog.SavePreferences();
                }

                preferencesDialog.Destroy();

                // Commit the changes
                ReloadViewportBackground();

                if (preferencesDialog.DidGameListChange)
                {
                    await ReloadGames();
                }

                if (preferencesDialog.ShouldRefilterTree)
                {
                    await RefilterTrees();
                }
            }
        }

        /// <summary>
        /// Handles saving of a set of files to disk.
        /// </summary>
        /// <param name="page">The <see cref="GamePage"/> in which the event originated.</param>
        /// <param name="fileReferences">The file references to save.</param>
        private async Task OnSaveRequested(GamePage page, IEnumerable<FileReference> fileReferences)
        {
            uint statusMessageContextID = this._mainStatusBar.GetContextId($"itemLoad_{fileReferences.GetHashCode()}");

            foreach (var fileReference in fileReferences)
            {
                this._statusSpinner.Active = true;

                uint statusMessageID = this._mainStatusBar.Push
                (
                    statusMessageContextID,
                    $"Saving \"{fileReference.Filename}\"..."
                );

                string cleanFilepath = fileReference.FilePath.ConvertPathSeparatorsToCurrentNativeSeparator();

                string exportpath;
                if (this._config.KeepFileDirectoryStructure)
                {
                    exportpath = IOPath.Combine(this._config.DefaultExportDirectory, cleanFilepath);
                    Directory.CreateDirectory(Directory.GetParent(exportpath).FullName);
                }
                else
                {
                    string filename = IOPath.GetFileName(cleanFilepath);
                    exportpath = IOPath.Combine(this._config.DefaultExportDirectory, filename);
                    Directory.CreateDirectory(Directory.GetParent(exportpath).FullName);
                }

                byte[] file = await fileReference.ExtractAsync();
                if (file != null)
                {
                    try
                    {
                        if (File.Exists(exportpath))
                        {
                            File.Delete(exportpath);
                        }

                        using (var fs = new FileStream(exportpath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            await fs.WriteAsync(file, 0, file.Length);
                        }
                    }
                    catch (UnauthorizedAccessException unex)
                    {
                        Log.Warn($"Failed to save \"{fileReference.Filename}\": {unex}");
                    }
                    catch (IOException iex)
                    {
                        Log.Warn($"Failed to save \"{fileReference.Filename}\": {iex}");
                    }
                }
                else
                {
                    Log.Warn($"Failed to save \"{fileReference.Filename}\": Could not extract any data from the archives.");
                }

                this._mainStatusBar.Remove(statusMessageContextID, statusMessageID);
            }

            this._statusSpinner.Active = false;
        }

        /// <summary>
        /// Handles selection of files in the game explorer, displaying them to the user and routing
        /// whatever rendering functionality the file needs to the viewport.
        /// </summary>
        /// <param name="page">The <see cref="GamePage"/> in which the event originated.</param>
        /// <param name="fileReference">The file reference to load.</param>
        private async Task OnFileLoadRequested(GamePage page, FileReference fileReference)
        {
            WarcraftFileType referencedType = fileReference.GetReferencedFileType();

            switch (referencedType)
            {
                case WarcraftFileType.BinaryImage:
                {
                    this._fileLoadingCancellationSource.Cancel();
                    this._fileLoadingCancellationSource = new CancellationTokenSource();

                    await DisplayRenderableFile
                    (
                        fileReference,
                        DataLoadingRoutines.LoadBinaryImage,
                        DataLoadingRoutines.CreateRenderableBinaryImage,
                        ControlPage.Image,
                        this._fileLoadingCancellationSource.Token
                    );

                    break;
                }
                case WarcraftFileType.WorldObjectModel:
                {
                    this._fileLoadingCancellationSource.Cancel();
                    this._fileLoadingCancellationSource = new CancellationTokenSource();

                    await DisplayRenderableFile
                    (
                        fileReference,
                        DataLoadingRoutines.LoadWorldModel,
                        DataLoadingRoutines.CreateRenderableWorldModel,
                        ControlPage.Model,
                        this._fileLoadingCancellationSource.Token
                    );

                    break;
                }
                case WarcraftFileType.WorldObjectModelGroup:
                {
                    this._fileLoadingCancellationSource.Cancel();
                    this._fileLoadingCancellationSource = new CancellationTokenSource();

                    await DisplayRenderableFile
                    (
                        fileReference,
                        DataLoadingRoutines.LoadWorldModelGroup,
                        DataLoadingRoutines.CreateRenderableWorldModel,
                        ControlPage.Model,
                        this._fileLoadingCancellationSource.Token
                    );

                    break;
                }
                case WarcraftFileType.GIFImage:
                case WarcraftFileType.PNGImage:
                case WarcraftFileType.JPGImage:
                {
                    this._fileLoadingCancellationSource.Cancel();
                    this._fileLoadingCancellationSource = new CancellationTokenSource();

                    await DisplayRenderableFile
                    (
                        fileReference,
                        DataLoadingRoutines.LoadBitmapImage,
                        DataLoadingRoutines.CreateRenderableBitmapImage,
                        ControlPage.Image,
                        this._fileLoadingCancellationSource.Token
                    );

                    break;
                }
                case WarcraftFileType.WaveAudio:
                case WarcraftFileType.MP3Audio:
                {
                    AudioManager.UnregisterSource(this._globalAudio);

                    if (this._config.AutoplayAudioFiles)
                    {
                        this._globalAudio = AudioSource.CreateNew();
                        await this._globalAudio.SetAudioAsync(fileReference);

                        AudioManager.RegisterSource(this._globalAudio);

                        this._globalAudio.Play();
                    }

                    break;
                }
                case WarcraftFileType.GameObjectModel:
                {
                    this._fileLoadingCancellationSource.Cancel();
                    this._fileLoadingCancellationSource = new CancellationTokenSource();

                    await DisplayRenderableFile
                    (
                        fileReference,
                        DataLoadingRoutines.LoadGameModel,
                        DataLoadingRoutines.CreateRenderableGameModel,
                        ControlPage.Model,
                        this._fileLoadingCancellationSource.Token
                    );

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

            this._exportQueueTreeView.GetPathAtPos((int)e.Event.X, (int)e.Event.Y, out var path);

            if (path == null)
            {
                return;
            }

            this._exportQueueListStore.GetIter(out var iter, path);

            FileReference queuedReference = (FileReference)this._exportQueueListStore.GetValue(iter, 0);

            if (string.IsNullOrEmpty(queuedReference?.FilePath))
            {
                this._removeQueueItem.Sensitive = false;
            }
            else
            {
                this._removeQueueItem.Sensitive = true;
            }

            this._queueContextMenu.ShowAll();
            this._queueContextMenu.PopupForDevice(e.Event.Device, null, null, null, null, e.Event.Button, e.Event.Time);
            //this.QueueContextMenu.Popup();
        }

        /// <summary>
        /// Handles removal of items from the export queue, triggered by a context menu press.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        private void OnQueueRemoveContextItemActivated(object sender, EventArgs e)
        {
            this._exportQueueTreeView.Selection.GetSelected(out var selectedIter);
            this._exportQueueListStore.Remove(ref selectedIter);
        }

        /// <summary>
        /// Handles application shutdown procedures - terminating render threads, cleaning
        /// up the UI, etc.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="a">The alpha component.</param>
        private void OnDeleteEvent(object sender, DeleteEventArgs a)
        {
            this._isShuttingDown = true;

            this._fileLoadingCancellationSource?.Cancel();

            this._viewportWidget.MakeCurrent();

            this._renderingEngine.SetRenderTarget(null);
            this._renderingEngine.Dispose();

            RenderCache.Instance?.Dispose();

            this._viewportWidget.Dispose();

            Application.Quit();
            a.RetVal = true;
        }
    }
}
