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
		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

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

			this.AddPathButton.Clicked += OnAddPathButtonClicked;
			this.RemovePathButton.Clicked += OnRemovePathButtonClicked;

			this.GamePathSelectionTreeView.KeyPressEvent += OnKeyPressedGamePathTreeView;

			this.AliasEntry.Changed += OnAliasEntryChanged;

			// Handle enter key presses in the path dialog
			this.NewGamePathDialog.KeyPressEvent += OnKeyPressedNewPathDialog;
			this.AliasEntry.KeyPressEvent += OnKeyPressedNewPathDialog;

			this.AllowStatsCheckButton.Toggled += OnAllowStatsToggled;
			this.ShowUnknownFilesCheckButton.Toggled += OnShowUnknownFilesToggled;

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
			if (this.ShowUnknownFilesCheckButton.Active != this.Config.ShowUnknownFilesWhenFiltering)
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
			bool suboptionSensitivity = this.AllowStatsCheckButton.Active;

			this.SendMachineIDCheckButton.Sensitive = suboptionSensitivity;
			this.SendInstallIDCheckButton.Sensitive = suboptionSensitivity;
			this.SendOSCheckButton.Sensitive = suboptionSensitivity;
			this.SendAppVersionCheckButton.Sensitive = suboptionSensitivity;
			this.SendRuntimeInfoCheckButton.Sensitive = suboptionSensitivity;
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

			if (this.AliasEntry.Text.Length > 0)
			{
				this.NewGamePathDialog.Respond(ResponseType.Ok);
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
			this.NewGamePathDialog.SetResponseSensitive(ResponseType.Ok, !string.IsNullOrEmpty(this.AliasEntry.Text));
		}

		/// <summary>
		/// Handles the add path button clicked event.
		/// </summary>
		/// <param name="sender">The sending object.</param>
		/// <param name="eventArgs">The event arguments.</param>
		private void OnAddPathButtonClicked(object sender, EventArgs eventArgs)
		{
			Uri defaultLocation = new Uri(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
			this.PathChooser.SetCurrentFolderUri(defaultLocation.ToString());

			switch ((ResponseType)this.NewGamePathDialog.Run())
			{
				case ResponseType.Ok:
				{
					string alias = this.AliasEntry.Text;
					WarcraftVersion selectedVersion = (WarcraftVersion)this.GameVersionCombo.Active;
					Uri uriToStore = new Uri(this.PathChooser.CurrentFolderUri);

					if (Directory.Exists(uriToStore.LocalPath))
					{
						this.GamePathListStore.AppendValues(alias, uriToStore.LocalPath, (uint)selectedVersion, selectedVersion.ToString());
						this.DidGameListChange = true;
					}

					this.NewGamePathDialog.Hide();
					break;
				}
				default:
				{
					this.NewGamePathDialog.Hide();
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
			this.GamePathSelectionTreeView.Selection.GetSelected(out selectedIter);

			if (!this.GamePathListStore.IterIsValid(selectedIter))
			{
				return;
			}

			string alias = (string)this.GamePathListStore.GetValue(selectedIter, 0);
			string path = (string)this.GamePathListStore.GetValue(selectedIter, 1);
			WarcraftVersion version = (WarcraftVersion)this.GamePathListStore.GetValue(selectedIter, 2);

			GamePathStorage.Instance.RemoveStoredPath(alias, version, path);
			this.GamePathListStore.Remove(ref selectedIter);
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
					this.GamePathListStore.AppendValues(alias, gamePath, (uint)version, version.ToString());
				}
			}

			// Make sure we've got the latest from disk
			this.Config.Reload();

			LoadConfigurationValues();
		}

		/// <summary>
		/// Loads the configuration values into the UI.
		/// </summary>
		private void LoadConfigurationValues()
		{
			this.ViewportColourButton.Rgba = this.Config.ViewportBackgroundColour;

			if (!string.IsNullOrEmpty(this.Config.DefaultExportDirectory))
			{
				if (!Directory.Exists(this.Config.DefaultExportDirectory))
				{
					try
					{
						Directory.CreateDirectory(this.Config.DefaultExportDirectory);
					}
					catch (UnauthorizedAccessException uax)
					{
						Console.WriteLine($"Failed to create the export directory: {uax}");
						throw;
					}
				}
			}

			this.DefaultExportDirectoryFileChooserButton.SetFilename(this.Config.DefaultExportDirectory);

			this.DefaultModelExportFormatComboBox.Active = (int)this.Config.DefaultModelExportFormat;
			this.DefaultImageExportFormatComboBox.Active = (int)this.Config.DefaultImageExportFormat;
			this.DefaultAudioExportFormatComboBox.Active = (int)this.Config.DefaultAudioExportFormat;
			this.KeepDirectoryStructureCheckButton.Active = this.Config.KeepFileDirectoryStructure;

			this.AllowStatsCheckButton.Active = this.Config.AllowSendingStatistics;
			this.SendMachineIDCheckButton.Active = this.Config.SendMachineID;
			this.SendInstallIDCheckButton.Active = this.Config.SendInstallID;
			this.SendOSCheckButton.Active = this.Config.SendOperatingSystem;
			this.SendAppVersionCheckButton.Active = this.Config.SendAppVersion;
			this.SendRuntimeInfoCheckButton.Active = this.Config.SendRuntimeInformation;

			this.WireframeColourButton.Rgba = this.Config.WireframeColour;
			this.OccludeBoundingBoxesCheckButton.Active = this.Config.OccludeBoundingBoxes;
			this.CameraSpeedAdjustment.Value = this.Config.CameraSpeed;
			this.SprintMultiplierAdjustment.Value = this.Config.SprintMultiplier;

			this.ShowUnknownFilesCheckButton.Active = this.Config.ShowUnknownFilesWhenFiltering;
			this.AutoplayAudioCheckButton.Active = this.Config.AutoplayAudioFiles;
		}

		/// <summary>
		/// Saves the selected preferences to disk from the UI elements.
		/// </summary>
		public void SavePreferences()
		{
			this.GamePathListStore.Foreach
			(
				(model, path, iter) =>
				{
					string alias = (string)model.GetValue(iter, 0);
					string gamePath = (string)model.GetValue(iter, 1);
					WarcraftVersion version = (WarcraftVersion)this.GamePathListStore.GetValue(iter, 2);
					GamePathStorage.Instance.StorePath(alias, version, gamePath);

					return false;
				}
			);

			this.Config.ViewportBackgroundColour = this.ViewportColourButton.Rgba;

			if (!Directory.Exists(this.DefaultExportDirectoryFileChooserButton.Filename))
			{
				try
				{
					Directory.CreateDirectory(this.DefaultExportDirectoryFileChooserButton.Filename);
				}
				catch (UnauthorizedAccessException uax)
				{
					Console.WriteLine($"Failed to create the export directory: {uax}");
					throw;
				}
			}

			this.Config.DefaultExportDirectory = this.DefaultExportDirectoryFileChooserButton.Filename;

			this.Config.DefaultModelExportFormat = (ModelFormat)this.DefaultModelExportFormatComboBox.Active;
			this.Config.DefaultImageExportFormat = (ImageFormat)this.DefaultImageExportFormatComboBox.Active;
			this.Config.DefaultAudioExportFormat = (AudioFormat)this.DefaultAudioExportFormatComboBox.Active;
			this.Config.KeepFileDirectoryStructure = this.KeepDirectoryStructureCheckButton.Active;

			this.Config.AllowSendingStatistics = this.AllowStatsCheckButton.Active;
			this.Config.SendMachineID = this.SendMachineIDCheckButton.Active;
			this.Config.SendInstallID = this.SendInstallIDCheckButton.Active;
			this.Config.SendOperatingSystem = this.SendOSCheckButton.Active;
			this.Config.SendAppVersion = this.SendAppVersionCheckButton.Active;
			this.Config.SendRuntimeInformation = this.SendRuntimeInfoCheckButton.Active;

			this.Config.WireframeColour = this.WireframeColourButton.Rgba;
			this.Config.OccludeBoundingBoxes = this.OccludeBoundingBoxesCheckButton.Active;
			this.Config.CameraSpeed = this.CameraSpeedAdjustment.Value;
			this.Config.SprintMultiplier = this.SprintMultiplierAdjustment.Value;

			this.Config.ShowUnknownFilesWhenFiltering = this.ShowUnknownFilesCheckButton.Active;
			this.Config.AutoplayAudioFiles = this.AutoplayAudioCheckButton.Active;

			this.Config.Commit();
		}
	}
}
