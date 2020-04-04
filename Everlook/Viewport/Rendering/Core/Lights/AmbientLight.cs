//
//  AmbientLight.cs
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

using OpenTK.Graphics;

namespace Everlook.Viewport.Rendering.Core.Lights
{
    /// <summary>
    /// Represents an ambient light source.
    /// </summary>
    public class AmbientLight
    {
        /// <summary>
        /// Gets or sets the colour of the light.
        /// </summary>
        public Color4 LightColour { get; set; }

        /// <summary>
        /// Gets or sets the intensity, in lux, of the light.
        /// </summary>
        public float Intensity { get; set; }
    }
}
