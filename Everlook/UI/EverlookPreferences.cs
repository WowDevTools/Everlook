//
//  EverlookPreferences.cs
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
using System.IO;
using Everlook.Configuration;
using Everlook.Export.Audio;
using Everlook.Export.Image;
using Everlook.Export.Model;
using GLib;
using Gtk;
using Warcraft.Core;

using EventArgs = System.EventArgs;
using Key = Gdk.Key;
using UIElement = Gtk.Builder.ObjectAttribute;

namespace Everlook.UI
{
    /// <summary>
    /// Everlook preferences dialog. The "partial" qualifier is not strictly needed, but prevents the compiler from
    /// generating errors about the autoconnected members that relate to UI elements.
    /// </summary>
    public sealed partial class EverlookPreferences : Dialog
    {
        private readonly EverlookConfiguration _config = EverlookConfiguration.Instance;

        /// <summary>
        /// Gets a value indicating whether the list of games to load changed.
        /// </summary>
        public bool DidGameListChange { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the file explorer should be refiltered.
        /// </summary>
        public bool ShouldRefilterTree { get; private set; }

        /// <summary>
        /// Creates an instance of the Preferences dialog, using the glade XML UI file.
        /// </summary>
        /// <returns>An initialized instance of the EverlookPreferences class.</returns>
        public static EverlookPreferences Create()
        {
            using (var builder = new Builder(null, "Everlook.interfaces.EverlookPreferences.glade", null))
            {
                return new EverlookPreferences(builder, builder.GetObject("_preferencesDialog").Handle);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Everlook.UI.EverlookPreferences"/> class.
        /// </summary>
        /// <param name="builder">Builder.</param>
        /// <param name="handle">Handle.</param>
        private EverlookPreferences(Builder builder, IntPtr handle)
            : base(handle)
        {
            builder.Autoconnect(this);

            _addPathButton.Clicked += OnAddPathButtonClicked;
            _removePathButton.Clicked += OnRemovePathButtonClicked;

            _gamePathSelectionTreeView.KeyPressEvent += OnKeyPressedGamePathTreeView;

            _aliasEntry.Changed += OnAliasEntryChanged;

            // Handle enter key presses in the path dialog
            _newGamePathDialog.KeyPressEvent += OnKeyPressedNewPathDialog;
            _aliasEntry.KeyPressEvent += OnKeyPressedNewPathDialog;

            _allowStatsCheckButton.Toggled += OnAllowStatsToggled;
            _showUnknownFilesCheckButton.Toggled += OnShowUnknownFilesToggled;

            LoadPreferences();
        }

        /// <summary>
        /// Handles tree refiltering if the unknown files option changes.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="e">The event arguments.</param>
        [ConnectBefore]
        private void OnShowUnknownFilesToggled(object sender, EventArgs e)
        {
            // Refilter if the option has changed
            if (_showUnknownFilesCheckButton.Active != _config.ShowUnknownFilesWhenFiltering)
            {
                this.ShouldRefilterTree = true;
                return;
            }

            this.ShouldRefilterTree = false;
        }

        /// <summary>
        /// Handles enabling and disabling of the statistical suboptions.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="e">The event arguments.</param>
        [ConnectBefore]
        private void OnAllowStatsToggled(object sender, EventArgs e)
        {
            var suboptionSensitivity = _allowStatsCheckButton.Active;

            _sendMachineIDCheckButton.Sensitive = suboptionSensitivity;
            _sendInstallIDCheckButton.Sensitive = suboptionSensitivity;
            _sendOSCheckButton.Sensitive = suboptionSensitivity;
            _sendAppVersionCheckButton.Sensitive = suboptionSensitivity;
            _sendRuntimeInfoCheckButton.Sensitive = suboptionSensitivity;
        }

        /// <summary>
        /// Handles enter key presses in the path dialog.
        /// </summary>
        /// <param name="o">The sending object.</param>
        /// <param name="args">The event arguments.</param>
        [ConnectBefore]
        private void OnKeyPressedNewPathDialog(object o, KeyPressEventArgs args)
        {
            if (args.Event.Key != Key.ISO_Enter || args.Event.Key != Key.KP_Enter || args.Event.Key != Key.Return)
            {
                return;
            }

            if (_aliasEntry.Text.Length > 0)
            {
                _newGamePathDialog.Respond(ResponseType.Ok);
            }
        }

        /// <summary>
        /// Handles deletion of rows in the treeview by keyboard shortcut.
        /// </summary>
        /// <param name="o">The sending object.</param>
        /// <param name="args">The event arguments.</param>
        private void OnKeyPressedGamePathTreeView(object o, KeyPressEventArgs args)
        {
            if (args.Event.Key == Key.Delete)
            {
                OnRemovePathButtonClicked(o, args);
            }
        }

        /// <summary>
        /// Handles setting the sensitivity of the confirmation button in the path addition dialog.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="eventArgs">The event arguments.</param>
        private void OnAliasEntryChanged(object sender, EventArgs eventArgs)
        {
            _newGamePathDialog.SetResponseSensitive(ResponseType.Ok, !string.IsNullOrEmpty(_aliasEntry.Text));
        }

        /// <summary>
        /// Handles the add path button clicked event.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="eventArgs">The event arguments.</param>
        private void OnAddPathButtonClicked(object sender, EventArgs eventArgs)
        {
            var defaultLocation = new Uri(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            _pathChooser.SetCurrentFolderUri(defaultLocation.ToString());

            switch ((ResponseType)_newGamePathDialog.Run())
            {
                case ResponseType.Ok:
                {
                    var alias = _aliasEntry.Text;
                    var selectedVersion = (WarcraftVersion)_gameVersionCombo.Active;
                    var uriToStore = _pathChooser.File.Uri;

                    if (Directory.Exists(uriToStore.LocalPath))
                    {
                        _gamePathListStore.AppendValues
                        (
                            alias,
                            uriToStore.LocalPath,
                            (uint)selectedVersion,
                            selectedVersion.ToString()
                        );

                        this.DidGameListChange = true;
                    }

                    _newGamePathDialog.Hide();
                    break;
                }
                default:
                {
                    _newGamePathDialog.Hide();
                    break;
                }
            }
        }

        /// <summary>
        /// Handles the remove path button clicked event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        private void OnRemovePathButtonClicked(object sender, EventArgs e)
        {
            TreeIter selectedIter;
            _gamePathSelectionTreeView.Selection.GetSelected(out selectedIter);

            if (!_gamePathListStore.IterIsValid(selectedIter))
            {
                return;
            }

            var alias = (string)_gamePathListStore.GetValue(selectedIter, 0);
            var path = (string)_gamePathListStore.GetValue(selectedIter, 1);
            var version = (WarcraftVersion)_gamePathListStore.GetValue(selectedIter, 2);

            GamePathStorage.Instance.RemoveStoredPath(alias, version, path);
            _gamePathListStore.Remove(ref selectedIter);
            this.DidGameListChange = true;
        }

        /// <summary>
        /// Loads the preferences from disk, setting their values in the UI.
        /// </summary>
        private void LoadPreferences()
        {
            foreach ((var alias, var version, var gamePath) in GamePathStorage.Instance.GamePaths)
            {
                if (Directory.Exists(gamePath))
                {
                    _gamePathListStore.AppendValues(alias, gamePath, (uint)version, version.ToString());
                }
            }

            // Make sure we've got the latest from disk
            _config.LoadFromDisk();

            LoadConfigurationValues();
        }

        /// <summary>
        /// Loads the configuration values into the UI.
        /// </summary>
        private void LoadConfigurationValues()
        {
            if (!string.IsNullOrEmpty(_config.DefaultExportDirectory))
            {
                if (!Directory.Exists(_config.DefaultExportDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(_config.DefaultExportDirectory);
                    }
                    catch (UnauthorizedAccessException uax)
                    {
                        Console.WriteLine($"Failed to create the export directory: {uax}");
                        throw;
                    }
                }
            }

            var fullExportPath = System.IO.Path.GetFullPath(_config.DefaultExportDirectory);
            _defaultExportDirectoryFileChooserButton.SetUri(new Uri(fullExportPath).AbsoluteUri);

            _defaultModelExportFormatComboBox.Active = (int)_config.DefaultModelExportFormat;
            _defaultImageExportFormatComboBox.Active = (int)_config.DefaultImageExportFormat;
            _defaultAudioExportFormatComboBox.Active = (int)_config.DefaultAudioExportFormat;
            _keepDirectoryStructureSwitch.Active = _config.KeepFileDirectoryStructure;

            _allowStatsCheckButton.Active = _config.AllowSendingStatistics;
            _sendMachineIDCheckButton.Active = _config.SendMachineID;
            _sendInstallIDCheckButton.Active = _config.SendInstallID;
            _sendOSCheckButton.Active = _config.SendOperatingSystem;
            _sendAppVersionCheckButton.Active = _config.SendAppVersion;
            _sendRuntimeInfoCheckButton.Active = _config.SendRuntimeInformation;

            _viewportColourButton.Rgba = _config.ViewportBackgroundColour;
            _wireframeColourButton.Rgba = _config.WireframeColour;
            _occludeBoundingBoxesSwitch.Active = _config.OccludeBoundingBoxes;
            _cameraSpeedAdjustment.Value = _config.CameraSpeed;
            _rotationSpeedAdjustment.Value = _config.RotationSpeed;
            _cameraFOVAdjustment.Value = _config.CameraFOV;
            _sprintMultiplierAdjustment.Value = _config.SprintMultiplier;

            _showUnknownFilesCheckButton.Active = _config.ShowUnknownFilesWhenFiltering;
            _autoplayAudioCheckButton.Active = _config.AutoplayAudioFiles;
        }

        /// <summary>
        /// Saves the selected preferences to disk from the UI elements.
        /// </summary>
        public void SavePreferences()
        {
            _gamePathListStore.Foreach
            (
                (model, path, iter) =>
                {
                    var alias = (string)model.GetValue(iter, 0);
                    var gamePath = (string)model.GetValue(iter, 1);
                    var version = (WarcraftVersion)_gamePathListStore.GetValue(iter, 2);
                    GamePathStorage.Instance.StorePath(alias, version, gamePath);

                    return false;
                }
            );

            var selectedExportDirectory = new Uri(_defaultExportDirectoryFileChooserButton.Uri).LocalPath;
            if (!Directory.Exists(selectedExportDirectory))
            {
                try
                {
                    Directory.CreateDirectory(selectedExportDirectory);
                }
                catch (UnauthorizedAccessException uax)
                {
                    Console.WriteLine($"Failed to create the export directory: {uax}");
                    throw;
                }
            }

            _config.DefaultExportDirectory = selectedExportDirectory;

            _config.DefaultModelExportFormat = (ModelFormat)_defaultModelExportFormatComboBox.Active;
            _config.DefaultImageExportFormat = (ImageFormat)_defaultImageExportFormatComboBox.Active;
            _config.DefaultAudioExportFormat = (AudioFormat)_defaultAudioExportFormatComboBox.Active;
            _config.KeepFileDirectoryStructure = _keepDirectoryStructureSwitch.Active;

            _config.AllowSendingStatistics = _allowStatsCheckButton.Active;
            _config.SendMachineID = _sendMachineIDCheckButton.Active;
            _config.SendInstallID = _sendInstallIDCheckButton.Active;
            _config.SendOperatingSystem = _sendOSCheckButton.Active;
            _config.SendAppVersion = _sendAppVersionCheckButton.Active;
            _config.SendRuntimeInformation = _sendRuntimeInfoCheckButton.Active;

            _config.ViewportBackgroundColour = _viewportColourButton.Rgba;
            _config.WireframeColour = _wireframeColourButton.Rgba;
            _config.OccludeBoundingBoxes = _occludeBoundingBoxesSwitch.Active;
            _config.CameraSpeed = _cameraSpeedAdjustment.Value;
            _config.RotationSpeed = _rotationSpeedAdjustment.Value;
            _config.CameraFOV = _cameraFOVAdjustment.Value;
            _config.SprintMultiplier = _sprintMultiplierAdjustment.Value;

            _config.ShowUnknownFilesWhenFiltering = _showUnknownFilesCheckButton.Active;
            _config.AutoplayAudioFiles = _autoplayAudioCheckButton.Active;

            _config.Commit();
        }
    }
}
