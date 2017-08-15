//
//  IAudioAsset.cs
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
using OpenTK.Audio.OpenAL;

namespace Everlook.Audio
{
	/// <summary>
	/// Exposes data common to audio assets.
	/// </summary>
	public interface IAudioAsset : IDisposable
	{
		/// <summary>
		/// Gets the <see cref="ALFormat"/> that the PCM data is in.
		/// </summary>
		ALFormat Format { get; }

		/// <summary>
		/// Gets the raw PCM data of the audio asset.
		/// </summary>
		byte[] PCMData { get; }

		/// <summary>
		/// Gets a stream containing the PCM data.
		/// </summary>
		Stream PCMStream { get; }

		/// <summary>
		/// Gets the number of channels that the audio has.
		/// </summary>
		int Channels { get; }

		/// <summary>
		/// Gets the number of bits per PCM sample.
		/// </summary>
		int BitsPerSample { get; }

		/// <summary>
		/// Gets the sample rate or frequency of the PCM data.
		/// </summary>
		int SampleRate { get; }
	}
}
