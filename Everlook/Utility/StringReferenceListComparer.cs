//
//  StringReferenceListComparer.cs
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
using Warcraft.DBC.SpecialFields;

namespace Everlook.Utility
{
    /// <summary>
    /// Utility class for determining equality between lists of string references.
    /// </summary>
    public class StringReferenceListComparer : IEqualityComparer<IReadOnlyList<StringReference>>
    {
        /// <inheritdoc />
        public bool Equals(IReadOnlyList<StringReference> x, IReadOnlyList<StringReference> y)
        {
            if (x == null && y == null)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            if (x.Count != y.Count)
            {
                return false;
            }

            for (var i = 0; i < x.Count; i++)
            {
                var leftReference = x[i];
                var rightReference = y[i];

                var areValuesEqual = string.Equals
                (
                    leftReference.Value,
                    rightReference.Value,
                    StringComparison.OrdinalIgnoreCase
                );

                var areOffsetsEqual = leftReference.Offset == rightReference.Offset;
                if (!(areValuesEqual && areOffsetsEqual))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public int GetHashCode(IReadOnlyList<StringReference> obj)
        {
            unchecked
            {
                var hash = 17;

                foreach (var reference in obj)
                {
                    hash *= 23 + reference.Value.GetHashCode();
                    hash *= 23 + reference.Offset.GetHashCode();
                }

                return hash;
            }
        }
    }
}
