//
//  EverlookPreferencesElements.cs
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

using Gtk;
using UIElement = Gtk.Builder.ObjectAttribute;

// ReSharper disable UnassignedReadonlyField
#pragma warning disable 649
#pragma warning disable 1591
#pragma warning disable SA1134 // Each attribute should be placed on its own line of code

namespace Everlook.UI
{
    public sealed partial class EverlookPreferences
    {
        /*
            New game dialog
        */

        [UIElement] private readonly Dialog _newGamePathDialog = null!;
        [UIElement] private readonly Entry _aliasEntry = null!;
        [UIElement] private readonly ComboBox _gameVersionCombo = null!;
        [UIElement] private readonly FileChooserButton _pathChooser = null!;

        /*
            General settings
        */

        [UIElement] private readonly TreeView _gamePathSelectionTreeView = null!;
        [UIElement] private readonly ListStore _gamePathListStore = null!;
        [UIElement] private readonly Button _addPathButton = null!;
        [UIElement] private readonly Button _removePathButton = null!;

        /*
            Export settings
        */

        [UIElement] private readonly FileChooserButton _defaultExportDirectoryFileChooserButton = null!;
        [UIElement] private readonly ComboBox _defaultModelExportFormatComboBox = null!;
        [UIElement] private readonly ComboBox _defaultImageExportFormatComboBox = null!;
        [UIElement] private readonly ComboBox _defaultAudioExportFormatComboBox = null!;
        [UIElement] private readonly Switch _keepDirectoryStructureSwitch = null!;

        /*
            Privacy settings
        */

        [UIElement] private readonly CheckButton _allowStatsCheckButton = null!;

        [UIElement] private readonly CheckButton _sendMachineIDCheckButton = null!;
        [UIElement] private readonly CheckButton _sendInstallIDCheckButton = null!;
        [UIElement] private readonly CheckButton _sendOSCheckButton = null!;
        [UIElement] private readonly CheckButton _sendAppVersionCheckButton = null!;
        [UIElement] private readonly CheckButton _sendRuntimeInfoCheckButton = null!;

        /*
            Viewport settings
        */

        [UIElement] private readonly ColorButton _viewportColourButton = null!;
        [UIElement] private readonly ColorButton _wireframeColourButton = null!;
        [UIElement] private readonly Switch _occludeBoundingBoxesSwitch = null!;
        [UIElement] private readonly Adjustment _cameraSpeedAdjustment = null!;
        [UIElement] private readonly Adjustment _rotationSpeedAdjustment = null!;
        [UIElement] private readonly Adjustment _cameraFOVAdjustment = null!;
        [UIElement] private readonly Adjustment _sprintMultiplierAdjustment = null!;

        /*
            Explorer settings
        */

        [UIElement] private readonly CheckButton _showUnknownFilesCheckButton = null!;
        [UIElement] private readonly CheckButton _autoplayAudioCheckButton = null!;
    }
}
