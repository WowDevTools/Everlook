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
using System.Threading.Tasks;
using Everlook.Audio.MP3;
using Everlook.Audio.Wave;
using Everlook.Explorer;
using OpenTK;
using OpenTK.Audio.OpenAL;
using Warcraft.Core;
using Warcraft.DBC.Definitions;

namespace Everlook.Audio
{
    /// <summary>
    /// Represents a single audio source in 3D space.
    /// </summary>
    public sealed class AudioSource : IDisposable, IEquatable<AudioSource>
    {
        private IAudioAsset? _audioAsset;
        private int _soundBufferID;
        private int _soundSourceID;

        /// <summary>
        /// Gets the format of the current audio source.
        /// </summary>
        public ALFormat SoundFormat { get; private set; }

        /// <summary>
        /// Gets the sample rate of the current audio source.
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// Gets the current state of the sound source.
        /// </summary>
        public ALSourceState State => AL.GetSourceState(_soundSourceID);

        /// <summary>
        /// Gets or sets the offset of the audio source into the sound (in seconds).
        /// </summary>
        public float TimeOffset
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSourcef.SecOffset, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourcef.SecOffset, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this audio source should loop.
        /// </summary>
        public bool Looping
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSourceb.Looping, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourceb.Looping, value);
        }

        /// <summary>
        /// Gets or sets the position of the audio source in 3D space.
        /// </summary>
        public Vector3 Position
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSource3f.Position, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSource3f.Position, ref value);
        }

        /// <summary>
        /// Gets or sets the direction of the sound cone.
        /// </summary>
        public Vector3 Direction
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSource3f.Direction, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSource3f.Direction, ref value);
        }

        /// <summary>
        /// Gets or sets the velocity vector of the sound source.
        /// </summary>
        public Vector3 Velocity
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSource3f.Direction, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSource3f.Direction, ref value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the audio position should be relative to the listener's position.
        /// </summary>
        public bool RelativePositioning
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSourceb.SourceRelative, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourceb.SourceRelative, value);
        }

        /// <summary>
        /// Gets or sets the attenuation distance, that is, the distance beyond which the source will be silent,
        /// of the audio source. Range: [0.0f - float.PositiveInfinity] (default: float.PositiveInfinity).
        /// </summary>
        public float Attenuation
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSourcef.MaxDistance, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourcef.MaxDistance, value);
        }

        /// <summary>
        /// Gets or sets the minimum gain for this source. Range: [0.0f - 1.0f] (default: 0.0f).
        /// </summary>
        public float MinGain
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSourcef.MinGain, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourcef.MinGain, value);
        }

        /// <summary>
        /// Gets or sets the maximum gain for this source. Range: [0.0f - 1.0f] (default: 0.0f).
        /// </summary>
        public float MaxGain
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSourcef.MaxGain, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourcef.MaxGain, value);
        }

        /// <summary>
        /// Gets or sets the gain outside the oriented audio cone. Range: [0.0f - 1.0f] (default: 0.0f).
        /// </summary>
        public float ConeOuterGain
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSourcef.ConeOuterGain, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourcef.ConeOuterGain, value);
        }

        /// <summary>
        /// Gets or sets the inner angle of the oriented audio cone. Range: [0.0f - 360.0f] (default: 360.0f).
        /// </summary>
        public float ConeInnerAngle
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSourcef.ConeInnerAngle, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourcef.ConeInnerAngle, value);
        }

        /// <summary>
        /// Gets or sets the inner angle of the oriented audio cone. Range: [0.0f - 360.0f] (default: 360.0f).
        /// </summary>
        public float ConeOuterAngle
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSourcef.ConeOuterAngle, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourcef.ConeOuterAngle, value);
        }

        /// <summary>
        /// Gets or sets the pitch of the audio source. Range: [0.5f - 2.0f] (default 1.0f).
        /// </summary>
        public float Pitch
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSourcef.Pitch, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourcef.Pitch, value);
        }

        /// <summary>
        /// Gets or sets the gain of the audio source. Range: [0.0f - float.PositiveInfinity]. A value of 1.0f means
        /// unchanged. The scale is logarithmic.
        /// </summary>
        public float Gain
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSourcef.Gain, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourcef.Gain, value);
        }

        /// <summary>
        /// Gets or sets the rolloff factor. Range: [0.0f - float.PositiveInfinity].
        /// </summary>
        public float RolloffFactor
        {
            get
            {
                AL.GetSource(_soundSourceID, ALSourcef.RolloffFactor, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourcef.RolloffFactor, value);
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
                AL.GetSource(_soundSourceID, ALSourcef.ReferenceDistance, out var value);
                return value;
            }
            set => AL.Source(_soundSourceID, ALSourcef.ReferenceDistance, value);
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
            if (soundEntry == null)
            {
                throw new ArgumentNullException(nameof(soundEntry));
            }

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
            AL.SourceRewind(_soundSourceID);
            AL.SourcePlay(_soundSourceID);
        }

        /// <summary>
        /// Resumes playback of the source.
        /// </summary>
        public void Resume()
        {
            AL.SourcePlay(_soundSourceID);
        }

        /// <summary>
        /// Pauses the source.
        /// </summary>
        public void Pause()
        {
            AL.SourcePause(_soundSourceID);
        }

        /// <summary>
        /// Stops the source and resets the position of the audio.
        /// </summary>
        public void Stop()
        {
            AL.SourceStop(_soundSourceID);
            AL.SourceRewind(_soundSourceID);
        }

        /// <summary>
        /// Sets the audio data of the source, clearing the previous one.
        /// </summary>
        /// <param name="fileReference">A file reference pointing to a sound file.</param>
        /// <returns>An asynchronous task.</returns>
        public async Task SetAudioAsync(FileReference fileReference)
        {
            if (fileReference == null)
            {
                throw new ArgumentNullException(nameof(fileReference));
            }

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
            _soundBufferID = AL.GenBuffer();
            _soundSourceID = AL.GenSource();

            AL.BufferData(_soundBufferID, this.SoundFormat, soundData, soundData.Length, this.SampleRate);
            AL.Source(_soundSourceID, ALSourcei.Buffer, _soundBufferID);
        }

        /// <summary>
        /// Clears the audio from the player.
        /// </summary>
        private void ClearAudio()
        {
            AL.SourceStop(_soundSourceID);

            AL.DeleteSource(_soundSourceID);
            AL.DeleteBuffer(_soundBufferID);

            _audioAsset?.Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Stop();
            ClearAudio();
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash *= 23 + _soundBufferID;
                hash *= 23 + _soundSourceID;

                return hash;
            }
        }

        /// <inheritdoc />
        public bool Equals(AudioSource other)
        {
            return Equals((object)other);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (!(obj is AudioSource))
            {
                return false;
            }

            var other = (AudioSource)obj;
            return other._soundSourceID == _soundSourceID &&
                   other._soundBufferID == _soundBufferID;
        }
    }
}
