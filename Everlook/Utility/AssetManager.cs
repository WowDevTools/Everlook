//
//  AssetManager.cs
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

using System.Drawing;
using System.IO;
using System.Reflection;
using Gdk;
using log4net;

namespace Everlook.Utility
{
	/// <summary>
	/// Handles loading and providing of assets. An asset is, in the context of Everlook, defined as
	/// any included, embedded, bundled or otherwise prepackaged texture, model, sound file, or other media.
	/// </summary>
	public class AssetManager
	{
		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(AssetManager));

		/// <summary>
		/// Loads an embedded image from the resource manifest of a specified width and height.
		/// </summary>
		/// <param name="resourceName">The name of the resource to load.</param>
		/// <param name="width">The width of the output <see cref="Pixbuf"/>.</param>
		/// <param name="height">The height of the output <see cref="Pixbuf"/>.</param>
		/// <returns>A pixel buffer containing the image.</returns>
		public static Pixbuf LoadEmbeddedImage(string resourceName, int width = 16, int height = 16)
		{
			using (Stream vectorStream =
				Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
			{
				if (vectorStream == null)
				{
					return null;
				}

				using (MemoryStream ms = new MemoryStream())
				{
					vectorStream.CopyTo(ms);

					return new Pixbuf(ms.ToArray(), width, height);
				}
			}
		}

		/// <summary>
		/// Loads an embedded image from the resource manifest.
		/// </summary>
		/// <param name="resourceName">The name of the resource to load.</param>
		public static Bitmap LoadEmbeddedImage(string resourceName)
		{
			using (Stream vectorStream =
				Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
			{
				if (vectorStream == null)
				{
					return null;
				}

				return new Bitmap(vectorStream);
			}
		}

		/// <summary>
		/// Loads an embedded text resource.
		/// </summary>
		/// <param name="resourcePath">The path of the resource.</param>
		/// <returns>The contents of the file.</returns>
		public static string LoadEmbeddedText(string resourcePath)
		{
			string contents;
			using (Stream contentStream =
				Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath))
			{
				if (contentStream == null)
				{
					return null;
				}

				using (StreamReader sr = new StreamReader(contentStream))
				{
					contents = sr.ReadToEnd();
				}
			}

			return contents;
		}
	}
}