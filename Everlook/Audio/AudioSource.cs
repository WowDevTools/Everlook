//
//  AudioSource.cs
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
using OpenTK;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

namespace Everlook.Audio
{
	/// <summary>
	/// Represents a single audio source in 3D space.
	/// </summary>
	public class AudioSource : IDisposable
	{
		private int SoundBufferID;
		private int SoundSourceID;

		/// <summary>
		/// Gets the format of the current audio source.
		/// </summary>
		public ALFormat SoundFormat { get; private set; }

		/// <summary>
		/// Gets the sample rate of the current audio source.
		/// </summary>
		public int SampleRate { get; private set; }

		/// <summary>
		/// Gets or sets whether or not this audio source should loop.
		/// </summary>
		public bool Looping
		{
			get => this.LoopingInternal;
			set
			{
				AL.Source(this.SoundSourceID, ALSourceb.Looping, value);
				this.LoopingInternal = value;
			}
		}
		private bool LoopingInternal;

		/// <summary>
		/// Gets or sets the position of the audio source in 3D space.
		/// </summary>
		public Vector3 Position
		{
			get => this.PositionInternal;
			set
			{
				AL.Source(this.SoundSourceID, ALSource3f.Position, ref value);
				this.PositionInternal = value;
			}
		}
		private Vector3 PositionInternal;

		/// <summary>
		/// Gets or sets the attenuation distance, that is, the distance beyond which the source will be silent,
		/// of the audio source.
		/// </summary>
		public float Attenuation
		{
			get => this.AttenuationInternal;
			set
			{
				AL.Source(this.SoundSourceID, ALSourcef.MaxDistance, value);
				this.AttenuationInternal = value;
			}
		}
		private float AttenuationInternal;

		/// <summary>
		/// Gets or sets whether or not the audio position should be relative to the listener's position.
		/// </summary>
		public bool RelativePositioning
		{
			get => this.RelativePositioningInternal;
			set
			{
				AL.Source(this.SoundSourceID, ALSourceb.SourceRelative, value);
				this.RelativePositioningInternal = value;
			}
		}
		private bool RelativePositioningInternal;

		
		
		/// <summary>
		/// Initializes a new instance of the <see cref="AudioSource"/> class.
		/// </summary>
		public AudioSource()
		{
			
		}

		/// <summary>
		/// Plays the source from the start.
		/// </summary>
		public void Play()
		{
			AL.SourceRewind(this.SoundSourceID);
			AL.SourcePlay(this.SoundSourceID);
		}

		/// <summary>
		/// Resumes playback of the source.
		/// </summary>
		public void Resume()
		{
			AL.SourcePlay(this.SoundSourceID);
		}

		/// <summary>
		/// Pauses the source.
		/// </summary>
		public void Pause()
		{
			AL.SourcePause(this.SoundSourceID);
		}

		/// <summary>
		/// Stops the source and resets the position of the audio.
		/// </summary>
		public void Stop()
		{
			AL.SourceRewind(this.SoundSourceID);
		}

		/// <summary>
		/// Sets the position of the audio stream (in nanoseconds from the start of the file)
		/// </summary>
		public void SetPosition(long nanoseconds)
		{

		}

		/// <summary>
		/// Sets the audio data of the source, clearing the previous one.
		/// </summary>
		/// <param name="fileReference">A file reference pointing to a sound file.</param>
		/// <returns>An asynchronous task.</returns>
		public async Task SetAudio(FileReference fileReference)
		{
			// First, clear the old audio
			ClearAudio();

			// Asynchronously load audio data
			byte[] soundData = null; // TODO 
			this.SoundFormat = 0; // TODO
			this.SampleRate = 0; // TODO

			// Create new AL data
			this.SoundBufferID = AL.GenBuffer();
			this.SoundSourceID = AL.GenSource();

			AL.BufferData(this.SoundBufferID, this.SoundFormat, soundData, soundData.Length, this.SampleRate);
			AL.Source(this.SoundSourceID, ALSourcei.Buffer, this.SoundBufferID);
		}

		/// <summary>
		/// Clears the audio from the player.
		/// </summary>
		private void ClearAudio()
		{
			AL.SourceStop(this.SoundSourceID);

			AL.DeleteSource(this.SoundSourceID);
			AL.DeleteBuffer(this.SoundBufferID);
		}

		/// <summary>
		/// Disposes the audio player, deleting underlying buffers and streams.
		/// </summary>
		public void Dispose()
		{
			ClearAudio();
		}
	}
}

