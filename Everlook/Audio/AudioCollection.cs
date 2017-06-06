//
//  AudioCollection.cs
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

using System.Collections.Generic;
using Everlook.Viewport.Rendering.Core;

namespace Everlook.Audio
{
	public class AudioCollection
	{
		private List<AudioSource> AudioSources = new List<AudioSource>();

		public Transform ActorTransform;

		/// <summary>
		/// Adds a source to the audio collection.
		/// </summary>
		/// <param name="audioSource"></param>
		public void AddSource(AudioSource audioSource)
		{
			if (!this.AudioSources.Contains(audioSource))
			{
				this.AudioSources.Add(audioSource);
			}
		}

		/// <summary>
		/// Removes a source from the audio collection
		/// </summary>
		/// <param name="audioSource"></param>
		public void RemoveSource(AudioSource audioSource)
		{
			if (this.AudioSources.Contains(audioSource))
			{
				this.AudioSources.Remove(audioSource);
			}
		}

		/// <summary>
		/// Plays all sources in the collection.
		/// </summary>
		public void PlayAll()
		{
			foreach (var audioSource in this.AudioSources)
			{
				audioSource.Play();
			}
		}

		/// <summary>
		/// Pauses all sources in the collection.
		/// </summary>
		public void PauseAll()
		{
			foreach (var audioSource in this.AudioSources)
			{
				audioSource.Pause();
			}
		}

		/// <summary>
		/// Stops all sources in the collection.
		/// </summary>
		public void StopAll()
		{
			foreach (var audioSource in this.AudioSources)
			{
				audioSource.Stop();
			}
		}
	}
}