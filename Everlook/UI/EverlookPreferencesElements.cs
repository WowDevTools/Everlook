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

        [UIElement] private readonly Dialog _newGamePathDialog;
        [UIElement] private readonly Entry _aliasEntry;
        [UIElement] private readonly ComboBox _gameVersionCombo;
        [UIElement] private readonly FileChooserButton _pathChooser;

        /*
            General settings
        */

        [UIElement] private readonly TreeView _gamePathSelectionTreeView;
        [UIElement] private readonly ListStore _gamePathListStore;
        [UIElement] private readonly Button _addPathButton;
        [UIElement] private readonly Button _removePathButton;

        /*
            Export settings
        */

        [UIElement] private readonly FileChooserButton _defaultExportDirectoryFileChooserButton;
        [UIElement] private readonly ComboBox _defaultModelExportFormatComboBox;
        [UIElement] private readonly ComboBox _defaultImageExportFormatComboBox;
        [UIElement] private readonly ComboBox _defaultAudioExportFormatComboBox;
        [UIElement] private readonly Switch _keepDirectoryStructureSwitch;

        /*
            Privacy settings
        */

        [UIElement] private readonly CheckButton _allowStatsCheckButton;

        [UIElement] private readonly CheckButton _sendMachineIDCheckButton;
        [UIElement] private readonly CheckButton _sendInstallIDCheckButton;
        [UIElement] private readonly CheckButton _sendOSCheckButton;
        [UIElement] private readonly CheckButton _sendAppVersionCheckButton;
        [UIElement] private readonly CheckButton _sendRuntimeInfoCheckButton;

        /*
            Viewport settings
        */

        [UIElement] private readonly ColorButton _viewportColourButton;
        [UIElement] private readonly ColorButton _wireframeColourButton;
        [UIElement] private readonly Switch _occludeBoundingBoxesSwitch;
        [UIElement] private readonly Adjustment _cameraSpeedAdjustment;
        [UIElement] private readonly Adjustment _rotationSpeedAdjustment;
        [UIElement] private readonly Adjustment _cameraFOVAdjustment;
        [UIElement] private readonly Adjustment _sprintMultiplierAdjustment;

        /*
            Explorer settings
        */

        [UIElement] private readonly CheckButton _showUnknownFilesCheckButton;
        [UIElement] private readonly CheckButton _autoplayAudioCheckButton;
    }
}
