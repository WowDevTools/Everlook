﻿//
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
using System.Threading;
using Gtk;
namespace Everlook.UI
{
	/// <summary>
	/// The main dialog that is shown when the program is loading games. External processes update the information
	/// on it.
	/// </summary>
	public partial class EverlookGameLoadingDialog : Gtk.Dialog
	{
		/// <summary>
		/// The source of the cancellation token associated with this dialog.
		/// </summary>
		public CancellationTokenSource CancellationSource { get;}

		private readonly List<string> Jokes = new List<string>();

		private readonly uint JokeTimeoutID;

		/// <summary>
		/// Creates a new dialog with the given window as its parent.
		/// </summary>
		/// <param name="parent"></param>
		/// <returns></returns>
		public static EverlookGameLoadingDialog Create(Window parent)
		{
			Builder builder = new Builder(null, "Everlook.interfaces.EverlookGameLoadingDialog.glade", null);
			return new EverlookGameLoadingDialog(builder, builder.GetObject("GameLoadingDialog").Handle, parent);
		}

		private EverlookGameLoadingDialog(Builder builder, IntPtr handle, Window parent) : base(handle)
		{
			builder.Autoconnect(this);
			this.TransientFor = parent;

			this.CancellationSource = new CancellationTokenSource();

			this.CancelGameLoadingButton.Pressed += (o, args) =>
			{
				this.GameLoadingDialogLabel.Text = "Cancelling...";
				this.CancelGameLoadingButton.Sensitive = false;

				this.CancellationSource.Cancel();
			};

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

			this.JokeTimeoutID = GLib.Timeout.Add(6000, () =>
			{
				RefreshJoke();
				return true;
			});
		}

		/// <summary>
		/// Picks a new random joke to display, and replaces the current one.
		/// </summary>
		public void RefreshJoke()
		{
			this.AdditionalInfoLabel.Markup = this.Jokes[new Random().Next(this.Jokes.Count)];
		}

		/// <summary>
		/// Sets the fraction of the dialog's loading bar.
		/// </summary>
		/// <param name="fraction"></param>
		public void SetFraction(double fraction)
		{
			this.GameLoadingProgressBar.Fraction = fraction;
		}

		/// <summary>
		/// Sets the status message of the dialog.
		/// </summary>
		/// <param name="statusMessage"></param>
		public void SetStatusMessage(string statusMessage)
		{
			this.GameLoadingDialogLabel.Text = statusMessage;
		}

		/// <summary>
		/// Destroys the dialog.
		/// </summary>
		public override void Destroy()
		{
			base.Destroy();

			GLib.Timeout.Remove(this.JokeTimeoutID);
		}
	}
}