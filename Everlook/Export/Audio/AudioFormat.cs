//
//  AudioTypes.cs
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

namespace Everlook.Export.Audio
{
	/// <summary>
	/// Supported audio formats for the audio exporter.
	/// </summary>
	public enum AudioFormat
	{
		/// <summary>
		/// Waveform Audio File Format
		/// <a href="https://en.wikipedia.org/wiki/WAV"/>
		/// </summary>
		WAV = 0,

		/// <summary>
		/// MPEG-2 Audio Layer III
		/// <a href="https://en.wikipedia.org/wiki/MP3"/>
		/// </summary>
		MP3 = 1,

		/// <summary>
		/// Xiph OGG Audio Format
		/// <a href="https://en.wikipedia.org/wiki/Ogg"/>
		/// </summary>
		OGG = 2,

		/// <summary>
		/// Free Lossless Audio Codec
		/// <a href="https://en.wikipedia.org/wiki/FLAC"/>
		/// </summary>
		FLAC = 3
	}
}

