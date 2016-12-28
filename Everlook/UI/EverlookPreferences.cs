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

			LoadPreferences();
		}

		/// <summary>
		/// Handles the add path button clicked event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnAddPathButtonClicked(object sender, EventArgs e)
		{
			Uri defaultLocation = new Uri(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
			this.GameSelectionFileChooserDialog.SetCurrentFolderUri(defaultLocation.ToString());
			if (this.GameSelectionFileChooserDialog.Run() == (int)ResponseType.Ok)
			{
				Uri uriToStore = new Uri(this.GameSelectionFileChooserDialog.CurrentFolderUri);

				if (Directory.Exists(uriToStore.LocalPath))
				{
					this.GamePathListStore.AppendValues(uriToStore.LocalPath);
					GamePathStorage.Instance.StorePath(uriToStore.LocalPath);
				}
			}

			this.GameSelectionFileChooserDialog.Hide();
		}

		/// <summary>
		/// Handles the remove path button clicked event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnRemovePathButtonClicked(object sender, EventArgs e)
		{
			TreeIter selectedIter;
			this.GamePathSelectionTreeView.Selection.GetSelected(out selectedIter);

			if (this.GamePathListStore.IterIsValid(selectedIter))
			{
				string gamePath = (string) this.GamePathListStore.GetValue(selectedIter, 0);
				GamePathStorage.Instance.RemoveStoredPath(gamePath);
				this.GamePathListStore.Remove(ref selectedIter);
			}
		}

		/// <summary>
		/// Loads the preferences from disk, setting their values in the UI.
		/// </summary>
		private void LoadPreferences()
		{
			foreach (string gamePath in GamePathStorage.Instance.GamePaths)
			{
				if (Directory.Exists(gamePath))
				{
					this.GamePathListStore.AppendValues(gamePath);
				}
			}

			this.ViewportColourButton.Rgba = this.Config.GetViewportBackgroundColour();

			if (!String.IsNullOrEmpty(this.Config.GetDefaultExportDirectory()))
			{
				this.DefaultExportDirectoryFileChooserButton.SetCurrentFolderUri(this.Config.GetDefaultExportDirectory());
			}

			this.DefaultModelExportFormatComboBox.Active = (int) this.Config.GetDefaultModelFormat();
			this.DefaultImageExportFormatComboBox.Active = (int) this.Config.GetDefaultImageFormat();
			this.DefaultAudioExportFormatComboBox.Active = (int) this.Config.GetDefaultAudioFormat();
			this.KeepDirectoryStructureCheckButton.Active = this.Config.GetShouldKeepFileDirectoryStructure();
			this.SendStatsCheckButton.Active = this.Config.GetAllowSendAnonymousStats();
		}

		/// <summary>
		/// Saves the selected preferences to disk from the UI elements.
		/// </summary>
		public void SavePreferences()
		{
			this.GamePathListStore.Foreach(delegate(ITreeModel model, TreePath path, TreeIter iter)
				{
					string GamePath = (string)model.GetValue(iter, 0);
					GamePathStorage.Instance.StorePath(GamePath);

					return false;
				});

			this.Config.SetViewportBackgroundColour(this.ViewportColourButton.Rgba);
			this.Config.SetDefaultExportDirectory(new Uri(this.DefaultExportDirectoryFileChooserButton.CurrentFolderUri).LocalPath);
			this.Config.SetDefaultModelFormat((ModelFormat) this.DefaultModelExportFormatComboBox.Active);
			this.Config.SetDefaultImageFormat((ImageFormat) this.DefaultImageExportFormatComboBox.Active);
			this.Config.SetDefaultAudioFormat((AudioFormat) this.DefaultAudioExportFormatComboBox.Active);
			this.Config.SetKeepFileDirectoryStructure(this.KeepDirectoryStructureCheckButton.Active);
			this.Config.SetAllowSendAnonymousStats(this.SendStatsCheckButton.Active);
		}
	}
}

