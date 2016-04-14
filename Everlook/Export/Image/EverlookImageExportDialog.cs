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
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;
using Everlook.Configuration;
using Everlook.Export.Image;
using Everlook.Package;
using System.IO;
using System.Drawing;
using Warcraft.BLP;
using Warcraft.MPQ.FileInfo;
using Everlook.Utility;


namespace Everlook.Export.Image
{
	/// <summary>
	/// Everlook image export dialog. The "partial" qualifier is not strictly needed, but prevents the compiler from 
	/// generating errors about the autoconnected members that relate to UI elements.
	/// </summary>
	public partial class EverlookImageExportDialog : Dialog
	{
		[UI] TextBuffer ImageInformationTextBuffer;
		[UI] ListStore MipLevelListStore;

		[UI] ComboBox ExportFormatComboBox;
		[UI] CheckButton ExportAllMipsCheckButton;
		[UI] ComboBox MipStartComboBox;
		[UI] ComboBox MipEndComboBox;
		[UI] FileChooserButton ExportDirectoryFileChooserButton;

		[UI] Button OKButton;

		/// <summary>
		/// The reference to the file in the package that is to be exported.
		/// </summary>
		private readonly ItemReference ExportTarget;

		/// <summary>
		/// The package path.
		/// </summary>
		private readonly string PackagePath;

		/// <summary>
		/// The interaction handler.
		/// </summary>
		private readonly PackageInteractionHandler InteractionHandler;

		/// <summary>
		/// The image we're exporting.
		/// </summary>
		private BLP Image;

		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

		/// <summary>
		/// Creates an instance of the Image Export dialog, using the glade XML UI file.
		/// </summary>
		public static EverlookImageExportDialog Create(ItemReference InExportTarget, string InPackagePath)
		{
			Builder builder = new Builder(null, "Everlook.interfaces.EverlookImageExport.glade", null);
			return new EverlookImageExportDialog(builder, builder.GetObject("EverlookImageExportDialog").Handle, 
				InExportTarget, InPackagePath);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Export.Image.EverlookImageExportDialog"/> class.
		/// </summary>
		/// <param name="builder">Builder.</param>
		/// <param name="handle">Handle.</param>
		/// <param name="InExportTarget">In export target.</param>
		/// <param name="InPackagePath">In package path.</param>
		protected EverlookImageExportDialog(Builder builder, IntPtr handle, ItemReference InExportTarget, 
		                                    string InPackagePath)
			: base(handle)
		{
			builder.Autoconnect(this);

			this.ExportTarget = InExportTarget;
			this.PackagePath = InPackagePath;

			this.InteractionHandler = new PackageInteractionHandler(this.PackagePath);

			/*
				 UI Setup
			*/
			LoadInformation();
			//OKButton.Clicked += OnOKButtonClicked;
			ExportAllMipsCheckButton.Clicked += OnExportAllMipsButtonClicked;
		}

		private void LoadInformation()
		{
			byte[] file = this.InteractionHandler.ExtractReference(ExportTarget);
			Image = new BLP(file);

			MPQFileInfo ImageInfo = this.InteractionHandler.GetReferenceInfo(ExportTarget);

			ImageInformationTextBuffer.Text = String.Format(
				"Image Version: {0}\n" +
				"Image Size (package): {1}\n" +
				"Image Size (disk): {2}\n" +
				"Storage Format: {3}\n\n" +
				"" +
				"Flags: {4}", 
				Image.GetFormat(), 
				ImageInfo.GetStoredSize(),
				ImageInfo.GetActualSize(),
				Image.GetCompressionType(),
				ImageInfo.GetFlags());

			ExportFormatComboBox.Active = (int)Config.GetDefaultImageFormat();
			
			ExportAllMipsCheckButton.Active = false;

			MipLevelListStore.Clear();
			foreach (string mipString in Image.GetMipMapLevelStrings())
			{
				MipLevelListStore.AppendValues(mipString);
			}

			MipStartComboBox.Sensitive = true;
			MipStartComboBox.Active = 0;

			MipEndComboBox.Sensitive = true;
			MipEndComboBox.Active = 0;

			ExportDirectoryFileChooserButton.SetFilename(Config.GetDefaultExportDirectory());
		}

		private void ExportImage()
		{
			string ImageFilename = System.IO.Path.GetFileNameWithoutExtension(Utilities.CleanPath(ExportTarget.ItemPath));

			string ExportPath = ExportDirectoryFileChooserButton.Filename + System.IO.Path.DirectorySeparatorChar +
			                    ImageFilename;

			if (ExportAllMipsCheckButton.Active)
			{
				for (int i = 0; i <= Image.GetMipMapCount(); ++i)
				{
					Image.GetMipMap((uint)i).Save(ExportPath + "_" + i, 
						GetSystemImageFormatFromImageFormat((ImageFormat)ExportFormatComboBox.Active));
				}
			}
			else
			{
				for (int i = MipStartComboBox.Active; i <= MipEndComboBox.Active; ++i)
				{
					Image.GetMipMap((uint)i).Save(ExportPath + "_" + i, 
						GetSystemImageFormatFromImageFormat((ImageFormat)ExportFormatComboBox.Active));
				}
			}
		}


		private System.Drawing.Imaging.ImageFormat GetSystemImageFormatFromImageFormat(ImageFormat Format)
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
		/// Handles the OK button clicked event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnOKButtonClicked(object sender, EventArgs e)
		{
			ExportImage();
		}

		/// <summary>
		/// Handles the export all mips button clicked event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnExportAllMipsButtonClicked(object sender, EventArgs e)
		{
			if (ExportAllMipsCheckButton.Active)
			{
				MipStartComboBox.Sensitive = false;			
				MipEndComboBox.Sensitive = false;			
			}
			else
			{
				MipStartComboBox.Sensitive = false;			
				MipEndComboBox.Sensitive = false;
			}
		}

		/// <summary>
		/// Disposes the object and the underlying base class.
		/// </summary>
		/// <param name="disposing">If set to <c>true</c> disposing.</param>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (InteractionHandler != null)
			{
				InteractionHandler.Dispose();
			}
		}
	}
}

