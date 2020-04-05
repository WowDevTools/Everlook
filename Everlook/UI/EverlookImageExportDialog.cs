//
//  EverlookImageExportDialog.cs
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
using Everlook.Explorer;
using Everlook.Export.Image;
using Everlook.Utility;
using Gdk;
using Gtk;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using Warcraft.BLP;

using IOPath = System.IO.Path;
using SystemImageFormat = System.Drawing.Imaging.ImageFormat;

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
        private readonly FileReference _exportTarget;

        private readonly EverlookConfiguration _config = EverlookConfiguration.Instance;

        /// <summary>
        /// The image we're exporting.
        /// </summary>
        private BLP _image = null!;

        /// <summary>
        /// Creates an instance of the Image Export dialog, using the glade XML UI file.
        /// </summary>
        /// <param name="inExportTarget">The reference which is to be exported.</param>
        /// <returns>An initialized instance of the EverlookImageExportDialog class.</returns>
        public static EverlookImageExportDialog Create(FileReference inExportTarget)
        {
            using var builder = new Builder(null, "Everlook.interfaces.EverlookImageExport.glade", null);
            return new EverlookImageExportDialog
            (
                builder,
                builder.GetObject("EverlookImageExportDialog").Handle,
                inExportTarget
            );
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

            _exportTarget = inExportTarget;
            /*
                 UI Setup
            */
            _exportMipToggleRenderer.Toggled += OnExportMipToggleClicked;
            _mipLevelListingTreeView.ButtonPressEvent += OnMipListingButtonPressed;
            _selectAllItem.Activated += OnSelectAllItemActivated;
            _selectNoneItem.Activated += OnSelectNoneItemActivated;

            LoadInformation();
        }

        /// <summary>
        /// Loads the information from the image into the UI.
        /// </summary>
        private void LoadInformation()
        {
            var imageFilename = IOPath.GetFileNameWithoutExtension
            (
                _exportTarget.FilePath.ConvertPathSeparatorsToCurrentNativeSeparator()
            );

            this.Title = $"Export Image | {imageFilename}";

            if (!_exportTarget.TryExtract(out var file))
            {
                throw new ArgumentException("The file data could not be extracted.", nameof(_exportTarget));
            }

            _image = new BLP(file);

            _exportFormatComboBox.Active = (int)_config.DefaultImageExportFormat;

            _mipLevelListStore.Clear();
            foreach (var mipString in _image.GetMipMapLevelStrings())
            {
                _mipLevelListStore.AppendValues(true, mipString);
            }

            _exportDirectoryFileChooserButton.SetFilename(_config.DefaultExportDirectory);
        }

        /// <summary>
        /// Exports the mipmaps in the image.
        /// </summary>
        public void RunExport()
        {
            var imageFilename = IOPath.GetFileNameWithoutExtension
            (
                _exportTarget.FilePath.ConvertPathSeparatorsToCurrentNativeSeparator()
            );

            string exportPath;
            if (_config.KeepFileDirectoryStructure)
            {
                exportPath = IOPath.Combine
                (
                    _exportDirectoryFileChooserButton.Filename,
                    _exportTarget.FilePath.ConvertPathSeparatorsToCurrentNativeSeparator().Replace(".blp", string.Empty)
                );
            }
            else
            {
                exportPath = IOPath.Combine
                (
                    _exportDirectoryFileChooserButton.Filename,
                    imageFilename
                );
            }

            var i = 0;
            _mipLevelListStore.Foreach
            (
                (model, path, iter) =>
                {
                    var shouldExport = (bool)_mipLevelListStore.GetValue(iter, 0);

                    if (shouldExport)
                    {
                        var formatExtension = GetFileExtensionFromImageFormat
                        (
                            (ImageFormat)_exportFormatComboBox.Active
                        );

                        Directory.CreateDirectory(Directory.GetParent(exportPath).FullName);

                        var fullExportPath = $"{exportPath}_{i}.{formatExtension}";

                        using var fs = File.OpenWrite(fullExportPath);
                        _image.GetMipMap((uint)i).Save
                        (
                            fs,
                            GetImageEncoderFromFormat((ImageFormat)_exportFormatComboBox.Active)
                        );
                    }

                    ++i;
                    return false;
                }
            );
        }

        /// <summary>
        /// Gets the system image format from image format.
        /// </summary>
        /// <returns>The system image format from image format.</returns>
        /// <param name="format">Format.</param>
        private static IImageEncoder GetImageEncoderFromFormat(ImageFormat format)
        {
            switch (format)
            {
                case ImageFormat.PNG:
                    return new PngEncoder();
                case ImageFormat.JPG:
                    return new JpegEncoder();
                case ImageFormat.BMP:
                    return new BmpEncoder();
                default:
                    return new PngEncoder();
            }
        }

        /// <summary>
        /// Gets the file extension from image format.
        /// </summary>
        /// <returns>The file extension from image format.</returns>
        /// <param name="format">Format.</param>
        private static string GetFileExtensionFromImageFormat(ImageFormat format)
        {
            switch (format)
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
        protected void OnMipListingButtonPressed(object? sender, ButtonPressEventArgs e)
        {
            if (e.Event.Type != EventType.ButtonPress || e.Event.Button != 3)
            {
                return;
            }

            _exportPopupMenu.ShowAll();

            _exportPopupMenu.PopupAtPointer(e.Event);
        }

        /// <summary>
        /// Handles the select all item activated event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        protected void OnSelectAllItemActivated(object? sender, EventArgs e)
        {
            _mipLevelListStore.Foreach
            (
                (model, path, iter) =>
                {
                    _mipLevelListStore.SetValue(iter, 0, true);
                    return false;
                }
            );
        }

        /// <summary>
        /// Handles the select none item activated event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        protected void OnSelectNoneItemActivated(object? sender, EventArgs e)
        {
            _mipLevelListStore.Foreach
            (
                (model, path, iter) =>
                {
                    _mipLevelListStore.SetValue(iter, 0, false);
                    return false;
                }
            );
        }

        /// <summary>
        /// Handles the export mip toggle clicked event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        protected void OnExportMipToggleClicked(object? sender, ToggledArgs e)
        {
            TreeIter iter;
            _mipLevelListStore.GetIterFromString(out iter, e.Path);

            var currentValue = (bool)_mipLevelListStore.GetValue(iter, 0);

            _mipLevelListStore.SetValue(iter, 0, !currentValue);
        }

        /// <summary>
        /// Handles the OK button clicked event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        protected void OnOkButtonClicked(object? sender, EventArgs e)
        {
            RunExport();
        }
    }
}
