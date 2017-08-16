//
//  IRenderable.cs
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
using Everlook.Viewport.Camera;
using OpenTK;

namespace Everlook.Viewport.Rendering.Interfaces
{
	/// <summary>
	/// Interface representing a renderable object that can be passed to the viewport renderer.
	/// Specific implementations of the rendering is implemented in the viewport renderer.
	/// </summary>
	public interface IRenderable : IDisposable
	{
		/// <summary>
		/// Gets a value indicating whether this instance uses static rendering; that is,
		/// a single frame is rendered and then reused. Useful as an optimization for images.
		/// </summary>
		/// <value><c>true</c> if this instance is static; otherwise, <c>false</c>.</value>
		bool IsStatic
		{
			get;
		}

		/// <summary>
		/// Gets or sets a value indicating whether the current renderable has been initialized.
		/// </summary>
		bool IsInitialized
		{
			get;
			set;
		}

		/// <summary>
		/// Gets the projection method to use for this renderable object. Typically, this is Orthographic
		/// or Perspective.
		/// </summary>
		ProjectionType Projection
		{
			get;
		}

		/// <summary>
		/// Initializes the required data for rendering.
		/// </summary>
		void Initialize();

		/// <summary>
		/// Renders the current object in the current OpenGL context.
		/// </summary>
		/// <param name="viewMatrix">The view matrix.</param>
		/// <param name="projectionMatrix">The projection matrix.</param>
		/// <param name="camera">The user camera.</param>
		void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera);
	}
}
