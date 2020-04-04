//
//  DynamicInterpolator.cs
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
using OpenTK;
using Warcraft.Core.Interpolation;
using Warcraft.Core.Structures;

#pragma warning disable SA1026
#pragma warning disable SA1011
#pragma warning disable SA1012

namespace Everlook.Viewport.Rendering.Core
{
    /// <summary>
    /// Handles interpolation of dynamic values.
    /// </summary>
    public class DynamicInterpolator
    {
        private static readonly Dictionary<Type, Func<dynamic, float[]>> TypeFlatteners = new Dictionary<Type, Func<dynamic, float[]>>
        {
            // Single-element numeric types
            { typeof(byte), v => new []{ (float)v } },
            { typeof(sbyte), v => new []{ (float)v } },
            { typeof(short), v => new []{ (float)v } },
            { typeof(ushort), v => new []{ (float)v } },
            { typeof(int), v => new []{ (float)v } },
            { typeof(uint), v => new []{ (float)v } },
            { typeof(long), v => new []{ (float)v } },
            { typeof(ulong), v => new []{ (float)v } },
            { typeof(float), v => new []{ (float)v } },
            { typeof(double), v => new []{ (float)v } },
            { typeof(decimal), v => new []{ (float)v } },

            // Two-component vector types
            { typeof(Vector2), v => new []{ (float)v.X, (float)v.Y } },
            { typeof(System.Numerics.Vector2), v => new []{ (float)v.X, (float)v.Y } },

            // Three-component vector types
            { typeof(Vector3), v => new []{ (float)v.X, (float)v.Y, (float)v.Z } },
            { typeof(System.Numerics.Vector3), v => new []{ (float)v.X, (float)v.Y, (float)v.Z } },
            { typeof(RGB), v => new []{ (float)v.R, (float)v.G, (float)v.B } },

            // Four-component vector types
            { typeof(Quaternion), v => new []{ (float)v.X, (float)v.Y, (float)v.Z, (float)v.W } },
            { typeof(System.Numerics.Quaternion), v => new []{ (float)v.X, (float)v.Y, (float)v.Z, (float)v.W } },
        };

        private static readonly Dictionary<Type, Func<float[], dynamic>> TypeCoalescers = new Dictionary<Type, Func<float[], dynamic>>
        {
            // Single-element numeric types
            { typeof(byte), a => (byte)a.First() },
            { typeof(sbyte), a => (sbyte)a.First() },
            { typeof(short), a => (short)a.First() },
            { typeof(ushort), a => (ushort)a.First() },
            { typeof(int), a => (int)a.First() },
            { typeof(uint), a => (uint)a.First() },
            { typeof(long), a => (long)a.First() },
            { typeof(ulong), a => (ulong)a.First() },
            { typeof(float), a => a.First() },
            { typeof(double), a => (double)a.First() },
            { typeof(decimal), a => (decimal)a.First() },

            // Two-component vector types
            { typeof(Vector2), v => new Vector2(v[0], v[1]) },
            { typeof(System.Numerics.Vector2), v => new System.Numerics.Vector2(v[0], v[1]) },

            // Three-component vector types
            { typeof(Vector3), v => new Vector3(v[0], v[1], v[2]) },
            { typeof(System.Numerics.Vector3), v => new System.Numerics.Vector3(v[0], v[1], v[2]) },
            { typeof(RGB), v => new RGB(v[0], v[1], v[2]) },

            // Four-component vector types
            { typeof(Quaternion), v => new Quaternion(v[0], v[1], v[2], v[3]) },
            { typeof(System.Numerics.Quaternion), v => new System.Numerics.Quaternion(v[0], v[1], v[2], v[3]) },
        };

        /// <summary>
        /// Interpolates between two values using linear interpolation.
        /// </summary>
        /// <param name="leaving">The value we're leaving behind.</param>
        /// <param name="approaching">The value we're approaching.</param>
        /// <param name="alpha">The alpha between the values.</param>
        /// <typeparam name="T">A type registered with the <see cref="DynamicInterpolator"/>.</typeparam>
        /// <returns>An interpolated value.</returns>
        public static T InterpolateLinear<T>(T leaving, T approaching, float alpha)
        {
            // Early bail outs
            if (alpha == 0)
            {
                return leaving;
            }

            if (alpha == 1)
            {
                return approaching;
            }

            var leavingValues = TypeFlatteners[typeof(T)](leaving);
            var approachingValues = TypeFlatteners[typeof(T)](approaching);

            var interpolatedValues = new List<float>();
            for (var i = 0; i < leavingValues.Length; ++i)
            {
                var leavingValue = leavingValues[i];
                var approachingValue = approachingValues[i];
                interpolatedValues.Add(Interpolation.InterpolateLinear(leavingValue, approachingValue, alpha));
            }

            return TypeCoalescers[typeof(T)](interpolatedValues.ToArray());
        }

        /// <summary>
        /// Interpolates between two spline key values using Hermite interpolation.
        /// </summary>
        /// <param name="leaving">The value we're leaving behind.</param>
        /// <param name="approaching">The value we're approaching.</param>
        /// <param name="alpha">The alpha between the values.</param>
        /// <typeparam name="T">A type registered with the <see cref="DynamicInterpolator"/>.</typeparam>
        /// <returns>An interpolated value.</returns>
        public static T InterpolateHermite<T>(SplineKey<T> leaving, SplineKey<T> approaching, float alpha)
        {
            // Early bail outs
            if (alpha == 0)
            {
                return leaving.Value;
            }

            if (alpha == 1)
            {
                return approaching.Value;
            }

            var leavingValues = TypeFlatteners[typeof(T)](leaving.Value);
            var approachingValues = TypeFlatteners[typeof(T)](approaching.Value);

            var leavingTangents = TypeFlatteners[typeof(T)](leaving.OutTangent);
            var approachingTangents = TypeFlatteners[typeof(T)](approaching.InTangent);

            var interpolatedValues = new List<float>();
            for (var i = 0; i < leavingValues.Length; ++i)
            {
                var leavingValue = leavingValues[i];
                var approachingValue = approachingValues[i];

                var leavingTangent = leavingTangents[i];
                var approachingTangent = approachingTangents[i];

                interpolatedValues.Add(Interpolation.InterpolateHermite(leavingValue, leavingTangent, approachingValue, approachingTangent, alpha));
            }

            return TypeCoalescers[typeof(T)](interpolatedValues.ToArray());
        }
    }
}
