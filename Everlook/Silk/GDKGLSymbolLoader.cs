//
//  GDKGLSymbolLoader.cs
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
using System.Runtime.InteropServices;
using AdvancedDLSupport;
using Everlook.Native;
using Silk.NET.Core.Loader;

namespace Everlook.Silk
{
    /// <summary>
    /// Handles loading OpenGL symbols via GDK.
    /// </summary>
    public class GDKGLSymbolLoader : GLSymbolLoader
    {
        private readonly IGTKGLExt _gtkglExt;

        /// <summary>
        /// Initializes a new instance of the <see cref="GDKGLSymbolLoader"/> class.
        /// </summary>
        public GDKGLSymbolLoader()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _gtkglExt = NativeLibraryBuilder.Default.ActivateInterface<IGTKGLExt>("libgtkglext-x11-1.0");
            }
            else
            {
                _gtkglExt = NativeLibraryBuilder.Default.ActivateInterface<IGTKGLExt>("gtkglext-1.0");
            }
        }

        /// <inheritdoc/>
        protected override IntPtr CoreLoadFunctionPointer(IntPtr handle, string functionName)
        {
            var glFunction = _gtkglExt.GetProcAddress(functionName);
            if (glFunction != IntPtr.Zero)
            {
                return glFunction;
            }

            var systemFunction = this.UnderlyingLoader.LoadFunctionPointer(handle, functionName);
            if (systemFunction != IntPtr.Zero)
            {
                return systemFunction;
            }

            throw new SymbolLoadingException();
        }
    }
}
