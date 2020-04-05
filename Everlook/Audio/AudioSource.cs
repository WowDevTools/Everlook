//
//  AudioSource.cs
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
using System.Numerics;
using System.Threading.Tasks;
using Everlook.Audio.MP3;
using Everlook.Audio.Wave;
using Everlook.Explorer;
using JetBrains.Annotations;
using Silk.NET.OpenAL;
using Warcraft.Core;
using Warcraft.DBC.Definitions;

namespace Everlook.Audio
{
    /// <summary>
    /// Represents a single audio source in 3D space.
    /// </summary>
    [PublicAPI]
    public sealed class AudioSource : IDisposable
    {
        private AL _al = AL.GetApi();
        private IAudioAsset? _audioAsset;
        private uint _soundBufferID;
        private uint _soundSourceID;

        /// <summary>
        /// Gets the format of the current audio source.
        /// </summary>
        public BufferFormat SoundFormat { get; private set; }

        /// <summary>
        /// Gets the sample rate of the current audio source.
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// Gets the current state of the sound source.
        /// </summary>
        public SourceState State
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, GetSourceInteger.SourceState, out var value);
                return (SourceState)value;
            }
        }

        /// <summary>
        /// Gets or sets the offset of the audio source into the sound (in seconds).
        /// </summary>
        public float TimeOffset
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceFloat.SecOffset, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceFloat.SecOffset, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this audio source should loop.
        /// </summary>
        public bool Looping
        {
            get
            {
                // TODO: Restore
                /*
                 * al.GetSourceProperty(_soundSourceID, SourceBoolean.Looping, out var temp);
                 * return temp > 0;
                 */

                return false;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceBoolean.Looping, value ? 1 : 0);
        }

        /// <summary>
        /// Gets or sets the position of the audio source in 3D space.
        /// </summary>
        public Vector3 Position
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceVector3.Position, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceVector3.Position, value);
        }

        /// <summary>
        /// Gets or sets the direction of the sound cone.
        /// </summary>
        public Vector3 Direction
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceVector3.Direction, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceVector3.Direction, value);
        }

        /// <summary>
        /// Gets or sets the velocity vector of the sound source.
        /// </summary>
        public Vector3 Velocity
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceVector3.Direction, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceVector3.Direction, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the audio position should be relative to the listener's position.
        /// </summary>
        public bool RelativePositioning
        {
            get
            {
                // TODO: Restore
                /*
                 * _al.GetSourceProperty(_soundSourceID, SourceBoolean.SourceRelative, out var value);
                 * return value > 0;
                 */

                return false;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceBoolean.SourceRelative, value ? 1 : 0);
        }

        /// <summary>
        /// Gets or sets the attenuation distance, that is, the distance beyond which the source will be silent,
        /// of the audio source. Range: [0.0f - float.PositiveInfinity] (default: float.PositiveInfinity).
        /// </summary>
        public float Attenuation
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceFloat.MaxDistance, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceFloat.MaxDistance, value);
        }

        /// <summary>
        /// Gets or sets the minimum gain for this source. Range: [0.0f - 1.0f] (default: 0.0f).
        /// </summary>
        public float MinGain
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceFloat.MinGain, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceFloat.MinGain, value);
        }

        /// <summary>
        /// Gets or sets the maximum gain for this source. Range: [0.0f - 1.0f] (default: 0.0f).
        /// </summary>
        public float MaxGain
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceFloat.MaxGain, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceFloat.MaxGain, value);
        }

        /// <summary>
        /// Gets or sets the gain outside the oriented audio cone. Range: [0.0f - 1.0f] (default: 0.0f).
        /// </summary>
        public float ConeOuterGain
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceFloat.ConeOuterGain, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceFloat.ConeOuterGain, value);
        }

        /// <summary>
        /// Gets or sets the inner angle of the oriented audio cone. Range: [0.0f - 360.0f] (default: 360.0f).
        /// </summary>
        public float ConeInnerAngle
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceFloat.ConeInnerAngle, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceFloat.ConeInnerAngle, value);
        }

        /// <summary>
        /// Gets or sets the inner angle of the oriented audio cone. Range: [0.0f - 360.0f] (default: 360.0f).
        /// </summary>
        public float ConeOuterAngle
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceFloat.ConeOuterAngle, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceFloat.ConeOuterAngle, value);
        }

        /// <summary>
        /// Gets or sets the pitch of the audio source. Range: [0.5f - 2.0f] (default 1.0f).
        /// </summary>
        public float Pitch
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceFloat.Pitch, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceFloat.Pitch, value);
        }

        /// <summary>
        /// Gets or sets the gain of the audio source. Range: [0.0f - float.PositiveInfinity]. A value of 1.0f means
        /// unchanged. The scale is logarithmic.
        /// </summary>
        public float Gain
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceFloat.Gain, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceFloat.Gain, value);
        }

        /// <summary>
        /// Gets or sets the rolloff factor. Range: [0.0f - float.PositiveInfinity].
        /// </summary>
        public float RolloffFactor
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceFloat.RolloffFactor, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceFloat.RolloffFactor, value);
        }

        /// <summary>
        /// Gets or sets the reference distance of the source, that is, the distance under which the volume for the
        /// source would normally drop by half. Range: [0.0f - float.PositiveInfinity] (default 1.0f).
        /// A value of 0.0f indicates no attenuation.
        /// </summary>
        public float ReferenceDistance
        {
            get
            {
                _al.GetSourceProperty(_soundSourceID, SourceFloat.ReferenceDistance, out var value);
                return value;
            }
            set => _al.SetSourceProperty(_soundSourceID, SourceFloat.ReferenceDistance, value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioSource"/> class.
        /// </summary>
        private AudioSource()
        {
        }

        /// <summary>
        /// Creates a new <see cref="AudioSource"/> and registers it with the <see cref="AudioManager"/>.
        /// </summary>
        /// <returns>A new AudioSource.</returns>
        public static AudioSource CreateNew()
        {
            var source = new AudioSource();
            AudioManager.RegisterSource(source);

            return source;
        }

        /// <summary>
        /// Creates a new <see cref="AudioSource"/> from a given <see cref="SoundEntriesRecord"/>.
        /// </summary>
        /// <param name="soundEntry">The sound database entry.</param>
        /// <returns>An AudioSource.</returns>
        public static AudioSource CreateFromSoundEntry(SoundEntriesRecord soundEntry)
        {
            var source = CreateNew();
            source.Attenuation = soundEntry.DistanceCutoff;

            // TODO: More settings
            AudioManager.RegisterSource(source);

            return source;
        }

        /// <summary>
        /// Plays the source from the start.
        /// </summary>
        public void Play()
        {
            _al.SourceRewind(_soundSourceID);
            _al.SourcePlay(_soundSourceID);
        }

        /// <summary>
        /// Resumes playback of the source.
        /// </summary>
        public void Resume()
        {
            _al.SourcePlay(_soundSourceID);
        }

        /// <summary>
        /// Pauses the source.
        /// </summary>
        public void Pause()
        {
            _al.SourcePause(_soundSourceID);
        }

        /// <summary>
        /// Stops the source and resets the position of the audio.
        /// </summary>
        public void Stop()
        {
            _al.SourceStop(_soundSourceID);
            _al.SourceRewind(_soundSourceID);
        }

        /// <summary>
        /// Sets the audio data of the source, clearing the previous one.
        /// </summary>
        /// <param name="fileReference">A file reference pointing to a sound file.</param>
        /// <returns>An asynchronous task.</returns>
        public async Task SetAudioAsync(FileReference fileReference)
        {
            // First, clear the old audio
            ClearAudio();

            // Asynchronously load audio data
            switch (fileReference.GetReferencedFileType())
            {
                case WarcraftFileType.WaveAudio:
                {
                    _audioAsset = await WaveAudioAsset.LoadAsync(fileReference);
                    break;
                }
                case WarcraftFileType.MP3Audio:
                {
                    _audioAsset = await MP3AudioAsset.LoadAsync(fileReference);
                    break;
                }
                default:
                {
                    return;
                }
            }

            var soundData = _audioAsset.PCMData;
            this.SoundFormat = _audioAsset.Format;
            this.SampleRate = _audioAsset.SampleRate;

            // Create new AL data
            _soundBufferID = _al.GenBuffer();
            _soundSourceID = _al.GenSource();

            unsafe
            {
                fixed (void* ptr = soundData)
                {
                    _al.BufferData(_soundBufferID, this.SoundFormat, ptr, soundData.Length, this.SampleRate);
                }
            }

            // TODO: Use correct overload
            _al.SetSourceProperty(_soundSourceID, SourceInteger.Buffer, (int)_soundBufferID);
        }

        /// <summary>
        /// Clears the audio from the player.
        /// </summary>
        private void ClearAudio()
        {
            _al.SourceStop(_soundSourceID);

            _al.DeleteSource(_soundSourceID);
            _al.DeleteBuffer(_soundBufferID);

            _audioAsset?.Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Stop();
            ClearAudio();
        }
    }
}
