//
//  WaveAudioAsset.cs
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
using Silk.NET.OpenAL;
using Warcraft.Core;

namespace Everlook.Audio.Wave
{
    /// <summary>
    /// Represents a loaded Wave audio asset.
    /// </summary>
    public sealed class WaveAudioAsset : IAudioAsset
    {
        /// <summary>
        /// Whether or not this instance has been disposed.
        /// </summary>
        private bool _isDisposed;

        /// <inheritdoc />
        public BufferFormat Format
        {
            get
            {
                switch (this.Channels)
                {
                    case 1:
                    {
                        return this.BitsPerSample == 8 ? BufferFormat.Mono8 : BufferFormat.Mono16;
                    }
                    case 2:
                    {
                        return this.BitsPerSample == 8 ? BufferFormat.Stereo8 : BufferFormat.Stereo16;
                    }
                    default:
                    {
                        throw new NotSupportedException("The specified sound format is not supported.");
                    }
                }
            }
        }

        private byte[]? _pcmDataInternal;

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
        /// Initializes a new instance of the <see cref="WaveAudioAsset"/> class.
        /// </summary>
        /// <param name="fileReference">The reference to create the asset from.</param>
        /// <exception cref="ArgumentException">Thrown if the reference is not a wave audio file.</exception>
        /// <exception cref="NotSupportedException">Thrown if the file data is not a wave audio file.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the file data can't be extracted.</exception>
        public WaveAudioAsset(FileReference fileReference)
        {
            if (fileReference == null)
            {
                throw new ArgumentNullException(nameof(fileReference));
            }

            if (fileReference.GetReferencedFileType() != WarcraftFileType.WaveAudio)
            {
                throw new ArgumentException
                (
                    "The provided file reference was not a wave audio file.",
                    nameof(fileReference)
                );
            }

            var fileBytes = fileReference.Extract();
            if (fileBytes == null)
            {
                throw new ArgumentException("The file data could not be extracted.", nameof(fileReference));
            }

            using (var ms = new MemoryStream(fileBytes))
            {
                using (var br = new BinaryReader(ms))
                {
                    var signature = new string(br.ReadChars(4));
                    if (signature != "RIFF")
                    {
                        throw new NotSupportedException("The file data is not a wave file.");
                    }

                    // Skip chunk size
                    br.BaseStream.Position += 4;

                    var format = new string(br.ReadChars(4));
                    if (format != "WAVE")
                    {
                        throw new NotSupportedException("The file data is not a wave file.");
                    }

                    var formatSignature = new string(br.ReadChars(4));
                    if (formatSignature != "fmt ")
                    {
                        throw new NotSupportedException("The file data is not a wave file.");
                    }

                    // Skip format chunk size
                    br.BaseStream.Position += 4;

                    // Skip audio format
                    br.BaseStream.Position += 2;

                    this.Channels = br.ReadInt16();
                    this.SampleRate = br.ReadInt32();

                    // Skip byte rate
                    br.BaseStream.Position += 4;

                    // Skip block alignment
                    br.BaseStream.Position += 2;

                    this.BitsPerSample = br.ReadInt16();

                    var dataSignature = new string(br.ReadChars(4));
                    if (dataSignature != "data")
                    {
                        throw new NotSupportedException("The file data is not a wave file.");
                    }

                    var dataChunkSize = br.ReadInt32();

                    this.PCMStream = new MemoryStream(br.ReadBytes(dataChunkSize));
                }
            }
        }

        /// <summary>
        /// Loads a <see cref="WaveAudioAsset"/> asynchronously.
        /// </summary>
        /// <param name="fileReference">The reference to load.</param>
        /// <returns>A WaveAudioAsset.</returns>
        public static Task<WaveAudioAsset> LoadAsync(FileReference fileReference)
        {
            return Task.Run(() => new WaveAudioAsset(fileReference));
        }

        /// <inheritdoc />
        public void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(ToString() ?? nameof(WaveAudioAsset));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _isDisposed = true;

            this.PCMStream?.Dispose();
        }
    }
}
