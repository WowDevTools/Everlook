//
//  EverlookPreferences.cs
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
using Gtk;
using UIElement = Gtk.Builder.ObjectAttribute;
using Everlook.Configuration;
using Everlook.Export.Model;
using Everlook.Export.Image;
using Everlook.Export.Audio;
using System.IO;
using GLib;
using Warcraft.Core;
using Key = Gdk.Key;

namespace Everlook.UI
{
	/// <summary>
	/// Everlook preferences dialog. The "partial" qualifier is not strictly needed, but prevents the compiler from
	/// generating errors about the autoconnected members that relate to UI elements.
	/// </summary>
	public partial class EverlookPreferences : Dialog
	{
		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

		/// <summary>
		/// Creates an instance of the Preferences dialog, using the glade XML UI file.
		/// </summary>
		public static EverlookPreferences Create()
		{
			Builder builder = new Builder(null, "Everlook.interfaces.EverlookPreferences.glade", null);
			return new EverlookPreferences(builder, builder.GetObject("PreferencesDialog").Handle);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.UI.EverlookPreferences"/> class.
		/// </summary>
		/// <param name="builder">Builder.</param>
		/// <param name="handle">Handle.</param>
		protected EverlookPreferences(Builder builder, IntPtr handle)
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

			LoadPreferences();
		}

		/// <summary>
		/// Handles enter key presses in the path dialog.
		/// </summary>
		/// <param name="o"></param>
		/// <param name="args"></param>
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
		/// <param name="o"></param>
		/// <param name="args"></param>
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
		/// <param name="sender"></param>
		/// <param name="eventArgs"></param>
		private void OnAliasEntryChanged(object sender, EventArgs eventArgs)
		{
			this.NewGamePathDialog.SetResponseSensitive(ResponseType.Ok, !string.IsNullOrEmpty(this.AliasEntry.Text));
		}

		/// <summary>
		/// Handles the add path button clicked event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnAddPathButtonClicked(object sender, EventArgs e)
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

			string alias = (string) this.GamePathListStore.GetValue(selectedIter, 0);
			string path = (string) this.GamePathListStore.GetValue(selectedIter, 1);
			WarcraftVersion version = (WarcraftVersion) this.GamePathListStore.GetValue(selectedIter, 2);

			GamePathStorage.Instance.RemoveStoredPath(alias, version, path);
			this.GamePathListStore.Remove(ref selectedIter);
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

			this.ViewportColourButton.Rgba = this.Config.GetViewportBackgroundColour();

			if (!string.IsNullOrEmpty(this.Config.GetDefaultExportDirectory()))
			{
				this.DefaultExportDirectoryFileChooserButton.SetCurrentFolderUri(new Uri(new Uri("file://"), this.Config.GetDefaultExportDirectory()).AbsoluteUri);
			}

			this.DefaultModelExportFormatComboBox.Active = (int) this.Config.GetDefaultModelFormat();
			this.DefaultImageExportFormatComboBox.Active = (int) this.Config.GetDefaultImageFormat();
			this.DefaultAudioExportFormatComboBox.Active = (int) this.Config.GetDefaultAudioFormat();
			this.KeepDirectoryStructureCheckButton.Active = this.Config.GetShouldKeepFileDirectoryStructure();
			this.SendStatsCheckButton.Active = this.Config.GetAllowSendAnonymousStats();

			this.WireframeColourButton.Rgba = this.Config.GetWireframeColour();
		}

		/// <summary>
		/// Saves the selected preferences to disk from the UI elements.
		/// </summary>
		public void SavePreferences()
		{
			this.GamePathListStore.Foreach(delegate(ITreeModel model, TreePath path, TreeIter iter)
				{
					string alias = (string)model.GetValue(iter, 0);
					string gamePath = (string)model.GetValue(iter, 1);
					WarcraftVersion version = (WarcraftVersion) this.GamePathListStore.GetValue(iter, 2);
					GamePathStorage.Instance.StorePath(alias, version, gamePath);

					return false;
				});

			this.Config.SetViewportBackgroundColour(this.ViewportColourButton.Rgba);

			/*Uri exportPathURI;
			if (!this.DefaultExportDirectoryFileChooserButton.CurrentFolderUri.StartsWith("file://"))
			{
				exportPathURI = new Uri("file://" + this.DefaultExportDirectoryFileChooserButton.CurrentFolderUri);
			}
			else
			{
				exportPathURI = new Uri(this.DefaultExportDirectoryFileChooserButton.CurrentFolderUri);
			}*/

			string exportPath = this.DefaultExportDirectoryFileChooserButton.CurrentFolderFile.Uri.LocalPath;
			this.Config.SetDefaultExportDirectory(exportPath);

			this.Config.SetDefaultModelFormat((ModelFormat) this.DefaultModelExportFormatComboBox.Active);
			this.Config.SetDefaultImageFormat((ImageFormat) this.DefaultImageExportFormatComboBox.Active);
			this.Config.SetDefaultAudioFormat((AudioFormat) this.DefaultAudioExportFormatComboBox.Active);
			this.Config.SetKeepFileDirectoryStructure(this.KeepDirectoryStructureCheckButton.Active);
			this.Config.SetAllowSendAnonymousStats(this.SendStatsCheckButton.Active);

			this.Config.SetWireframeColour(this.WireframeColourButton.Rgba);
		}
	}
}

