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
            using (Builder builder = new Builder(null, "Everlook.interfaces.EverlookPreferences.glade", null))
            {
                return new EverlookPreferences(builder, builder.GetObject("PreferencesDialog").Handle);
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

            this._addPathButton.Clicked += OnAddPathButtonClicked;
            this._removePathButton.Clicked += OnRemovePathButtonClicked;

            this._gamePathSelectionTreeView.KeyPressEvent += OnKeyPressedGamePathTreeView;

            this._aliasEntry.Changed += OnAliasEntryChanged;

            // Handle enter key presses in the path dialog
            this._newGamePathDialog.KeyPressEvent += OnKeyPressedNewPathDialog;
            this._aliasEntry.KeyPressEvent += OnKeyPressedNewPathDialog;

            this._allowStatsCheckButton.Toggled += OnAllowStatsToggled;
            this._showUnknownFilesCheckButton.Toggled += OnShowUnknownFilesToggled;

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
            if (this._showUnknownFilesCheckButton.Active != this._config.ShowUnknownFilesWhenFiltering)
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
            bool suboptionSensitivity = this._allowStatsCheckButton.Active;

            this._sendMachineIDCheckButton.Sensitive = suboptionSensitivity;
            this._sendInstallIDCheckButton.Sensitive = suboptionSensitivity;
            this._sendOSCheckButton.Sensitive = suboptionSensitivity;
            this._sendAppVersionCheckButton.Sensitive = suboptionSensitivity;
            this._sendRuntimeInfoCheckButton.Sensitive = suboptionSensitivity;
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

            if (this._aliasEntry.Text.Length > 0)
            {
                this._newGamePathDialog.Respond(ResponseType.Ok);
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
            this._newGamePathDialog.SetResponseSensitive(ResponseType.Ok, !string.IsNullOrEmpty(this._aliasEntry.Text));
        }

        /// <summary>
        /// Handles the add path button clicked event.
        /// </summary>
        /// <param name="sender">The sending object.</param>
        /// <param name="eventArgs">The event arguments.</param>
        private void OnAddPathButtonClicked(object sender, EventArgs eventArgs)
        {
            Uri defaultLocation = new Uri(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            this._pathChooser.SetCurrentFolderUri(defaultLocation.ToString());

            switch ((ResponseType)this._newGamePathDialog.Run())
            {
                case ResponseType.Ok:
                {
                    string alias = this._aliasEntry.Text;
                    WarcraftVersion selectedVersion = (WarcraftVersion)this._gameVersionCombo.Active;
                    Uri uriToStore = this._pathChooser.File.Uri;

                    if (Directory.Exists(uriToStore.LocalPath))
                    {
                        this._gamePathListStore.AppendValues(alias, uriToStore.LocalPath, (uint)selectedVersion, selectedVersion.ToString());
                        this.DidGameListChange = true;
                    }

                    this._newGamePathDialog.Hide();
                    break;
                }
                default:
                {
                    this._newGamePathDialog.Hide();
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
            this._gamePathSelectionTreeView.Selection.GetSelected(out selectedIter);

            if (!this._gamePathListStore.IterIsValid(selectedIter))
            {
                return;
            }

            string alias = (string)this._gamePathListStore.GetValue(selectedIter, 0);
            string path = (string)this._gamePathListStore.GetValue(selectedIter, 1);
            WarcraftVersion version = (WarcraftVersion)this._gamePathListStore.GetValue(selectedIter, 2);

            GamePathStorage.Instance.RemoveStoredPath(alias, version, path);
            this._gamePathListStore.Remove(ref selectedIter);
            this.DidGameListChange = true;
        }

        /// <summary>
        /// Loads the preferences from disk, setting their values in the UI.
        /// </summary>
        private void LoadPreferences()
        {
            foreach ((string alias, WarcraftVersion version, string gamePath) in GamePathStorage.Instance.GamePaths)
            {
                if (Directory.Exists(gamePath))
                {
                    this._gamePathListStore.AppendValues(alias, gamePath, (uint)version, version.ToString());
                }
            }

            // Make sure we've got the latest from disk
            this._config.Reload();

            LoadConfigurationValues();
        }

        /// <summary>
        /// Loads the configuration values into the UI.
        /// </summary>
        private void LoadConfigurationValues()
        {
            if (!string.IsNullOrEmpty(this._config.DefaultExportDirectory))
            {
                if (!Directory.Exists(this._config.DefaultExportDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(this._config.DefaultExportDirectory);
                    }
                    catch (UnauthorizedAccessException uax)
                    {
                        Console.WriteLine($"Failed to create the export directory: {uax}");
                        throw;
                    }
                }
            }

            string fullExportPath = System.IO.Path.GetFullPath(this._config.DefaultExportDirectory);
            this._defaultExportDirectoryFileChooserButton.SetUri(new Uri(fullExportPath).AbsoluteUri);

            this._defaultModelExportFormatComboBox.Active = (int)this._config.DefaultModelExportFormat;
            this._defaultImageExportFormatComboBox.Active = (int)this._config.DefaultImageExportFormat;
            this._defaultAudioExportFormatComboBox.Active = (int)this._config.DefaultAudioExportFormat;
            this._keepDirectoryStructureSwitch.Active = this._config.KeepFileDirectoryStructure;

            this._allowStatsCheckButton.Active = this._config.AllowSendingStatistics;
            this._sendMachineIDCheckButton.Active = this._config.SendMachineID;
            this._sendInstallIDCheckButton.Active = this._config.SendInstallID;
            this._sendOSCheckButton.Active = this._config.SendOperatingSystem;
            this._sendAppVersionCheckButton.Active = this._config.SendAppVersion;
            this._sendRuntimeInfoCheckButton.Active = this._config.SendRuntimeInformation;

            this._viewportColourButton.Rgba = this._config.ViewportBackgroundColour;
            this._wireframeColourButton.Rgba = this._config.WireframeColour;
            this._occludeBoundingBoxesSwitch.Active = this._config.OccludeBoundingBoxes;
            this._cameraSpeedAdjustment.Value = this._config.CameraSpeed;
            this._rotationSpeedAdjustment.Value = this._config.RotationSpeed;
            this._cameraFOVAdjustment.Value = this._config.CameraFOV;
            this._sprintMultiplierAdjustment.Value = this._config.SprintMultiplier;

            this._showUnknownFilesCheckButton.Active = this._config.ShowUnknownFilesWhenFiltering;
            this._autoplayAudioCheckButton.Active = this._config.AutoplayAudioFiles;
        }

        /// <summary>
        /// Saves the selected preferences to disk from the UI elements.
        /// </summary>
        public void SavePreferences()
        {
            this._gamePathListStore.Foreach
            (
                (model, path, iter) =>
                {
                    string alias = (string)model.GetValue(iter, 0);
                    string gamePath = (string)model.GetValue(iter, 1);
                    WarcraftVersion version = (WarcraftVersion)this._gamePathListStore.GetValue(iter, 2);
                    GamePathStorage.Instance.StorePath(alias, version, gamePath);

                    return false;
                }
            );

            string selectedExportDirectory = new Uri(this._defaultExportDirectoryFileChooserButton.Uri).LocalPath;
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

            this._config.DefaultExportDirectory = selectedExportDirectory;

            this._config.DefaultModelExportFormat = (ModelFormat)this._defaultModelExportFormatComboBox.Active;
            this._config.DefaultImageExportFormat = (ImageFormat)this._defaultImageExportFormatComboBox.Active;
            this._config.DefaultAudioExportFormat = (AudioFormat)this._defaultAudioExportFormatComboBox.Active;
            this._config.KeepFileDirectoryStructure = this._keepDirectoryStructureSwitch.Active;

            this._config.AllowSendingStatistics = this._allowStatsCheckButton.Active;
            this._config.SendMachineID = this._sendMachineIDCheckButton.Active;
            this._config.SendInstallID = this._sendInstallIDCheckButton.Active;
            this._config.SendOperatingSystem = this._sendOSCheckButton.Active;
            this._config.SendAppVersion = this._sendAppVersionCheckButton.Active;
            this._config.SendRuntimeInformation = this._sendRuntimeInfoCheckButton.Active;

            this._config.ViewportBackgroundColour = this._viewportColourButton.Rgba;
            this._config.WireframeColour = this._wireframeColourButton.Rgba;
            this._config.OccludeBoundingBoxes = this._occludeBoundingBoxesSwitch.Active;
            this._config.CameraSpeed = this._cameraSpeedAdjustment.Value;
            this._config.RotationSpeed = this._rotationSpeedAdjustment.Value;
            this._config.CameraFOV = this._cameraFOVAdjustment.Value;
            this._config.SprintMultiplier = this._sprintMultiplierAdjustment.Value;

            this._config.ShowUnknownFilesWhenFiltering = this._showUnknownFilesCheckButton.Active;
            this._config.AutoplayAudioFiles = this._autoplayAudioCheckButton.Active;

            this._config.Commit();
        }
    }
}
