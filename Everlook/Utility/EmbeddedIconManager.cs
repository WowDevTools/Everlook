//
//  Program.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gdk;
using Gtk;

namespace Everlook.Utility
{
	/// <summary>
	/// Handles loading and providing embedded GTK icons.
	/// </summary>
	public static class EmbeddedIconManager
	{
		/// <summary>
		/// Loads all embedded builtin icons into the application's icon theme.
		/// </summary>
		public static void LoadBuiltInIcons()
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			string[] manifestResourceNames = executingAssembly
				.GetManifestResourceNames();
			IEnumerable<string> manifestIcons = manifestResourceNames
				.Where(path => path.Contains(".Icons.") && path.EndsWith(".png"));

			foreach (string manifestIcon in manifestIcons)
			{
				using (Stream iconStream = executingAssembly.GetManifestResourceStream(manifestIcon))
				{
					if (iconStream == null)
					{
						continue;
					}

					string iconNameWithExtension = manifestIcon.Substring(manifestIcon.IndexOf("Icons.", StringComparison.Ordinal) + "Icons.".Length);
					string iconName = Path.GetFileNameWithoutExtension(iconNameWithExtension);
					using (MemoryStream ms = new MemoryStream())
					{
						iconStream.CopyTo(ms);

						IconTheme.AddBuiltinIcon(iconName, 16, new Pixbuf(ms.ToArray(), 16, 16));
					}
				}
			}
		}
	}
}