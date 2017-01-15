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
using Everlook.Explorer;
using Everlook.Export.Image;
using Everlook.Utility;
using Gdk;
using Gtk;
using Warcraft.BLP;

namespace Everlook.UI
{
	/// <summary>
	/// Everlook image export dialog. The "partial" qualifier is not strictly needed, but prevents the compiler from
	/// generating errors about the autoconnected members that relate to UI elements.
	/// </summary>
	public partial class EverlookImageExportDialog : Dialog
	{
		/// <summary>
		/// The reference to the file in the package that is to be exported.
		/// </summary>
		private readonly FileReference ExportTarget;

		/// <summary>
		/// The image we're exporting.
		/// </summary>
		private BLP Image;

		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

		/// <summary>
		/// Creates an instance of the Image Export dialog, using the glade XML UI file.
		/// </summary>
		public static EverlookImageExportDialog Create(FileReference inExportTarget)
		{
			Builder builder = new Builder(null, "Everlook.interfaces.EverlookImageExport.glade", null);
			return new EverlookImageExportDialog(builder, builder.GetObject("EverlookImageExportDialog").Handle,
				inExportTarget);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="EverlookImageExportDialog"/> class.
		/// </summary>
		/// <param name="builder">Builder.</param>
		/// <param name="handle">Handle.</param>
		/// <param name="inExportTarget">In export target.</param>
		private EverlookImageExportDialog(Builder builder, IntPtr handle, FileReference inExportTarget)
			: base(handle)
		{
			builder.Autoconnect(this);

			this.ExportTarget = inExportTarget;
			/*
				 UI Setup
			*/
			this.ExportMipToggleRenderer.Toggled += OnExportMipToggleClicked;
			this.MipLevelListingTreeView.ButtonPressEvent += OnMipListingButtonPressed;
			this.SelectAllItem.Activated += OnSelectAllItemActivated;
			this.SelectNoneItem.Activated += OnSelectNoneItemActivated;

			LoadInformation();
		}

		private void LoadInformation()
		{
			string imageFilename = System.IO.Path.GetFileNameWithoutExtension(ExtensionMethods.ConvertPathSeparatorsToCurrentNativeSeparator(this.ExportTarget.FilePath));
			this.Title = $"Export Image | {imageFilename}";

			byte[] file = this.ExportTarget.Extract();
			this.Image = new BLP(file);

			this.ExportFormatComboBox.Active = (int) this.Config.GetDefaultImageFormat();

			this.MipLevelListStore.Clear();
			foreach (string mipString in this.Image.GetMipMapLevelStrings())
			{
				this.MipLevelListStore.AppendValues(true, mipString);
			}

			this.ExportDirectoryFileChooserButton.SetFilename(this.Config.GetDefaultExportDirectory());
		}

		/// <summary>
		/// Exports the mipmaps in the image.
		/// </summary>
		public void RunExport()
		{
			string imageFilename = System.IO.Path.GetFileNameWithoutExtension(this.ExportTarget.FilePath.ConvertPathSeparatorsToCurrentNativeSeparator());

			string exportPath = "";
			if (this.Config.GetShouldKeepFileDirectoryStructure())
			{
				exportPath =
					$"{this.ExportDirectoryFileChooserButton.Filename}{System.IO.Path.DirectorySeparatorChar}{this.ExportTarget.FilePath.ConvertPathSeparatorsToCurrentNativeSeparator().Replace(".blp", "")}";
			}
			else
			{
				exportPath = $"{this.ExportDirectoryFileChooserButton.Filename}{System.IO.Path.DirectorySeparatorChar}{imageFilename}";
			}


			int i = 0;
			this.MipLevelListStore.Foreach(delegate(ITreeModel model, TreePath path, TreeIter iter)
			{
				bool bShouldExport = (bool) this.MipLevelListStore.GetValue(iter, 0);

				if (bShouldExport)
				{
					string formatExtension = GetFileExtensionFromImageFormat((ImageFormat) this.ExportFormatComboBox.Active);
					System.IO.Directory.CreateDirectory(System.IO.Directory.GetParent(exportPath).FullName);

					string fullExportPath = $"{exportPath}_{i}.{formatExtension}";
					this.Image.GetMipMap((uint)i).Save(fullExportPath, GetSystemImageFormatFromImageFormat((ImageFormat) this.ExportFormatComboBox.Active));
				}

				++i;
				return false;
			});
		}

		/// <summary>
		/// Gets the system image format from image format.
		/// </summary>
		/// <returns>The system image format from image format.</returns>
		/// <param name="Format">Format.</param>
		private static System.Drawing.Imaging.ImageFormat GetSystemImageFormatFromImageFormat(ImageFormat Format)
		{
			switch (Format)
			{
				case ImageFormat.PNG:
					return System.Drawing.Imaging.ImageFormat.Png;
				case ImageFormat.JPG:
					return System.Drawing.Imaging.ImageFormat.Jpeg;
				case ImageFormat.TIF:
					return System.Drawing.Imaging.ImageFormat.Tiff;
				case ImageFormat.BMP:
					return System.Drawing.Imaging.ImageFormat.Bmp;
				default:
					return System.Drawing.Imaging.ImageFormat.Png;
			}
		}

		/// <summary>
		/// Gets the file extension from image format.
		/// </summary>
		/// <returns>The file extension from image format.</returns>
		/// <param name="Format">Format.</param>
		private static string GetFileExtensionFromImageFormat(ImageFormat Format)
		{
			switch (Format)
			{
				case ImageFormat.PNG:
					return "png";
				case ImageFormat.JPG:
					return "jpg";
				case ImageFormat.TIF:
					return "tif";
				case ImageFormat.BMP:
					return "bmp";
				default:
					return "png";
			}
		}

		/// <summary>
		/// Handles context menu spawning for the game explorer.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		[GLib.ConnectBefore]
		protected void OnMipListingButtonPressed(object sender, ButtonPressEventArgs e)
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
		protected void OnSelectAllItemActivated(object sender, EventArgs e)
		{
			this.MipLevelListStore.Foreach(delegate(ITreeModel model, TreePath path, TreeIter iter)
			{
				this.MipLevelListStore.SetValue(iter, 0, true);
				return false;
			});
		}

		/// <summary>
		/// Handles the select none item activated event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnSelectNoneItemActivated(object sender, EventArgs e)
		{
			this.MipLevelListStore.Foreach(delegate(ITreeModel model, TreePath path, TreeIter iter)
			{
				this.MipLevelListStore.SetValue(iter, 0, false);
				return false;
			});
		}

		/// <summary>
		/// Handles the export mip toggle clicked event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnExportMipToggleClicked(object sender, ToggledArgs e)
		{
			TreeIter Iter;
			this.MipLevelListStore.GetIterFromString(out Iter, e.Path);

			bool currentValue = (bool) this.MipLevelListStore.GetValue(Iter, 0);

			this.MipLevelListStore.SetValue(Iter, 0, !currentValue);
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

