//
//  AudioGroup.cs
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
using System.Collections.Generic;
using System.Numerics;

namespace Everlook.Audio
{
    /// <summary>
    /// Represents a group of concurrently playing and linked audio sources, such as music collections or
    /// doodad sounds which should be considered as a unit. Each audio source in a collection must be unique.
    /// </summary>
    public sealed class AudioGroup : IDisposable
    {
        private readonly List<AudioSource> _audioSources = new List<AudioSource>();

        private Vector3 _positionInternal;

        /// <summary>
        /// Gets or sets the location at which the audio collection is. The locations of any sources
        /// added to this collection are placed at the same point.
        /// </summary>
        public Vector3 Position
        {
            get => _positionInternal;
            set
            {
                _positionInternal = value;
                foreach (var audioSource in _audioSources)
                {
                    audioSource.Position = value;
                }
            }
        }

        /// <summary>
        /// Adds a source to the audio collection.
        /// </summary>
        /// <param name="audioSource">The audio source to add to the collection.</param>
        public void AddSource(AudioSource audioSource)
        {
            if (!_audioSources.Contains(audioSource))
            {
                _audioSources.Add(audioSource);
            }
        }

        /// <summary>
        /// Removes a source from the audio collection.
        /// </summary>
        /// <param name="audioSource">The audio source to add to the collection.</param>
        public void RemoveSource(AudioSource audioSource)
        {
            if (_audioSources.Contains(audioSource))
            {
                _audioSources.Remove(audioSource);
            }
        }

        /// <summary>
        /// Plays all sources in the collection.
        /// </summary>
        public void PlayAll()
        {
            foreach (var audioSource in _audioSources)
            {
                audioSource.Play();
            }
        }

        /// <summary>
        /// Pauses all sources in the collection.
        /// </summary>
        public void PauseAll()
        {
            foreach (var audioSource in _audioSources)
            {
                audioSource.Pause();
            }
        }

        /// <summary>
        /// Stops all sources in the collection.
        /// </summary>
        public void StopAll()
        {
            foreach (var audioSource in _audioSources)
            {
                audioSource.Stop();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var audioSource in _audioSources)
            {
                audioSource.Dispose();
            }

            _audioSources.Clear();
        }
    }
}
