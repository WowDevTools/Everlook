//
//  RenderableWMO.cs
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
using Warcraft.WMO;

namespace Everlook.Renderables
{
	/// <summary>
	/// Represents a renderable World Model Object
	/// </summary>
	public class RenderableWMO : IRenderable
	{
		/// <summary>
		/// Gets a value indicating whether this instance uses static rendering; that is, 
		/// a single frame is rendered and then reused. Useful as an optimization for images.
		/// </summary>
		/// <value>true</value>
		/// <c>false</c>
		public bool IsStatic
		{
			get
			{
				return true;
			}
		}

		/// <summary>
		/// The model contained by this renderable world object.
		/// </summary>
		/// <value>The model.</value>
		public WMO Model
		{
			get;
			private set;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.Renderables.RenderableWMO"/> class.
		/// </summary>
		public RenderableWMO(WMO InModel)
		{
			this.Model = InModel;
		}

		/// <summary>
		/// Releases all resource used by the <see cref="Everlook.Renderables.RenderableWMO"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Everlook.Renderables.RenderableWMO"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="Everlook.Renderables.RenderableWMO"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="Everlook.Renderables.RenderableWMO"/> so the garbage collector can reclaim the memory that the
		/// <see cref="Everlook.Renderables.RenderableWMO"/> was occupying.</remarks>
		public void Dispose()
		{
			Model.Dispose();
			Model = null;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="Everlook.Renderables.RenderableWMO"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (IsStatic.GetHashCode() + Model.GetHashCode()).GetHashCode();
		}
	}
}

