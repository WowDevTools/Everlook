//
//  EverlookImageExportDialog.cs
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
using Everlook.Configuration;
using Everlook.Package;
using Gdk;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;

namespace Everlook.Export.Directory
{
	/// <summary>
	/// Everlook directory export dialog. The "partial" qualifier is not strictly needed, but prevents the compiler from 
	/// generating errors about the autoconnected members that relate to UI elements.
	/// </summary>
	public partial class EverlookDirectoryExportDialog : Dialog
	{
		[UI] ListStore ItemExportListStore;
		[UI] TreeView ItemListingTreeView;
		[UI] CellRendererToggle ExportItemToggleRenderer;

		[UI] Menu ExportPopupMenu;
		[UI] ImageMenuItem SelectAllItem;
		[UI] ImageMenuItem SelectNoneItem;

		[UI] FileChooserButton ExportDirectoryFileChooserButton;

		/// <summary>
		/// The reference to the file in the package that is to be exported.
		/// </summary>
		private readonly ItemReference ExportTarget;

		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

		/// <summary>
		/// Creates an instance of the Image Export dialog, using the glade XML UI file.
		/// </summary>
		public static EverlookDirectoryExportDialog Create(ItemReference InExportTarget)
		{
			Builder builder = new Builder(null, "Everlook.interfaces.EverlookDirectoryExport.glade", null);
			return new EverlookDirectoryExportDialog(builder, builder.GetObject("EverlookDirectoryExportDialog").Handle, 
				InExportTarget);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Export.Image.EverlookImageExportDialog"/> class.
		/// </summary>
		/// <param name="builder">Builder.</param>
		/// <param name="handle">Handle.</param>
		/// <param name="InExportTarget">In export target.</param>
		protected EverlookDirectoryExportDialog(Builder builder, IntPtr handle, ItemReference InExportTarget)
			: base(handle)
		{
			builder.Autoconnect(this);

			this.ExportTarget = InExportTarget;

			/*
				 UI Setup
			*/
			ExportItemToggleRenderer.Toggled += OnExportItemToggleClicked;
			ItemListingTreeView.ButtonPressEvent += OnItemListingButtonPressed;
			SelectAllItem.Activated += OnSelectAllItemActivated;
			SelectNoneItem.Activated += OnSelectNoneItemActivated;

			LoadInformation();
		}

		private void LoadInformation()
		{
			this.Title = "Export Directory | " + ExportTarget.GetReferencedItemName();
			ExportDirectoryFileChooserButton.SetFilename(Config.GetDefaultExportDirectory());

			// Load all references
		}

		/// <summary>
		/// Exports the mipmaps in the image.
		/// </summary>
		public void RunExport()
		{
			int i = 0;
			ItemExportListStore.Foreach(new TreeModelForeachFunc(delegate(ITreeModel model, TreePath path, TreeIter iter)
					{
						bool bShouldExport = (bool)ItemExportListStore.GetValue(iter, 0);

						if (bShouldExport)
						{

						}

						++i;
						return false;
					}));
		}

		/// <summary>
		/// Handles context menu spawning for the game explorer.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		[GLib.ConnectBefore]
		protected void OnItemListingButtonPressed(object sender, ButtonPressEventArgs e)
		{
			if (e.Event.Type == EventType.ButtonPress && e.Event.Button == 3)
			{
				ExportPopupMenu.ShowAll();
				ExportPopupMenu.Popup();
			}
		}

		/// <summary>
		/// Handles the select all item activated event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnSelectAllItemActivated(object sender, EventArgs e)
		{
			ItemExportListStore.Foreach(new TreeModelForeachFunc(delegate(ITreeModel model, TreePath path, TreeIter iter)
					{
						ItemExportListStore.SetValue(iter, 0, true);
						return false;
					}));
		}

		/// <summary>
		/// Handles the select none item activated event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnSelectNoneItemActivated(object sender, EventArgs e)
		{
			ItemExportListStore.Foreach(new TreeModelForeachFunc(delegate(ITreeModel model, TreePath path, TreeIter iter)
					{
						ItemExportListStore.SetValue(iter, 0, false);
						return false;
					}));
		}

		/// <summary>
		/// Handles the export mip toggle clicked event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnExportItemToggleClicked(object sender, ToggledArgs e)
		{
			TreeIter Iter;
			ItemExportListStore.GetIterFromString(out Iter, e.Path);

			bool currentValue = (bool)ItemExportListStore.GetValue(Iter, 0);

			ItemExportListStore.SetValue(Iter, 0, !currentValue);
		}

		/// <summary>
		/// Handles the OK button clicked event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnOKButtonClicked(object sender, EventArgs e)
		{
			RunExport();
		}
	}
}

