//
//  SplineTimeline.cs
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
using System.Collections.Generic;
using Everlook.Viewport.Rendering.Interfaces;
using Warcraft.Core.Interpolation;
using Warcraft.Core.Structures;

namespace Everlook.Viewport.Rendering.Core
{
    /// <summary>
    /// Represents a timeline track with spline key values.
    /// </summary>
    /// <typeparam name="T">
    /// A type supporting numerical operators. If this type is not a numeric type, runtime exceptions will be thrown.
    /// You have been warned.
    /// </typeparam>
    public sealed class SplineTimeline<T> : Timeline<SplineKey<T>>, ITimeline<T> where T : struct
    {
        /// <inheritdoc />
        public new T Value
        {
            get
            {
                var neighbourValues = GetNeighbourValues(this.Position);
                switch (this.Interpolation)
                {
                    case InterpolationType.None: return neighbourValues.Leaving.Value;
                    case InterpolationType.Hermite:
                    case InterpolationType.Bezier:
                    {
                        return DynamicInterpolator.InterpolateHermite
                        (
                            neighbourValues.Leaving,
                            neighbourValues.Approaching,
                            neighbourValues.Alpha
                        );
                    }
                    case InterpolationType.Linear:
                    {
                        return DynamicInterpolator.InterpolateLinear
                        (
                            neighbourValues.Leaving.Value,
                            neighbourValues.Approaching.Value,
                            neighbourValues.Alpha
                        );
                    }
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SplineTimeline{T}"/> class. This constructor overrides the duration
        /// of the timeline with the given value.
        /// </summary>
        /// <param name="interpolation">The interpolation type of the timeline.</param>
        /// <param name="timestamps">The timestamps in the timeline.</param>
        /// <param name="values">The values at the timestamps in the timeline.</param>
        /// <param name="duration">The duration of the timeline.</param>
        public SplineTimeline(InterpolationType interpolation, IReadOnlyCollection<uint> timestamps, IReadOnlyCollection<SplineKey<T>> values, float duration)
            : base(interpolation, timestamps, values, duration)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SplineTimeline{T}"/> class.
        /// </summary>
        /// <param name="interpolation">The interpolation type of the timeline.</param>
        /// <param name="timestamps">The timestamps in the timeline.</param>
        /// <param name="values">The values at the timestamps in the timeline.</param>
        public SplineTimeline(InterpolationType interpolation, IReadOnlyCollection<uint> timestamps, IReadOnlyCollection<SplineKey<T>> values)
            : base(interpolation, timestamps, values)
        {
        }
    }
}
