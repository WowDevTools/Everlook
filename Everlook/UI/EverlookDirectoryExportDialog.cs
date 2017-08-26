//
//  EverlookDirectoryExportDialog.cs
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
using Everlook.Configuration;
using Everlook.Explorer;
using Gdk;
using Gtk;

namespace Everlook.UI
{
	/// <summary>
	/// Everlook directory export dialog. The "partial" qualifier is not strictly needed, but prevents the compiler from
	/// generating errors about the autoconnected members that relate to UI elements.
	/// </summary>
	public partial class EverlookDirectoryExportDialog : Dialog
	{
		/// <summary>
		/// The reference to the file in the package that is to be exported.
		/// </summary>
		private readonly FileReference ExportTarget;

		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

		private readonly Dictionary<string, FileReference> ReferenceMapping = new Dictionary<string, FileReference>();

		/// <summary>
		/// Creates an instance of the Image Export dialog, using the glade XML UI file.
		/// </summary>
		/// <param name="inExportTarget">The reference which is to be exported.</param>
		/// <returns>An initialized instance of the EverlookDirectoryExportDialog class.</returns>
		public static EverlookDirectoryExportDialog Create(FileReference inExportTarget)
		{
			using (Builder builder = new Builder(null, "Everlook.interfaces.EverlookDirectoryExport.glade", null))
			{
				return new EverlookDirectoryExportDialog
				(
					builder,
					builder.GetObject("EverlookDirectoryExportDialog").Handle,
					inExportTarget
				);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="EverlookDirectoryExportDialog"/> class.
		/// </summary>
		/// <param name="builder">Builder.</param>
		/// <param name="handle">Handle.</param>
		/// <param name="inExportTarget">In export target.</param>
		private EverlookDirectoryExportDialog(Builder builder, IntPtr handle, FileReference inExportTarget)
			: base(handle)
		{
			builder.Autoconnect(this);

			this.ExportTarget = inExportTarget;

			/*
				 UI Setup
			*/
			this.ExportItemToggleRenderer.Toggled += OnExportItemToggleClicked;
			this.ItemListingTreeView.ButtonPressEvent += OnItemListingButtonPressed;
			this.SelectAllItem.Activated += OnSelectAllItemActivated;
			this.SelectNoneItem.Activated += OnSelectNoneItemActivated;

			LoadInformation();
		}

		private void LoadInformation()
		{
			this.Title = $"Export Directory | {this.ExportTarget.Filename}";
			this.ExportDirectoryFileChooserButton.SetFilename(this.Config.DefaultExportDirectory);

			// Load all references
			/*
			foreach (FileReference childReference in this.ExportTarget.ChildReferences)
			{
				// TODO: Support recursive folder export
				if (childReference.IsFile)
				{
					this.ItemExportListStore.AppendValues(true, childReference.GetReferencedItemName());

					if (!this.ReferenceMapping.ContainsKey(childReference.GetReferencedItemName()))
					{
						this.ReferenceMapping.Add(childReference.GetReferencedItemName(), childReference);
					}
				}
			}
			*/
		}

		/// <summary>
		/// Exports the mipmaps in the image.
		/// </summary>
		public void RunExport()
		{
			/*
			this.ItemExportListStore.Foreach(delegate(ITreeModel model, TreePath path, TreeIter iter)
			{
				bool bShouldExport = (bool) this.ItemExportListStore.GetValue(iter, 0);

				if (bShouldExport)
				{
					FileReference referenceToExport = this.ReferenceMapping[(string) this.ItemExportListStore.GetValue(iter, 1)];

					string exportPath = "";
					if (this.Config.GetShouldKeepFileDirectoryStructure())
					{
						string parentDirectoryOfFile = this.ExportTarget.FilePath.ConvertPathSeparatorsToCurrentNativeSeparator();

						exportPath =
							$"{this.ExportDirectoryFileChooserButton.Filename}{System.IO.Path.DirectorySeparatorChar}{parentDirectoryOfFile}{System.IO.Path.DirectorySeparatorChar}{referenceToExport.GetReferencedItemName()}";
					}
					else
					{
						exportPath = $"{this.ExportDirectoryFileChooserButton.Filename}{System.IO.Path.DirectorySeparatorChar}{referenceToExport.GetReferencedItemName()}";
					}

					Directory.CreateDirectory(Directory.GetParent(exportPath).FullName);

					byte[] fileData = referenceToExport.Extract();
					if (fileData != null)
					{
						File.WriteAllBytes(exportPath, fileData);
					}
				}

				return false;
			});
			*/
		}

		/// <summary>
		/// Handles context menu spawning for the game explorer.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		[GLib.ConnectBefore]
		private void OnItemListingButtonPressed(object sender, ButtonPressEventArgs e)
		{
			if (e.Event.Type == EventType.ButtonPress && e.Event.Button == 3)
			{
				this.ExportPopupMenu.ShowAll();
				this.ExportPopupMenu.Popup();
			}
		}

		/// <summary>
		/// Handles the select all item activated event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnSelectAllItemActivated(object sender, EventArgs e)
		{
			this.ItemExportListStore.Foreach
			(
				(model, path, iter) =>
				{
					this.ItemExportListStore.SetValue(iter, 0, true);
					return false;
				}
			);
		}

		/// <summary>
		/// Handles the select none item activated event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnSelectNoneItemActivated(object sender, EventArgs e)
		{
			this.ItemExportListStore.Foreach
			(
				(model, path, iter) =>
				{
					this.ItemExportListStore.SetValue(iter, 0, false);
					return false;
				}
			);
		}

		/// <summary>
		/// Handles the export mip toggle clicked event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnExportItemToggleClicked(object sender, ToggledArgs e)
		{
			TreeIter iter;
			this.ItemExportListStore.GetIterFromString(out iter, e.Path);

			bool currentValue = (bool)this.ItemExportListStore.GetValue(iter, 0);

			this.ItemExportListStore.SetValue(iter, 0, !currentValue);
		}

		/// <summary>
		/// Handles the OK button clicked event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		private void OnOkButtonClicked(object sender, EventArgs e)
		{
			RunExport();
		}
	}
}
