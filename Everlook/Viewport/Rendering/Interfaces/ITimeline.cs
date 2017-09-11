//
//  ITimeline.cs
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

using Warcraft.Core.Interpolation;

namespace Everlook.Viewport.Rendering.Interfaces
{
	/// <summary>
	/// Representation of a timeline with interpolated values.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public interface ITimeline<out T>
	{
		/// <summary>
		/// Gets or sets a value indicating whether the track loops. A looping track will overflow into itself instead
		/// of returning the end value when advanced beyond its end.
		/// </summary>
		bool Looping { get; set; }

		/// <summary>
		/// Gets or sets the position (in milliseconds) of the track.
		/// </summary>
		float Position { get; set; }

		/// <summary>
		/// Gets the duration (in milliseconds) of the track.
		/// </summary>
		float Duration { get; }

		/// <summary>
		/// Gets the interpolation type of the timeline.
		/// </summary>
		InterpolationType Interpolation { get; }

		/// <summary>
		/// Gets the value at the current position in the timeline.
		/// </summary>
		T Value { get; }

		/// <summary>
		/// Advances the position of the track by the specified distance. If the track is looping, and the distance
		/// would advance the track beyond its end timestamp, then it instead overflows onto itself.
		/// </summary>
		/// <param name="time">The time to advance the timeline by.</param>
		void Advance(float time);
	}
}
