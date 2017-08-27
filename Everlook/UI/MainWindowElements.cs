//
//  MainWindowElements.cs
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

using System.Collections.Generic;
using Everlook.Explorer;
using Gtk;
using OpenTK;
using UIElement = Gtk.Builder.ObjectAttribute;

// ReSharper disable UnassignedReadonlyField
#pragma warning disable 649
#pragma warning disable 1591
#pragma warning disable SA1134 // Each attribute should be placed on its own line of code
#pragma warning disable SA1214 // Readonly fields should appear before non-readonly fields

namespace Everlook.UI
{
	public sealed partial class MainWindow
	{
		/*
			Main UI elements
		*/
		[UIElement] private readonly ToolButton AboutButton;
		[UIElement] private readonly AboutDialog AboutDialog;
		[UIElement] private readonly ToolButton PreferencesButton;

		[UIElement] private readonly Paned ViewportPaned;
		[UIElement] private readonly Paned LowerBoxPaned;
		[UIElement] private readonly Alignment LowerBoxAlignment;

		[UIElement] private readonly Alignment ViewportAlignment;
		private readonly GLWidget ViewportWidget;
		private bool ViewportHasPendingRedraw;

		[UIElement] private readonly ComboBox FileFilterComboBox;

		[UIElement] private readonly Button CancelCurrentActionButton;

		/*
			Export queue elements
		*/
		[UIElement] private readonly TreeView ExportQueueTreeView;
		[UIElement] private readonly ListStore ExportQueueListStore;

		[UIElement] private readonly Menu QueueContextMenu;
		[UIElement] private readonly ImageMenuItem RemoveQueueItem;

		[UIElement] private readonly Button ClearExportQueueButton;
		[UIElement] private readonly Button RunExportQueueButton;

		/*
			Game explorer elements
		*/
		[UIElement] private readonly Notebook GameTabNotebook;
		private readonly List<GamePage> GamePages = new List<GamePage>();

		/*
			General item control elements
		*/

		[UIElement] private readonly Notebook ItemControlNotebook;

		/*
			Image control elements
		*/
		[UIElement] private readonly CheckButton RenderAlphaCheckButton;
		[UIElement] private readonly CheckButton RenderRedCheckButton;
		[UIElement] private readonly CheckButton RenderGreenCheckButton;
		[UIElement] private readonly CheckButton RenderBlueCheckButton;

		[UIElement] private readonly Label MipCountLabel;

		/*
			Status bar elements
		*/
		[UIElement] private readonly Statusbar MainStatusBar;
		[UIElement] private readonly Spinner StatusSpinner;

		/*
			Model control elements
		*/

		[UIElement] private readonly CheckButton RenderBoundsCheckButton;
		[UIElement] private readonly CheckButton RenderWireframeCheckButton;
		[UIElement] private readonly CheckButton RenderDoodadsCheckButton;

		[UIElement] private readonly ComboBox ModelVariationComboBox;
		[UIElement] private readonly ListStore ModelVariationListStore;
		[UIElement] private readonly CellRendererText ModelVariationTextRenderer;

		/*
			Animation control elements
		*/

		/*
			Audio control elements
		*/
	}
}
