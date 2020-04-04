//
//  CellRenderers.cs
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

using System.IO;
using System.Text.RegularExpressions;
using Everlook.Explorer;
using Everlook.Utility;
using FileTree.Tree.Nodes;
using Gtk;
using ListFile;
using Warcraft.Core;

namespace Everlook.UI.Helpers
{
    /// <summary>
    /// Container class for different cell renderer functions.
    /// </summary>
    public static class CellRenderers
    {
        /// <summary>
        /// Renders the name of a model variation in the variation dropdown.
        /// </summary>
        /// <param name="cellLayout">The layout of the cell.</param>
        /// <param name="cell">The cell.</param>
        /// <param name="model">The model of the combobox.</param>
        /// <param name="iter">The iter pointing to the rendered row.</param>
        public static void RenderModelVariationName
        (
            ICellLayout cellLayout,
            CellRenderer cell,
            ITreeModel model,
            TreeIter iter
        )
        {
            var cellText = cell as CellRendererText;
            if (cellText == null)
            {
                return;
            }

            var storedText = (string)model.GetValue(iter, 0);

            // Builtin override for the standard set name
            if (storedText.ToLowerInvariant().Contains("set_$defaultglobal"))
            {
                cellText.Text = "Default";
                return;
            }

            var transientText = storedText.FastReplaceCaseInsensitive("set_", string.Empty);

            // Insert spaces between words and abbreviations
            transientText = Regex.Replace
            (
                transientText,
                @"(\B[A-Z0-9]+?(?=[A-Z][^A-Z])|\B[A-Z0-9]+?(?=[^A-Z]))", " $1"
            );

            cellText.Text = transientText;
        }

        /// <summary>
        /// Renders the name of a file reference in the export queue.
        /// </summary>
        /// <param name="column">The column which the cell is in.</param>
        /// <param name="cell">The cell which the reference is in.</param>
        /// <param name="model">The model of the treeview.</param>
        /// <param name="iter">The <see cref="TreeIter"/> pointing to the row the reference is in.</param>
        public static void RenderExportQueueReferenceName
        (
            TreeViewColumn column,
            CellRenderer cell,
            ITreeModel model,
            TreeIter iter
        )
        {
            var cellText = cell as CellRendererText;
            var reference = (FileReference)model.GetValue(iter, 0);

            if (reference == null || cellText == null)
            {
                return;
            }

            cellText.Text = reference.FilePath.Replace('\\', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Renders the icon of a file reference in the export queue.
        /// </summary>
        /// <param name="column">The column which the cell is in.</param>
        /// <param name="cell">The cell which the icon is in.</param>
        /// <param name="model">The model of the treeview.</param>
        /// <param name="iter">The <see cref="TreeIter"/> pointing to the row the icon is in.</param>
        public static void RenderExportQueueReferenceIcon
        (
            TreeViewColumn column,
            CellRenderer cell,
            ITreeModel model,
            TreeIter iter
        )
        {
            var cellIcon = cell as CellRendererPixbuf;
            var reference = (FileReference)model.GetValue(iter, 0);

            if (reference == null || cellIcon == null)
            {
                return;
            }

            if (reference.Node.Type.HasFlag(NodeType.Directory))
            {
                cellIcon.Pixbuf = IconManager.GetIconForFiletype(WarcraftFileType.Directory);
                return;
            }

            cellIcon.Pixbuf = IconManager.GetIconForFiletype(reference.Node.FileType);
        }
    }
}
