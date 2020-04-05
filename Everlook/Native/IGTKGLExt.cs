//
//  IGTKGLExt.cs
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
using AdvancedDLSupport;

// ReSharper disable ExplicitCallerInfoArgument
namespace Everlook.Native
{
    /// <summary>
    /// Represents the native interface to the GTK GL Extensions library.
    /// </summary>
    public interface IGTKGLExt
    {
        /// <summary>
        /// Gets a function pointer to the named symbol via GDK.
        /// </summary>
        /// <param name="functionName">The name of the symbol.</param>
        /// <returns>A pointer to the symbol, or IntPtr.Zero.</returns>
        [NativeSymbol("gdk_gl_get_proc_address")]
        IntPtr GetProcAddress(string functionName);
    }
}
