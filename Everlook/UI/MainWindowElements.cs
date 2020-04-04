//
//  MainWindowElements.cs
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

using System.Collections.Generic;
using Everlook.Explorer;
using Everlook.UI.Widgets;
using Gtk;
using UIElement = Gtk.Builder.ObjectAttribute;

// ReSharper disable UnassignedReadonlyField
#pragma warning disable 649
#pragma warning disable 1591
#pragma warning disable SA1134 // Each attribute should be placed on its own line of code
#pragma warning disable SA1214 // Readonly fields should appear before non-readonly fields

namespace Everlook.UI
{
    public sealed partial class MainWindow
    {
        /*
            Main UI elements
        */
        [UIElement] private readonly ToolButton _aboutButton = null!;
        [UIElement] private readonly AboutDialog _aboutDialog = null!;
        [UIElement] private readonly ToolButton _preferencesButton = null!;

        [UIElement] private readonly Paned _viewportPaned = null!;
        [UIElement] private readonly Paned _lowerBoxPaned = null!;
        [UIElement] private readonly Alignment _lowerBoxAlignment = null!;

        [UIElement] private readonly Alignment _viewportAlignment = null!;
        private readonly ViewportArea _viewportWidget;

        [UIElement] private readonly ComboBox _fileFilterComboBox = null!;

        [UIElement] private readonly Button _cancelCurrentActionButton = null!;

        /*
            Export queue elements
        */
        [UIElement] private readonly TreeView _exportQueueTreeView = null!;
        [UIElement] private readonly ListStore _exportQueueListStore = null!;

        [UIElement] private readonly Menu _queueContextMenu = null!;
        [UIElement] private readonly ImageMenuItem _removeQueueItem = null!;

        [UIElement] private readonly Button _clearExportQueueButton = null!;
        [UIElement] private readonly Button _runExportQueueButton = null!;

        /*
            Game explorer elements
        */
        [UIElement] private readonly Notebook _gameTabNotebook = null!;
        private readonly List<GamePage> _gamePages = new List<GamePage>();

        /*
            General item control elements
        */

        [UIElement] private readonly Notebook _itemControlNotebook = null!;

        /*
            Image control elements
        */
        [UIElement] private readonly CheckButton _renderAlphaCheckButton = null!;
        [UIElement] private readonly CheckButton _renderRedCheckButton = null!;
        [UIElement] private readonly CheckButton _renderGreenCheckButton = null!;
        [UIElement] private readonly CheckButton _renderBlueCheckButton = null!;

        [UIElement] private readonly Label _mipCountLabel = null!;

        /*
            Status bar elements
        */
        [UIElement] private readonly Statusbar _mainStatusBar = null!;
        [UIElement] private readonly Spinner _statusSpinner = null!;

        /*
            Model control elements
        */

        [UIElement] private readonly CheckButton _renderBoundsCheckButton = null!;
        [UIElement] private readonly CheckButton _renderWireframeCheckButton = null!;
        [UIElement] private readonly CheckButton _renderDoodadsCheckButton = null!;

        [UIElement] private readonly ComboBox _modelVariationComboBox = null!;
        [UIElement] private readonly ListStore _modelVariationListStore = null!;
        [UIElement] private readonly CellRendererText _modelVariationTextRenderer = null!;

        /*
            Animation control elements
        */

        /*
            Audio control elements
        */

        /*
            Object info elements
        */

        [UIElement] private readonly Label _polyCountLabel = null!;
        [UIElement] private readonly Label _vertexCountLabel = null!;
        [UIElement] private readonly Label _skinCountLabel = null!;
    }
}
