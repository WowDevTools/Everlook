//
//  GLSLPreprocessor.cs
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

using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Everlook.Viewport.Rendering.Shaders.GLSLExtended
{
	/// <summary>
	/// Holds a set of GLSL preprocessor extensions, such as #include statements. A better solution should be
	/// written as a separate library.
	/// </summary>
	public static class GLSLPreprocessor
	{
		/// <summary>
		/// Processes include statements in the provided source code, replacing them with their file contents.
		/// </summary>
		/// <param name="source">The unmodified GLSL source.</param>
		/// <param name="baseResourceDirectory">The base resource directory to search.</param>
		/// <returns>GLSL source with #include statements replaced by the pointed-to source code.</returns>
		public static string ProcessIncludes(string source, string baseResourceDirectory)
		{
			// Find a list of includes
			var includeRegex = new Regex("#include\\s+?\"(?<includeFile>.+)\"", RegexOptions.Multiline);
			var matches = includeRegex.Matches(source).Cast<Match>().Reverse();

			// Remove any trailing dot separators from the resource directory.
			baseResourceDirectory = baseResourceDirectory.TrimEnd('.');

			StringBuilder sourceBuilder = new StringBuilder(source);

			// Resolve their files in reverse order
			foreach (var match in matches)
			{
				var resourceName = match.Groups["includeFile"].Value.Replace('\\', '.').Replace('/', '.');

				// Try loading it from the resource manifest
				var fileContents = Utility.ResourceManager.LoadStringResource($"{baseResourceDirectory}.{resourceName}");
				if (fileContents == null)
				{
					throw new FileNotFoundException($"No embedded resource found at the given resource path: {resourceName}", resourceName);
				}

				if (includeRegex.IsMatch(fileContents))
				{
					fileContents = ProcessIncludes(fileContents, baseResourceDirectory);
				}

				// Insert the file contents at the include locations
				sourceBuilder.Replace(match.Value, fileContents);
			}

			return sourceBuilder.ToString();
		}

		/// <summary>
		/// Processes GLSL source code, finding all function declarations and creating forward declarations for all of
		/// them.
		/// </summary>
		/// <param name="source">The unmodifed GLSL source.</param>
		/// <returns>GLSL source code with all functions forward declared.</returns>
		public static string ProcessForwardDeclarations(string source)
		{
			// Find a list of functions

			// Create their forward declaration statements

			// Append the forward declarations at the top of the file
			return null;
		}
	}
}
