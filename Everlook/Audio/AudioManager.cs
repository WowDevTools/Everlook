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
using System.Collections.Generic;
using OpenTK.Audio;

namespace Everlook.Audio
{
	/// <summary>
	/// Manages the audio context of the application, and handles audio sources within it.
	/// </summary>
	public class AudioManager : IDisposable
	{
		/// <summary>
		/// Gets the singleton instance of the <see cref="AudioManager"/>.
		/// </summary>
		private static readonly AudioManager Instance = new AudioManager();

		/// <summary>
		/// All registered sources in the manager.
		/// </summary>
		private readonly List<AudioSource> Sources = new List<AudioSource>();

		private readonly AudioContext Context;

		/// <summary>
		/// Initializes a new instance of the <see cref="AudioManager"/> class.
		/// </summary>
		protected AudioManager()
		{
			this.Context = new AudioContext();
			this.Context.MakeCurrent();
		}

		/// <summary>
		/// Finalizes an instance of the <see cref="AudioManager"/> class.
		/// </summary>
		~AudioManager()
		{
			Dispose();
		}

		/// <summary>
		/// Registers an <see cref="AudioSource"/> with the manager. Registering a source will offload the resource
		/// management of the source to the manager.
		/// </summary>
		/// <param name="audioSource">The audio source to register.</param>
		public static void RegisterSource(AudioSource audioSource)
		{
			if (!IsRegistered(audioSource))
			{
				Instance.Sources.Add(audioSource);
			}
		}

		/// <summary>
		/// Unregisters an <see cref="AudioSource"/> with the manager. Unregistering a source will stop and dispose it.
		/// </summary>
		/// <param name="audioSource">The audio source to unregister.</param>
		public static void UnregisterSource(AudioSource audioSource)
		{
			if (audioSource == null)
			{
				return;
			}

			if (IsRegistered(audioSource))
			{
				Instance.Sources.Remove(audioSource);
			}

			audioSource.Dispose();
		}

		/// <summary>
		/// Determines whether or not an <see cref="AudioSource"/> is registered with the manager.
		/// </summary>
		/// <param name="audioSource">The audio source to check.</param>
		/// <returns>true if the source is registered; false otherwise.</returns>
		public static bool IsRegistered(AudioSource audioSource)
		{
			return Instance.Sources.Contains(audioSource);
		}

		/// <summary>
		/// Disposes the audio manager.
		/// </summary>
		public void Dispose()
		{
			foreach (AudioSource source in this.Sources)
			{
				source.Dispose();
			}

			this.Context.Dispose();
		}
	}
}
