
//
//  ImageTypes.cs
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

namespace Everlook.Export.Image
{
	/// <summary>
	/// Supported image formats for the image exporter.
	/// </summary>
	public enum ImageFormat
	{
		/// <summary>
		/// Portable Network Graphics (ISO 15948)
		/// <a href="https://en.wikipedia.org/wiki/Portable_Network_Graphics"/>
		/// </summary>
		PNG = 0,

		/// <summary>
		/// Joint Photographics Export Format (ISO 10918)
		/// <a href="https://en.wikipedia.org/wiki/JPEG"/>
		/// </summary>
		JPG = 1,

		/// <summary>
		/// Tagged Image File Format
		/// <a href="https://en.wikipedia.org/wiki/Tagged_Image_File_Format"/>
		/// </summary>
		TIF = 2,

		/// <summary>
		/// Bitmap Image
		/// <a href="https://en.wikipedia.org/wiki/BMP_file_format"/>
		/// </summary>
		BMP = 3
	}
}

