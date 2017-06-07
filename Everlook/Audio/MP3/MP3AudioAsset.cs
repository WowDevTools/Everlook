//
//  MP3AudioAsset.cs
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

using System;
using System.IO;
using System.Threading.Tasks;
using Everlook.Explorer;
using MP3Sharp;
using OpenTK.Audio.OpenAL;
using Warcraft.Core;

namespace Everlook.Audio.MP3
{
	/// <summary>
	/// Represents a loaded MP3 audio asset.
	/// </summary>
	public class MP3AudioAsset : IAudioAsset
	{
		public ALFormat Format
		{
			get
			{
				switch (this.Channels)
				{
					case 1:
					{
						return ALFormat.Mono16;
					}
					case 2:
					{
						return ALFormat.Stereo16;
					}
					default:
					{
						throw new NotSupportedException("The specified sound format is not supported.");
					}
				}
			}
		}

		public byte[] PCMData { get; }
		public int Channels { get; }
		public int BitsPerSample { get; }
		public int SampleRate { get; }

		/// <summary>
		/// Initializes a new <see cref="MP3AudioAsset"/> from a file reference.
		/// </summary>
		/// <param name="fileReference"></param>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		public MP3AudioAsset(FileReference fileReference)
		{
			if (fileReference.GetReferencedFileType() != WarcraftFileType.MP3Audio)
			{
				throw new ArgumentException("The provided file reference was not an MP3 audio file.", nameof(fileReference));
			}

			byte[] fileBytes = fileReference.Extract();
			if (fileBytes == null)
			{
				throw new ArgumentNullException(nameof(fileReference), "The file data could not be extracted.");
			}

			using (MemoryStream ms = new MemoryStream(fileBytes))
			using (MP3Stream mp3 = new MP3Stream(ms))
			{
				this.Channels = mp3.ChannelCount;
				this.BitsPerSample = 16;
				this.SampleRate = mp3.Frequency;

				// Decode the whole stream
				using (MemoryStream pcm = new MemoryStream())
				{
					mp3.CopyTo(pcm);
					this.PCMData = pcm.ToArray();
				}
			}
		}

		/// <summary>
		/// Loads a <see cref="MP3AudioAsset"/> asynchronously.
		/// </summary>
		/// <param name="fileReference"></param>
		/// <returns></returns>
		public static async Task<MP3AudioAsset> LoadAsync(FileReference fileReference)
		{
			return await Task.Run(() => new MP3AudioAsset(fileReference));
		}
	}
}