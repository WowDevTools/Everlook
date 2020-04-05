//
//  AmbientLighting.cs
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

using System.Numerics;
using Everlook.Viewport.Rendering.Core;
using Silk.NET.OpenGL;

namespace Everlook.Viewport.Rendering.Shaders.Components
{
    /// <summary>
    /// An ambient lighting shader component.
    /// </summary>
    public class AmbientLighting : GraphicsObject
    {
        private const string AmbientColourIdentifier = "AmbientColour";
        private const string AmbientIntensityIdentifier = "AmbientIntensity";

        private readonly uint _parentShaderNativeID;

        /// <summary>
        /// Initializes a new instance of the <see cref="AmbientLighting"/> class, and attaches it to the given parent
        /// shader.
        /// </summary>
        /// <param name="gl">The OpenGL API.</param>
        /// <param name="parentShaderID">The native ID of the parent shader.</param>
        public AmbientLighting(GL gl, uint parentShaderID)
            : base(gl)
        {
            _parentShaderNativeID = parentShaderID;
        }

        /// <summary>
        /// Sets the colour of the ambient light shader.
        /// </summary>
        /// <param name="lightColour">The colour of the light.</param>
        public void SetAmbientColour(Vector4 lightColour)
        {
            var colourLoc = this.GL.GetUniformLocation(_parentShaderNativeID, AmbientColourIdentifier);
            this.GL.Uniform4(colourLoc, lightColour);
        }

        /// <summary>
        /// Sets the intensity, in lux, of the ambient light shader.
        /// </summary>
        /// <param name="lightIntensity">The intensity, in lux.</param>
        public void SetAmbientIntensity(float lightIntensity)
        {
            var intensityLoc = this.GL.GetUniformLocation(_parentShaderNativeID, AmbientIntensityIdentifier);
            this.GL.Uniform1(intensityLoc, lightIntensity);
        }
    }
}
