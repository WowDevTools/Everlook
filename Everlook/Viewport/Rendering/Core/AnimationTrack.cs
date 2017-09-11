//
//  AnimationTrack.cs
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
using Everlook.Viewport.Rendering.Interfaces;
using OpenTK;
using Warcraft.MDX.Animation;

namespace Everlook.Viewport.Rendering.Core
{
	/// <summary>
	/// Represents a set of animation timelines.
	/// </summary>
	/// <typeparam name="T">The value type contained in the track.</typeparam>
	public class AnimationTrack<T> where T : struct
	{
		/// <summary>
		/// Gets the timelines in this animation.
		/// </summary>
		public IReadOnlyCollection<ITimeline<T>> Timelines { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="AnimationTrack{T}"/> class.
		/// </summary>
		/// <param name="tracks">The tracks to wrap around.</param>
		public AnimationTrack(MDXTrack<T> tracks)
		{

		}
	}
}
