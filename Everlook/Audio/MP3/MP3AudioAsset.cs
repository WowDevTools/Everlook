//
//  MP3AudioAsset.cs
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
    public sealed class MP3AudioAsset : IAudioAsset
    {
        /// <summary>
        /// Whether or not this instance has been disposed.
        /// </summary>
        private bool _isDisposed;

        /// <inheritdoc />
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

        private byte[] _pcmDataInternal;

        /// <inheritdoc />
        public byte[] PCMData
        {
            get
            {
                ThrowIfDisposed();

                if (_pcmDataInternal != null)
                {
                    return _pcmDataInternal;
                }

                // Decode the whole stream
                using (var pcm = new MemoryStream())
                {
                    // Quick note: this is inside the actual MP3 stream, not the PCM stream
                    this.PCMStream.Seek(0, SeekOrigin.Begin);

                    this.PCMStream.CopyTo(pcm);
                    _pcmDataInternal = pcm.ToArray();
                }

                return _pcmDataInternal;
            }
            private set => _pcmDataInternal = value;
        }

        /// <inheritdoc />
        public Stream PCMStream { get; private set; }

        /// <inheritdoc />
        public int Channels { get; }

        /// <inheritdoc />
        public int BitsPerSample { get; }

        /// <inheritdoc />
        public int SampleRate { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MP3AudioAsset"/> class.
        /// </summary>
        /// <param name="fileReference">The reference to create the asset from.</param>
        /// <exception cref="ArgumentException">Thrown if the reference is not an MP3 audio file.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the file data can't be extracted.</exception>
        public MP3AudioAsset(FileReference fileReference)
        {
            if (fileReference == null)
            {
                throw new ArgumentNullException(nameof(fileReference));
            }

            if (fileReference.GetReferencedFileType() != WarcraftFileType.MP3Audio)
            {
                throw new ArgumentException("The provided file reference was not an MP3 audio file.", nameof(fileReference));
            }

            var fileBytes = fileReference.Extract();
            if (fileBytes == null)
            {
                throw new ArgumentException("The file data could not be extracted.", nameof(fileReference));
            }

            this.PCMStream = new MP3Stream(new MemoryStream(fileBytes));
            this.Channels = ((MP3Stream)this.PCMStream).ChannelCount;
            this.BitsPerSample = 16;
            this.SampleRate = ((MP3Stream)this.PCMStream).Frequency;
        }

        /// <summary>
        /// Loads a <see cref="MP3AudioAsset"/> asynchronously.
        /// </summary>
        /// <param name="fileReference">The reference to load.</param>
        /// <returns>An MP3AudioAsset.</returns>
        public static Task<MP3AudioAsset> LoadAsync(FileReference fileReference)
        {
            return Task.Run(() => new MP3AudioAsset(fileReference));
        }

        /// <inheritdoc />
        public void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(ToString());
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _isDisposed = true;

            this.PCMStream?.Dispose();

            this.PCMStream = null;
            this.PCMData = null;
        }
    }
}
