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
        private AudioSource? _globalAudio;

        /// <summary>
        /// Creates an instance of the <see cref="MainWindow"/> class, loading the glade XML UI as needed.
        /// </summary>
        /// <returns>An initialized instance of the MainWindow class.</returns>
        public static MainWindow Create()
        {
            using (var builder = new Builder(null, "Everlook.interfaces.Everlook.glade", null))
            {
                return new MainWindow(builder, builder.GetObject("_mainWindow").Handle);
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

            _uiTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            _fileLoadingCancellationSource = new CancellationTokenSource();

            _viewportWidget = new ViewportArea(3, 3)
            {
                AutoRender = true,
                CanFocus = true
            };

            _viewportWidget.Events |=
                EventMask.ButtonPressMask |
                EventMask.ButtonReleaseMask |
                EventMask.EnterNotifyMask |
                EventMask.LeaveNotifyMask |
                EventMask.KeyPressMask |
                EventMask.KeyReleaseMask;

            _viewportWidget.Initialized += (sender, args) =>
            {
                _renderingEngine.Initialize();
            };

            _viewportWidget.Render += (sender, args) =>
            {
                if (_isShuttingDown)
                {
                    return;
                }

                if (!_renderingEngine.IsInitialized)
                {
                    return;
                }

                _renderingEngine.RenderFrame();

                _viewportWidget.QueueRender();
            };

            _viewportWidget.ButtonPressEvent += OnViewportButtonPressed;
            _viewportWidget.ButtonReleaseEvent += OnViewportButtonReleased;
            _viewportWidget.EnterNotifyEvent += OnViewportMouseEnter;
            _viewportWidget.LeaveNotifyEvent += OnViewportMouseLeave;

            _renderingEngine = new ViewportRenderer(_viewportWidget);
            _viewportAlignment.Add(_viewportWidget);
            _viewportAlignment.ShowAll();

            _aboutButton.Clicked += OnAboutButtonClicked;
            _preferencesButton.Clicked += OnPreferencesButtonClicked;

            _gameTabNotebook.ClearPages();

            _exportQueueTreeView.ButtonPressEvent += OnExportQueueButtonPressed;
            _exportQueueTreeView.GetColumn(0).SetCellDataFunc
            (
                _exportQueueTreeView.GetColumn(0).Cells[0],
                CellRenderers.RenderExportQueueReferenceIcon
            );

            _exportQueueTreeView.GetColumn(0).Expand = true;
            _exportQueueTreeView.GetColumn(0).SetCellDataFunc
            (
                _exportQueueTreeView.GetColumn(0).Cells[1],
                CellRenderers.RenderExportQueueReferenceName
            );

            _modelVariationComboBox.SetCellDataFunc
            (
                _modelVariationTextRenderer,
                CellRenderers.RenderModelVariationName
            );

            _removeQueueItem.Activated += OnQueueRemoveContextItemActivated;

            _clearExportQueueButton.Clicked += OnClearExportQueueButtonClicked;
            _runExportQueueButton.Clicked += OnRunExportQueueButtonClicked;

            _fileFilterComboBox.Changed += OnFilterChanged;

            _cancelCurrentActionButton.Clicked += OnCancelCurrentActionClicked;

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
            _renderBoundsCheckButton.Toggled += (sender, args) =>
            {
                switch (_renderingEngine.RenderTarget)
                {
                    case RenderableWorldModel wmo:
                    {
                        wmo.ShouldRenderBounds = _renderBoundsCheckButton.Active;
                        break;
                    }
                    case RenderableGameModel mdx:
                    {
                        mdx.ShouldRenderBounds = _renderBoundsCheckButton.Active;
                        break;
                    }
                }
            };

            _renderWireframeCheckButton.Toggled += (sender, args) =>
            {
                switch (_renderingEngine.RenderTarget)
                {
                    case RenderableWorldModel wmo:
                    {
                        wmo.ShouldRenderWireframe = _renderWireframeCheckButton.Active;
                        break;
                    }
                    case RenderableGameModel mdx:
                    {
                        mdx.ShouldRenderWireframe = _renderWireframeCheckButton.Active;
                        break;
                    }
                }
            };

            _renderDoodadsCheckButton.Toggled += (sender, args) =>
            {
                switch (_renderingEngine.RenderTarget)
                {
                    case RenderableWorldModel wmo:
                    {
                        wmo.ShouldRenderDoodads = _renderDoodadsCheckButton.Active;
                        break;
                    }
                }

                _modelVariationComboBox.Sensitive = _renderDoodadsCheckButton.Active;
            };

            _modelVariationComboBox.Changed += (sender, args) =>
            {
                switch (_renderingEngine.RenderTarget)
                {
                    case RenderableWorldModel wmo:
                    {
                        _modelVariationComboBox.GetActiveIter(out var activeIter);
                        var doodadSetName = (string)_modelVariationListStore.GetValue(activeIter, 0);

                        wmo.DoodadSet = doodadSetName;
                        break;
                    }
                    case RenderableGameModel mdx:
                    {
                        _modelVariationComboBox.GetActiveIter(out var activeIter);

                        var variationObject = _modelVariationListStore.GetValue(activeIter, 1);
                        if (variationObject != null)
                        {
                            var variationID = (int)variationObject;

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
            _renderAlphaCheckButton.Toggled += (sender, args) =>
            {
                var image = _renderingEngine.RenderTarget as RenderableImage;
                if (image == null)
                {
                    return;
                }

                image.RenderAlphaChannel = _renderAlphaCheckButton.Active;
            };

            _renderRedCheckButton.Toggled += (sender, args) =>
            {
                var image = _renderingEngine.RenderTarget as RenderableImage;
                if (image == null)
                {
                    return;
                }

                image.RenderRedChannel = _renderRedCheckButton.Active;
            };

            _renderGreenCheckButton.Toggled += (sender, args) =>
            {
                var image = _renderingEngine.RenderTarget as RenderableImage;
                if (image == null)
                {
                    return;
                }

                image.RenderGreenChannel = _renderGreenCheckButton.Active;
            };

            _renderBlueCheckButton.Toggled += (sender, args) =>
            {
                var image = _renderingEngine.RenderTarget as RenderableImage;
                if (image == null)
                {
                    return;
                }

                image.RenderBlueChannel = _renderBlueCheckButton.Active;
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
            _exportQueueListStore.Clear();
        }

        /// <summary>
        /// Handles cancelling the current topmost cancellable action in the UI.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="e">The event arguments.</param>
        [ConnectBefore]
        private void OnCancelCurrentActionClicked(object sender, EventArgs e)
        {
            _fileLoadingCancellationSource.Cancel();
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
                _viewportPaned.Position =
                    _viewportPaned.AllocatedHeight +
                    _lowerBoxPaned.AllocatedHeight +
                    (int)_viewportAlignment.BottomPadding +
                    (int)_lowerBoxAlignment.TopPadding;
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
            if (_renderingEngine.RenderTarget?.Projection == ProjectionType.Orthographic)
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
            var box = _fileFilterComboBox;

            _statusSpinner.Active = true;
            var refilterStatusContextID = _mainStatusBar.GetContextId("refreshFilter");
            var refilterStatusMessageID = _mainStatusBar.Push
            (
                refilterStatusContextID,
                "Refiltering node trees..."
            );

            // Disable the pages
            foreach (var page in _gamePages)
            {
                page.SetTreeSensitivity(false);
            }

            var filterType = (FilterType)box.Active;
            foreach (var page in _gamePages)
            {
                page.ShouldDisplayUnknownFiles = _config.ShowUnknownFilesWhenFiltering;

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
            foreach (var page in _gamePages)
            {
                page.SetTreeSensitivity(true);
            }

            _statusSpinner.Active = false;
            _mainStatusBar.Remove(refilterStatusContextID, refilterStatusMessageID);
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
            var sw = new Stopwatch();
            sw.Start();

            var loader = new GameLoader();
            var dialog = EverlookGameLoadingDialog.Create(this);
            dialog.ShowAll();

            var loadingProgress = default(OverallLoadingProgress);
            loadingProgress.OperationCount = GamePathStorage.Instance.GamePaths.Count;
            var loadedGames = 0;

            foreach (var gameTarget in GamePathStorage.Instance.GamePaths)
            {
                loadedGames++;
                loadingProgress.FinishedOperations = loadedGames;
                dialog.OverallProgressNotifier.Report(loadingProgress);

                if (!Directory.Exists(gameTarget.Path))
                {
                    Log.Warn($"Could not find game folder for {gameTarget.Alias}. Has the directory moved?");
                    continue;
                }

                try
                {
                    (var group, var nodeTree) = await loader.LoadGameAsync
                    (
                        gameTarget.Alias,
                        gameTarget.Path,
                        dialog.CancellationSource.Token,
                        dialog.GameLoadProgressNotifier
                    );

                    if (group is null || nodeTree is null)
                    {
                        continue;
                    }

                    AddGamePage(gameTarget.Alias, gameTarget.Version, group, nodeTree);
                }
                catch (OperationCanceledException)
                {
                    Log.Info("Cancelled game loading operation.");
                    break;
                }
            }

            dialog.Hide();
            dialog.Destroy();

            sw.Stop();
            Log.Debug($"Game loading took {sw.Elapsed.TotalMilliseconds}ms ({sw.Elapsed.TotalSeconds}s)");
        }

        private void AddGamePage(string alias, WarcraftVersion version, PackageGroup group, SerializedTree nodeTree)
        {
            var page = new GamePage(group, nodeTree, version, alias);

            page.FileLoadRequested += OnFileLoadRequested;
            page.SaveRequested += OnSaveRequested;
            page.ExportItemRequested += OnExportItemRequested;
            page.EnqueueFileExportRequested += OnEnqueueItemRequested;

            _gamePages.Add(page);
            _gameTabNotebook.AppendPage(page.PageWidget, new Label(page.Alias));
            _gameTabNotebook.SetTabReorderable(page.PageWidget, true);

            _gameTabNotebook.ShowAll();
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
            _itemControlNotebook.Page = (int)pageToEnable;

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
                    var image = _renderingEngine.RenderTarget as RenderableImage;
                    if (image == null)
                    {
                        return;
                    }

                    _mipCountLabel.Text = image.MipCount.ToString();

                    _renderAlphaCheckButton.Sensitive = true;
                    _renderRedCheckButton.Sensitive = true;
                    _renderGreenCheckButton.Sensitive = true;
                    _renderBlueCheckButton.Sensitive = true;

                    image.RenderAlphaChannel = _renderAlphaCheckButton.Active;
                    image.RenderRedChannel = _renderRedCheckButton.Active;
                    image.RenderGreenChannel = _renderGreenCheckButton.Active;
                    image.RenderBlueChannel = _renderBlueCheckButton.Active;
                    break;
                }
                case ControlPage.Model:
                {
                    _renderBoundsCheckButton.Sensitive = true;
                    _renderWireframeCheckButton.Sensitive = true;
                    _renderDoodadsCheckButton.Sensitive = true;

                    _modelVariationComboBox.Sensitive = true;

                    if (_renderingEngine.RenderTarget is RenderableWorldModel wmo)
                    {
                        wmo.ShouldRenderBounds = _renderBoundsCheckButton.Active;
                        wmo.ShouldRenderWireframe = _renderWireframeCheckButton.Active;
                        wmo.ShouldRenderDoodads = _renderDoodadsCheckButton.Active;

                        var doodadSetNames = wmo.GetDoodadSetNames().ToList();
                        _modelVariationListStore.Clear();
                        for (var i = 0; i < doodadSetNames.Count; ++i)
                        {
                            _modelVariationListStore.AppendValues(doodadSetNames[i], i);
                        }

                        _modelVariationComboBox.Active = 0;
                        _modelVariationComboBox.Sensitive = _renderDoodadsCheckButton.Active &&
                                                            doodadSetNames.Count > 1;
                        _renderDoodadsCheckButton.Sensitive = true;
                    }

                    if (_renderingEngine.RenderTarget is RenderableGameModel mdx)
                    {
                        mdx.ShouldRenderBounds = _renderBoundsCheckButton.Active;
                        mdx.ShouldRenderWireframe = _renderWireframeCheckButton.Active;

                        var skinVariations = mdx.GetSkinVariations().ToList();
                        _modelVariationListStore.Clear();
                        foreach (var variation in skinVariations)
                        {
                            var firstTextureName = variation.TextureVariation1.Value;
                            if (!string.IsNullOrEmpty(firstTextureName))
                            {
                                _modelVariationListStore.AppendValues(variation.TextureVariation1.Value, variation.ID);
                            }
                        }

                        _modelVariationComboBox.Active = 0;
                        _modelVariationComboBox.Sensitive = skinVariations.Count > 1;
                        _renderDoodadsCheckButton.Sensitive = false;
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
                    _renderAlphaCheckButton.Sensitive = false;
                    _renderRedCheckButton.Sensitive = false;
                    _renderGreenCheckButton.Sensitive = false;
                    _renderBlueCheckButton.Sensitive = false;
                    break;
                }
                case ControlPage.Model:
                {
                    _renderBoundsCheckButton.Sensitive = false;
                    _renderWireframeCheckButton.Sensitive = false;
                    _renderDoodadsCheckButton.Sensitive = false;

                    _modelVariationListStore.Clear();
                    _modelVariationComboBox.Sensitive = false;
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
            _gamePages.Clear();
            _gameTabNotebook.ClearPages();
            return LoadGames();
        }

        /// <summary>
        /// Reloads the viewport background colour from the configuration.
        /// </summary>
        private void ReloadViewportBackground()
        {
            _viewportWidget.MakeCurrent();

            _renderingEngine.SetClearColour(_config.ViewportBackgroundColour);
            _viewportWidget.QueueRender();
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
        private async Task DisplayRenderableFile<T>
        (
            FileReference fileReference,
            LoadReference<T> referenceLoadingRoutine,
            CreateRenderable<T> createRenderable,
            ControlPage associatedControlPage,
            CancellationToken ct
        )
        {
            if (fileReference == null)
            {
                throw new ArgumentNullException(nameof(fileReference));
            }

            Log.Info($"Loading \"{fileReference.FilePath}\".");

            _statusSpinner.Active = true;

            var modelName = fileReference.Filename;
            var modelStatusMessageContextID = _mainStatusBar.GetContextId($"itemLoad_{modelName}");
            var modelStatusMessageID = _mainStatusBar.Push
            (
                modelStatusMessageContextID,
                $"Loading \"{modelName}\"..."
            );

            try
            {
                var item = await Task.Run
                (
                    () => referenceLoadingRoutine(fileReference),
                    ct
                );

                var renderable = await Task.Run
                (
                    () => createRenderable(item, fileReference),
                    ct
                );

                if (renderable != null)
                {
                    ct.ThrowIfCancellationRequested();

                    _viewportWidget.MakeCurrent();
                    _viewportWidget.AttachBuffers();
                    renderable.Initialize();

                    _renderingEngine.SetRenderTarget(renderable);

                    EnableControlPage(associatedControlPage);

                    if (renderable is IModelInfoProvider infoProvider)
                    {
                        _polyCountLabel.Text = infoProvider.PolygonCount.ToString();
                        _vertexCountLabel.Text = infoProvider.VertexCount.ToString();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Info($"Cancelled loading of {fileReference.Filename}");
            }
            finally
            {
                _statusSpinner.Active = false;
                _mainStatusBar.Remove(modelStatusMessageContextID, modelStatusMessageID);
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
            if (_renderingEngine.IsMovementDisabled())
            {
                return;
            }

            if (args.Event.Type != EventType.ButtonPress)
            {
                return;
            }

            var validButtonIsPressed = false;
            if (_renderingEngine.HasRenderTarget && _renderingEngine.RenderTarget!.Projection == ProjectionType.Perspective)
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

            _viewportWidget.GrabFocus();

            _renderingEngine.InitialMouseX = args.Event.XRoot;
            _renderingEngine.InitialMouseY = args.Event.YRoot;

            _renderingEngine.WantsToMove = true;
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

            var validButtonIsPressed = false;
            if (_renderingEngine.RenderTarget?.Projection == ProjectionType.Perspective)
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
            _renderingEngine.WantsToMove = false;
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
            _exportQueueListStore.AppendValues(fileReference, IconManager.GetIcon("package-downgrade"));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Displays the About dialog to the user.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        private void OnAboutButtonClicked(object sender, EventArgs e)
        {
            _aboutDialog.Run();
            _aboutDialog.Hide();
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
            var statusMessageContextID = _mainStatusBar.GetContextId($"itemLoad_{fileReferences.GetHashCode()}");

            foreach (var fileReference in fileReferences)
            {
                _statusSpinner.Active = true;

                var statusMessageID = _mainStatusBar.Push
                (
                    statusMessageContextID,
                    $"Saving \"{fileReference.Filename}\"..."
                );

                var cleanFilepath = fileReference.FilePath.ConvertPathSeparatorsToCurrentNativeSeparator();

                string exportpath;
                if (_config.KeepFileDirectoryStructure)
                {
                    exportpath = IOPath.Combine(_config.DefaultExportDirectory, cleanFilepath);
                    Directory.CreateDirectory(Directory.GetParent(exportpath).FullName);
                }
                else
                {
                    var filename = IOPath.GetFileName(cleanFilepath);
                    exportpath = IOPath.Combine(_config.DefaultExportDirectory, filename);
                    Directory.CreateDirectory(Directory.GetParent(exportpath).FullName);
                }

                var file = await fileReference.ExtractAsync();
                if (file != null)
                {
                    try
                    {
                        if (File.Exists(exportpath))
                        {
                            File.Delete(exportpath);
                        }

                        using
                        (
                            var fs = new FileStream(exportpath, FileMode.CreateNew, FileAccess.Write, FileShare.None)
                        )
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
                    Log.Warn
                    (
                        $"Failed to save \"{fileReference.Filename}\": Could not extract any data from the archives."
                    );
                }

                _mainStatusBar.Remove(statusMessageContextID, statusMessageID);
            }

            _statusSpinner.Active = false;
        }

        /// <summary>
        /// Handles selection of files in the game explorer, displaying them to the user and routing
        /// whatever rendering functionality the file needs to the viewport.
        /// </summary>
        /// <param name="page">The <see cref="GamePage"/> in which the event originated.</param>
        /// <param name="fileReference">The file reference to load.</param>
        private async Task OnFileLoadRequested(GamePage page, FileReference fileReference)
        {
            var referencedType = fileReference.GetReferencedFileType();

            switch (referencedType)
            {
                case WarcraftFileType.BinaryImage:
                {
                    _fileLoadingCancellationSource.Cancel();
                    _fileLoadingCancellationSource = new CancellationTokenSource();

                    await DisplayRenderableFile
                    (
                        fileReference,
                        DataLoadingRoutines.LoadBinaryImage,
                        DataLoadingRoutines.CreateRenderableBinaryImage,
                        ControlPage.Image,
                        _fileLoadingCancellationSource.Token
                    );

                    break;
                }
                case WarcraftFileType.WorldObjectModel:
                {
                    _fileLoadingCancellationSource.Cancel();
                    _fileLoadingCancellationSource = new CancellationTokenSource();

                    await DisplayRenderableFile
                    (
                        fileReference,
                        DataLoadingRoutines.LoadWorldModel,
                        DataLoadingRoutines.CreateRenderableWorldModel,
                        ControlPage.Model,
                        _fileLoadingCancellationSource.Token
                    );

                    break;
                }
                case WarcraftFileType.WorldObjectModelGroup:
                {
                    _fileLoadingCancellationSource.Cancel();
                    _fileLoadingCancellationSource = new CancellationTokenSource();

                    await DisplayRenderableFile
                    (
                        fileReference,
                        DataLoadingRoutines.LoadWorldModelGroup,
                        DataLoadingRoutines.CreateRenderableWorldModel,
                        ControlPage.Model,
                        _fileLoadingCancellationSource.Token
                    );

                    break;
                }
                case WarcraftFileType.GIFImage:
                case WarcraftFileType.PNGImage:
                case WarcraftFileType.JPGImage:
                {
                    _fileLoadingCancellationSource.Cancel();
                    _fileLoadingCancellationSource = new CancellationTokenSource();

                    await DisplayRenderableFile
                    (
                        fileReference,
                        DataLoadingRoutines.LoadBitmapImage,
                        DataLoadingRoutines.CreateRenderableBitmapImage,
                        ControlPage.Image,
                        _fileLoadingCancellationSource.Token
                    );

                    break;
                }
                case WarcraftFileType.WaveAudio:
                case WarcraftFileType.MP3Audio:
                {
                    if (_globalAudio is null)
                    {
                        return;
                    }

                    AudioManager.UnregisterSource(_globalAudio);

                    if (_config.AutoplayAudioFiles)
                    {
                        _globalAudio = AudioSource.CreateNew();
                        await _globalAudio.SetAudioAsync(fileReference);

                        AudioManager.RegisterSource(_globalAudio);

                        _globalAudio.Play();
                    }

                    break;
                }
                case WarcraftFileType.GameObjectModel:
                {
                    _fileLoadingCancellationSource.Cancel();
                    _fileLoadingCancellationSource = new CancellationTokenSource();

                    await DisplayRenderableFile
                    (
                        fileReference,
                        DataLoadingRoutines.LoadGameModel,
                        DataLoadingRoutines.CreateRenderableGameModel,
                        ControlPage.Model,
                        _fileLoadingCancellationSource.Token
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

            _exportQueueTreeView.GetPathAtPos((int)e.Event.X, (int)e.Event.Y, out var path);

            if (path == null)
            {
                return;
            }

            _exportQueueListStore.GetIter(out var iter, path);

            var queuedReference = (FileReference)_exportQueueListStore.GetValue(iter, 0);

            if (string.IsNullOrEmpty(queuedReference?.FilePath))
            {
                _removeQueueItem.Sensitive = false;
            }
            else
            {
                _removeQueueItem.Sensitive = true;
            }

            _queueContextMenu.ShowAll();
            _queueContextMenu.PopupForDevice(e.Event.Device, null, null, null, null, e.Event.Button, e.Event.Time);
            //this.QueueContextMenu.Popup();
        }

        /// <summary>
        /// Handles removal of items from the export queue, triggered by a context menu press.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        private void OnQueueRemoveContextItemActivated(object sender, EventArgs e)
        {
            _exportQueueTreeView.Selection.GetSelected(out var selectedIter);
            _exportQueueListStore.Remove(ref selectedIter);
        }

        /// <summary>
        /// Handles application shutdown procedures - terminating render threads, cleaning
        /// up the UI, etc.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="a">The alpha component.</param>
        private void OnDeleteEvent(object sender, DeleteEventArgs a)
        {
            _isShuttingDown = true;

            _fileLoadingCancellationSource?.Cancel();

            _viewportWidget.MakeCurrent();

            _renderingEngine.SetRenderTarget(null);
            _renderingEngine.Dispose();

            _viewportWidget.Dispose();

            Application.Quit();
            a.RetVal = true;
        }
    }
}
