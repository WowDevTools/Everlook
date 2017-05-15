//
//  EverlookPreferencesElements.cs
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

using Gtk;
using UIElement = Gtk.Builder.ObjectAttribute;

namespace Everlook.UI
{
	public partial class EverlookPreferences
	{
		[UIElement] private Dialog NewGamePathDialog;
		[UIElement] private Entry AliasEntry;
		[UIElement] private FileChooserButton PathChooser;

		[UIElement] private TreeView GamePathSelectionTreeView;
		[UIElement] private ListStore GamePathListStore;
		[UIElement] private Button AddPathButton;
		[UIElement] private Button RemovePathButton;

		[UIElement] private ColorButton ViewportColourButton;
		//[UIElement] CheckButton ShowUnknownFilesCheckButton;

		[UIElement] private FileChooserButton DefaultExportDirectoryFileChooserButton;
		[UIElement] private ComboBox DefaultModelExportFormatComboBox;
		[UIElement] private ComboBox DefaultImageExportFormatComboBox;
		[UIElement] private ComboBox DefaultAudioExportFormatComboBox;
		[UIElement] private CheckButton KeepDirectoryStructureCheckButton;

		[UIElement] private CheckButton SendStatsCheckButton;
	}
}