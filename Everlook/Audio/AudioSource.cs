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
        private IAudioAsset _audioAsset;
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
        public ALSourceState State => AL.GetSourceState(this._soundSourceID);

        /// <summary>
        /// Gets or sets the offset of the audio source into the sound (in seconds).
        /// </summary>
        public float TimeOffset
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSourcef.SecOffset, out float value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourcef.SecOffset, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this audio source should loop.
        /// </summary>
        public bool Looping
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSourceb.Looping, out bool value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourceb.Looping, value);
        }

        /// <summary>
        /// Gets or sets the position of the audio source in 3D space.
        /// </summary>
        public Vector3 Position
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSource3f.Position, out Vector3 value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSource3f.Position, ref value);
        }

        /// <summary>
        /// Gets or sets the direction of the sound cone.
        /// </summary>
        public Vector3 Direction
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSource3f.Direction, out Vector3 value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSource3f.Direction, ref value);
        }

        /// <summary>
        /// Gets or sets the velocity vector of the sound source.
        /// </summary>
        public Vector3 Velocity
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSource3f.Direction, out Vector3 value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSource3f.Direction, ref value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the audio position should be relative to the listener's position.
        /// </summary>
        public bool RelativePositioning
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSourceb.SourceRelative, out bool value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourceb.SourceRelative, value);
        }

        /// <summary>
        /// Gets or sets the attenuation distance, that is, the distance beyond which the source will be silent,
        /// of the audio source. Range: [0.0f - float.PositiveInfinity] (default: float.PositiveInfinity).
        /// </summary>
        public float Attenuation
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSourcef.MaxDistance, out float value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourcef.MaxDistance, value);
        }

        /// <summary>
        /// Gets or sets the minimum gain for this source. Range: [0.0f - 1.0f] (default: 0.0f).
        /// </summary>
        public float MinGain
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSourcef.MinGain, out float value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourcef.MinGain, value);
        }

        /// <summary>
        /// Gets or sets the maximum gain for this source. Range: [0.0f - 1.0f] (default: 0.0f).
        /// </summary>
        public float MaxGain
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSourcef.MaxGain, out float value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourcef.MaxGain, value);
        }

        /// <summary>
        /// Gets or sets the gain outside the oriented audio cone. Range: [0.0f - 1.0f] (default: 0.0f).
        /// </summary>
        public float ConeOuterGain
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSourcef.ConeOuterGain, out float value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourcef.ConeOuterGain, value);
        }

        /// <summary>
        /// Gets or sets the inner angle of the oriented audio cone. Range: [0.0f - 360.0f] (default: 360.0f).
        /// </summary>
        public float ConeInnerAngle
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSourcef.ConeInnerAngle, out float value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourcef.ConeInnerAngle, value);
        }

        /// <summary>
        /// Gets or sets the inner angle of the oriented audio cone. Range: [0.0f - 360.0f] (default: 360.0f).
        /// </summary>
        public float ConeOuterAngle
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSourcef.ConeOuterAngle, out float value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourcef.ConeOuterAngle, value);
        }

        /// <summary>
        /// Gets or sets the pitch of the audio source. Range: [0.5f - 2.0f] (default 1.0f).
        /// </summary>
        public float Pitch
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSourcef.Pitch, out float value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourcef.Pitch, value);
        }

        /// <summary>
        /// Gets or sets the gain of the audio source. Range: [0.0f - float.PositiveInfinity]. A value of 1.0f means
        /// unchanged. The scale is logarithmic.
        /// </summary>
        public float Gain
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSourcef.Gain, out float value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourcef.Gain, value);
        }

        /// <summary>
        /// Gets or sets the rolloff factor. Range: [0.0f - float.PositiveInfinity].
        /// </summary>
        public float RolloffFactor
        {
            get
            {
                AL.GetSource(this._soundSourceID, ALSourcef.RolloffFactor, out float value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourcef.RolloffFactor, value);
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
                AL.GetSource(this._soundSourceID, ALSourcef.ReferenceDistance, out float value);
                return value;
            }
            set => AL.Source(this._soundSourceID, ALSourcef.ReferenceDistance, value);
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
            AudioSource source = new AudioSource();
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

            AudioSource source = CreateNew();
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
            AL.SourceRewind(this._soundSourceID);
            AL.SourcePlay(this._soundSourceID);
        }

        /// <summary>
        /// Resumes playback of the source.
        /// </summary>
        public void Resume()
        {
            AL.SourcePlay(this._soundSourceID);
        }

        /// <summary>
        /// Pauses the source.
        /// </summary>
        public void Pause()
        {
            AL.SourcePause(this._soundSourceID);
        }

        /// <summary>
        /// Stops the source and resets the position of the audio.
        /// </summary>
        public void Stop()
        {
            AL.SourceStop(this._soundSourceID);
            AL.SourceRewind(this._soundSourceID);
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
                    this._audioAsset = await WaveAudioAsset.LoadAsync(fileReference);
                    break;
                }
                case WarcraftFileType.MP3Audio:
                {
                    this._audioAsset = await MP3AudioAsset.LoadAsync(fileReference);
                    break;
                }
                default:
                {
                    return;
                }
            }

            byte[] soundData = this._audioAsset.PCMData;
            this.SoundFormat = this._audioAsset.Format;
            this.SampleRate = this._audioAsset.SampleRate;

            // Create new AL data
            this._soundBufferID = AL.GenBuffer();
            this._soundSourceID = AL.GenSource();

            AL.BufferData(this._soundBufferID, this.SoundFormat, soundData, soundData.Length, this.SampleRate);
            AL.Source(this._soundSourceID, ALSourcei.Buffer, this._soundBufferID);
        }

        /// <summary>
        /// Clears the audio from the player.
        /// </summary>
        private void ClearAudio()
        {
            AL.SourceStop(this._soundSourceID);

            AL.DeleteSource(this._soundSourceID);
            AL.DeleteBuffer(this._soundBufferID);

            this._audioAsset?.Dispose();
            this._audioAsset = null;
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
                int hash = 17;
                hash *= 23 + this._soundBufferID;
                hash *= 23 + this._soundSourceID;

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

            AudioSource other = (AudioSource)obj;
            return other._soundSourceID == this._soundSourceID &&
                   other._soundBufferID == this._soundBufferID;
        }
    }
}
