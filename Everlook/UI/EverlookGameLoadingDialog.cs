//
//  EverlookGameLoadingDialog.cs
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
using System.Reflection;
using Gtk;
namespace Everlook.UI
{
	public partial class EverlookGameLoadingDialog : Gtk.Dialog
	{
		private readonly List<string> Jokes = new List<string>();

		public static EverlookGameLoadingDialog Create(Window parent)
		{
			Builder builder = new Builder(null, "Everlook.interfaces.EverlookGameLoadingDialog.glade", null);
			return new EverlookGameLoadingDialog(builder, builder.GetObject("GameLoadingDialog").Handle, parent);
		}

		private EverlookGameLoadingDialog(Builder builder, IntPtr handle, Window parent) : base(handle)
		{
			builder.Autoconnect(this);
			this.TransientFor = parent;

			using (Stream shaderStream =
				Assembly.GetExecutingAssembly().GetManifestResourceStream("Everlook.Content.jokes.txt"))
			{
				if (shaderStream == null)
				{
					return;
				}

				using (StreamReader sr = new StreamReader(shaderStream))
				{
					while (sr.BaseStream.Length > sr.BaseStream.Position)
					{
						// Add italics to all jokes. Jokes are in Pango markup format
						this.Jokes.Add("<i>" + sr.ReadLine() + "</i>");
					}
				}
			}

			RefreshJoke();
		}

		/// <summary>
		/// Picks a new random joke to display, and replaces the current one.
		/// </summary>
		public void RefreshJoke()
		{
			this.AdditionalInfoLabel.Markup = this.Jokes[new Random().Next(this.Jokes.Count)];
		}
	}
}