//
//  ResourceManager.cs
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

using System.IO;
using System.Reflection;

namespace Everlook.Utility
{
	public static class ResourceManager
	{
		/// <summary>
		/// Loads the source code of a stored shader from the specified resource path.
		/// </summary>
		/// <param name="resourcePath">The resource path of the shader.</param>
		/// <returns>The source code of a shader.</returns>
		public static string LoadStringResource(string resourcePath)
		{
			string resourceString;
			using (Stream resourceStream =
				Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath))
			{
				if (resourceStream == null)
				{
					return null;
				}

				using (StreamReader sr = new StreamReader(resourceStream))
				{
					resourceString = sr.ReadToEnd();
				}
			}

			return resourceString;
		}
	}
}