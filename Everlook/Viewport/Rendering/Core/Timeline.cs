//
//  Timeline.cs
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
using System.Linq;
using Everlook.Viewport.Rendering.Interfaces;
using OpenTK;
using Warcraft.Core.Interpolation;

namespace Everlook.Viewport.Rendering.Core
{
    /// <summary>
    /// Represents a timeline track with values.
    /// </summary>
    /// <typeparam name="T">
    /// A type supporting numerical operators. If this type is not a numeric type, runtime exceptions will be thrown.
    /// You have been warned.
    /// </typeparam>
    public class Timeline<T> : ITimeline<T>
    {
        /// <inheritdoc />
        public bool Looping { get; set; }

        /// <inheritdoc />
        public float Position { get; set; }

        /// <inheritdoc />
        public float Duration { get; protected set; }

        /// <inheritdoc />
        public InterpolationType Interpolation { get; }

        /// <inheritdoc />
        public virtual T Value
        {
            get
            {
                var neighbourValues = GetNeighbourValues(this.Position);
                switch (this.Interpolation)
                {
                    case InterpolationType.None: return neighbourValues.Leaving;
                    case InterpolationType.Linear:
                    {
                        return DynamicInterpolator.InterpolateLinear
                        (
                            neighbourValues.Leaving,
                            neighbourValues.Approaching,
                            neighbourValues.Alpha
                        );
                    }
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Gets the timestamps in the timeline.
        /// </summary>
        protected IReadOnlyList<uint> Timestamps { get; }

        /// <summary>
        /// Gets the values in the timeline.
        /// </summary>
        protected IReadOnlyList<T> Values { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timeline{T}"/> class. This constructor overrides the duration
        /// of the timeline with the given value.
        /// </summary>
        /// <param name="interpolation">The interpolation type of the timeline.</param>
        /// <param name="timestamps">The timestamps in the timeline.</param>
        /// <param name="values">The values at the timestamps in the timeline.</param>
        /// <param name="duration">The duration of the timeline.</param>
        public Timeline
        (
            InterpolationType interpolation,
            IReadOnlyCollection<uint> timestamps,
            IReadOnlyCollection<T> values,
            float duration
        )
            : this(interpolation, timestamps, values)
        {
            this.Duration = duration;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timeline{T}"/> class.
        /// </summary>
        /// <param name="interpolation">The interpolation type of the timeline.</param>
        /// <param name="timestamps">The timestamps in the timeline.</param>
        /// <param name="values">The values at the timestamps in the timeline.</param>
        public Timeline
        (
            InterpolationType interpolation,
            IReadOnlyCollection<uint> timestamps,
            IReadOnlyCollection<T> values
        )
        {
            this.Interpolation = interpolation;
            this.Timestamps = timestamps.ToList();
            this.Values = values.ToList();

            this.Duration = timestamps.Last();
        }

        /// <inheritdoc />
        public void Advance(float time)
        {
            if (this.Position + time > this.Duration)
            {
                if (this.Looping)
                {
                    var overflow = time - (this.Duration - this.Position);

                    this.Position = overflow;
                    return;
                }

                this.Position = this.Duration;
                return;
            }

            this.Position += time;
        }

        /// <summary>
        /// Gets the neighbouring values to the given time index, that is, the two values which the time is leaving and
        /// approaching, respectively.
        /// </summary>
        /// <param name="time">The time index.</param>
        /// <returns>A value tuple with the leaving and approaching values.</returns>
        protected (T Leaving, T Approaching, float Alpha) GetNeighbourValues(float time)
        {
            var leaving = this.Values.First();
            var approaching = this.Values.First();

            var normalizedTime = NormalizeTime(time);

            var allTimestamps = new List<uint>(this.Timestamps);
            if (this.Duration > this.Timestamps.Last())
            {
                allTimestamps.Add((uint)this.Duration);
            }

            uint leavingTimestamp = 0;
            uint approachingTimestamp = 0;
            for (var i = 0; i < allTimestamps.Count; ++i)
            {
                if (allTimestamps[i] < normalizedTime)
                {
                    leaving = approaching;
                }
                else
                {
                    if (allTimestamps[i] == normalizedTime)
                    {
                        approaching = this.Values[i];
                    }

                    leavingTimestamp = allTimestamps[i - 1];
                    approachingTimestamp = allTimestamps[i];

                    break;
                }

                var nextIndex = i + 1;
                if (nextIndex == this.Timestamps.Count)
                {
                    approaching = this.Values.First();
                }
                else
                {
                    approaching = this.Values[nextIndex];
                }
            }

            // Calculate alpha value
            float normalizationFactor = Math.Abs(leavingTimestamp - approachingTimestamp);
            var alpha = normalizedTime / normalizationFactor;

            return (leaving, approaching, alpha);
        }

        /// <summary>
        /// Normalizes a time value (in milliseconds) to fit inside the timeline. If the timeline is looping, the value
        /// is over- or underflowed appropriately. If not, it is clamped between 0 and the duration of the timeline.
        /// </summary>
        /// <param name="time">The time value.</param>
        /// <returns>A normalized time value.</returns>
        protected float NormalizeTime(float time)
        {
            if (this.Looping)
            {
                return time % this.Duration;
            }

            return MathHelper.Clamp(time, 0, this.Duration);
        }
    }
}
