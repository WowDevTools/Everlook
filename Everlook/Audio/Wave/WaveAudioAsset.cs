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
using OpenTK.Audio.OpenAL;
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
        private bool IsDisposed;

        /// <inheritdoc />
        public ALFormat Format
        {
            get
            {
                switch (this.Channels)
                {
                    case 1:
                    {
                        return this.BitsPerSample == 8 ? ALFormat.Mono8 : ALFormat.Mono16;
                    }
                    case 2:
                    {
                        return this.BitsPerSample == 8 ? ALFormat.Stereo8 : ALFormat.Stereo16;
                    }
                    default:
                    {
                        throw new NotSupportedException("The specified sound format is not supported.");
                    }
                }
            }
        }

        private byte[] PCMDataInternal;

        /// <inheritdoc />
        public byte[] PCMData
        {
            get
            {
                ThrowIfDisposed();

                if (this.PCMDataInternal != null)
                {
                    return this.PCMDataInternal;
                }

                // Decode the whole stream
                using (MemoryStream pcm = new MemoryStream())
                {
                    this.PCMStream.Seek(0, SeekOrigin.Begin);

                    this.PCMStream.CopyTo(pcm);
                    this.PCMDataInternal = pcm.ToArray();
                }

                return this.PCMDataInternal;
            }
            private set => this.PCMDataInternal = value;
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
                throw new ArgumentException("The provided file reference was not a wave audio file.", nameof(fileReference));
            }

            byte[] fileBytes = fileReference.Extract();
            if (fileBytes == null)
            {
                throw new ArgumentException("The file data could not be extracted.", nameof(fileReference));
            }

            using (MemoryStream ms = new MemoryStream(fileBytes))
            {
                using (BinaryReader br = new BinaryReader(ms))
                {
                    string signature = new string(br.ReadChars(4));
                    if (signature != "RIFF")
                    {
                        throw new NotSupportedException("The file data is not a wave file.");
                    }

                    // Skip chunk size
                    br.BaseStream.Position += 4;

                    string format = new string(br.ReadChars(4));
                    if (format != "WAVE")
                    {
                        throw new NotSupportedException("The file data is not a wave file.");
                    }

                    string formatSignature = new string(br.ReadChars(4));
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

                    string dataSignature = new string(br.ReadChars(4));
                    if (dataSignature != "data")
                    {
                        throw new NotSupportedException("The file data is not a wave file.");
                    }

                    int dataChunkSize = br.ReadInt32();

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
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(ToString());
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.IsDisposed = true;

            this.PCMStream?.Dispose();

            this.PCMStream = null;
            this.PCMData = null;
        }
    }
}
