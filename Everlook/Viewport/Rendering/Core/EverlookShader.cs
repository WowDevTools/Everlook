//
//  RenderCache.cs
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

namespace Everlook.Viewport.Rendering.Core
{
	/// <summary>
	/// A set of shaders available to Everlook at runtime.
	/// </summary>
	public enum EverlookShader
	{
		/// <summary>
		/// A plain shader used for 2D images. This shader simply takes vertices, a single texture sampler
		/// and UV coordinates for these vertices.
		/// </summary>
		Plain2D,

		/// <summary>
		/// A shader capable of rendering a group inside a World Model. This shader does not support
		/// any animation.
		/// </summary>
		UnlitWorldModelOpaque,

		/// <summary>
		/// A shader capable of rendering a group inside a world model, and discarding fragments based on the alpha
		/// value.
		/// </summary>
		UnlitWorldModelAlphaKey,

		/// <summary>
		/// A shader capable of rendering a simple game model. This shader does not support any animation.
		/// </summary>
		UnlitGameModel,

		/// <summary>
		/// A shader capable of rendering an animated model.
		/// </summary>
		Model,

		/// <summary>
		/// A shader capable of rendering a particle system.
		/// </summary>
		ParticleSystem,

		/// <summary>
		/// A shader capable of rendering a bounding box
		/// </summary>
		BoundingBox
	}
}