//
//  IconManager.cs
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
using System.IO;
using System.Linq;
using System.Reflection;
using Gdk;
using GLib;
using Gtk;
using log4net;
using Warcraft.Core;

namespace Everlook.Utility
{
    /// <summary>
    /// Handles loading and providing embedded GTK icons.
    /// </summary>
    public static class IconManager
    {
        /// <summary>
        /// Logger instance for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(IconManager));

        private static readonly Dictionary<(string IconName, int IconSize), Pixbuf> IconCache =
            new Dictionary<(string IconName, int IconSize), Pixbuf>();

        private static readonly Dictionary<WarcraftFileType, string> KnownIconTypes = new Dictionary<WarcraftFileType, string>
        {
            { WarcraftFileType.Directory,              Stock.Directory },
            { WarcraftFileType.MoPaQArchive,           "package-x-generic" },
            { WarcraftFileType.TerrainTable,           "x-office-spreadsheet" },
            { WarcraftFileType.DatabaseContainer,      "x-office-spreadsheet" },
            { WarcraftFileType.Hashmap,                "x-office-spreadsheet" },
            { WarcraftFileType.TerrainWater,           "Blender-Wave-Icon" },
            { WarcraftFileType.TerrainLiquid,          "Blender-Wave-Icon" },
            { WarcraftFileType.TerrainLevel,           "text-x-generic-template" },
            { WarcraftFileType.TerrainData,            "Blender-Planet-Icon" },
            { WarcraftFileType.GameObjectModel,        "Blender-Armature-Icon" },
            { WarcraftFileType.WorldObjectModel,       "Blender-Object-Icon" },
            { WarcraftFileType.WorldObjectModelGroup,  "Blender-Object-Icon" },
            { WarcraftFileType.WaveAudio,              "audio-x-generic" },
            { WarcraftFileType.MP3Audio,               "audio-x-generic" },
            { WarcraftFileType.VorbisAudio,            "audio-x-generic" },
            { WarcraftFileType.WMAAudio,               "audio-x-generic" },
            { WarcraftFileType.Subtitles,              "gnome-subtitles" },
            { WarcraftFileType.Text,                   "text-x-generic" },
            { WarcraftFileType.AddonManifest,          "text-x-generic" },
            { WarcraftFileType.AddonManifestSignature, "text-x-generic" },
            { WarcraftFileType.GIFImage,               "image-gif" },
            { WarcraftFileType.PNGImage,               "image-png" },
            { WarcraftFileType.JPGImage,               "image-jpeg" },
            { WarcraftFileType.IconImage,              "image-x-ico" },
            { WarcraftFileType.BitmapImage,            "image-bmp" },
            { WarcraftFileType.BinaryImage,            "image-x-generic" },
            { WarcraftFileType.TargaImage,             "image-x-generic" },
            { WarcraftFileType.PDF,                    "application-pdf" },
            { WarcraftFileType.Web,                    "text-html" },
            { WarcraftFileType.Assembly,               "application-x-executable" },
            { WarcraftFileType.Font,                   "font-x-generic" },
            { WarcraftFileType.Animation,              "Blender-JumpingToon-Icon" },
            { WarcraftFileType.Physics,                "Blender-Deform-Icon" },
            { WarcraftFileType.Skeleton,               "Blender-Skeleton-Icon" },
            { WarcraftFileType.DataCache,              "text-x-sql" },
            { WarcraftFileType.INI,                    "utilities-terminal" },
            { WarcraftFileType.ConfigurationFile,      "utilities-terminal" },
            { WarcraftFileType.Script,                 "utilities-terminal" },
            { WarcraftFileType.Lighting,               "Blender-Sun-Icon" },
            { WarcraftFileType.Shader,                 "Blender-Shader-Icon" },
            { WarcraftFileType.XML,                    "text-xml" }
        };

        /// <summary>
        /// Loads all embedded builtin icons into the application's icon theme.
        /// </summary>
        public static void LoadEmbeddedIcons()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var manifestResourceNames = executingAssembly
                .GetManifestResourceNames();

            var manifestIcons = manifestResourceNames.Where
            (
                path =>
                    path.Contains(".Icons.") &&
                    (
                        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                    )
            );

            if (!Pixbuf.Formats.Select(f => f.Name).Any(n => n.ToLowerInvariant().Contains("svg")))
            {
                // No SVG support, use PNG fallbacks
                Log.Warn("No SVG support detected. Using PNG fallbacks for bundled icons.");
                manifestIcons = manifestIcons.Where(path => path.EndsWith(".png"));
            }

            foreach (var manifestIconName in manifestIcons)
            {
                // Grab the second to last part of the resource name, that is, the filename before the extension.
                // Note that this assumes that there is only a single extension.
                var manifestNameParts = manifestIconName.Split('.');
                var iconName = manifestNameParts.ElementAt(manifestNameParts.Length - 2);

                var iconBuffer = LoadEmbeddedImage(manifestIconName);
                if (iconBuffer != null)
                {
                    IconTheme.AddBuiltinIcon(iconName, 16, iconBuffer);
                }
            }
        }

        /// <summary>
        /// Loads an embedded image from the resource manifest of a specified width and height.
        /// </summary>
        /// <param name="resourceName">The name of the resource to load.</param>
        /// <param name="width">The width of the output <see cref="Pixbuf"/>.</param>
        /// <param name="height">The height of the output <see cref="Pixbuf"/>.</param>
        /// <returns>A pixel buffer containing the image.</returns>
        private static Pixbuf? LoadEmbeddedImage(string resourceName, int width = 16, int height = 16)
        {
            using var vectorStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (vectorStream is null)
            {
                return null;
            }

            using var ms = new MemoryStream();
            vectorStream.CopyTo(ms);

            try
            {
                return new Pixbuf(ms.ToArray(), width, height);
            }
            catch (GException gex)
            {
                Log.Error($"Failed to load resource \"{resourceName}\" due to a GException: {gex}");
                return null;
            }
        }

        /// <summary>
        /// Gets the specified icon as a pixel buffer. If the icon is not found in the current theme, or
        /// if loading should fail for any other reason, a default icon will be returned instead.
        /// </summary>
        /// <param name="iconName">The name of the icon.</param>
        /// <returns>A pixel buffer containing the icon.</returns>
        public static Pixbuf GetIcon(string iconName)
        {
            try
            {
                return LoadIconPixbuf(iconName);
            }
            catch (GException gex)
            {
                Log.Warn
                (
                    $"Loading of icon \"{iconName}\" failed. Exception message: {gex.Message}\n" +
                    $"A fallback icon will be used instead."
                );

                return LoadIconPixbuf("empty");
            }
        }

        /// <summary>
        /// Gets the icon that would best represent the provided file. This is
        /// usually the mimetype.
        /// </summary>
        /// <returns>The icon for the filetype.</returns>
        /// <param name="file">Reference.</param>
        public static Pixbuf GetIconForFiletype(string file)
        {
            return GetIconForFiletype(FileInfoUtilities.GetFileType(file));
        }

        /// <summary>
        /// Gets the icon that would best represent the provided file. This is
        /// usually the mimetype.
        /// </summary>
        /// <returns>The icon for the filetype.</returns>
        /// <param name="fileType">The file type.</param>
        public static Pixbuf GetIconForFiletype(WarcraftFileType fileType)
        {
            if (KnownIconTypes.TryGetValue(fileType, out var result))
            {
                return GetIcon(result);
            }

            return GetIcon(Stock.File);
        }

        /// <summary>
        /// Loads the pixel buffer for the specified icon. This method is unchecked and can
        /// throw exceptions.
        /// </summary>
        /// <param name="iconName">The name of the icon.</param>
        /// <param name="size">The desired size of the icon.</param>
        /// <exception cref="GException">
        /// Thrown for a number of reasons, but can be thrown if the icon is not present
        /// in the current icon theme.
        /// </exception>
        /// <returns>A pixel buffer containing the icon.</returns>
        private static Pixbuf LoadIconPixbuf(string iconName, int size = 16)
        {
            var key = (iconName, size);
            if (IconCache.ContainsKey(key))
            {
                return IconCache[key];
            }

            var icon = IconTheme.Default.LoadIcon(iconName, size, IconLookupFlags.UseBuiltin);
            IconCache.Add(key, icon);

            return IconCache[key];
        }
    }
}
