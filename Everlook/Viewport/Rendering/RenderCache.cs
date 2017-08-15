//
//  RenderCache.cs
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
using System.Drawing;
using Everlook.Utility;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Shaders;
using log4net;
using OpenTK.Graphics.OpenGL;
using Warcraft.BLP;
using GLPixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using SysPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Everlook.Viewport.Rendering
{
	/// <summary>
	/// OpenGL caching handler for objects that can be used more than once during a run of the program and
	/// may take some time to generate.
	///
	/// Currently, these are textures and shader programs.
	/// </summary>
	public class RenderCache : IDisposable
	{
		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(RenderCache));

		/// <summary>
		/// The cache dictionary that maps active OpenGL textures on the GPU.
		/// </summary>
		private readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();

		/// <summary>
		/// The cache dictionary that maps active OpenGL shaders on the GPU.
		/// </summary>
		private readonly Dictionary<EverlookShader, ShaderProgram> ShaderCache = new Dictionary<EverlookShader, ShaderProgram>();

		/// <summary>
		/// Gets the the fallback texture.
		/// </summary>
		public Texture2D FallbackTexture { get; }

		/// <summary>
		/// A singleton instance of the rendering cache.
		/// </summary>
		public static readonly RenderCache Instance = new RenderCache();

		private RenderCache()
		{
			this.FallbackTexture = new Texture2D(ResourceManager.GetFallbackImage());
		}

		/// <summary>
		/// Gets a <see cref="ShaderProgram"/> for the specified shader type. If one is not already in the cache, it
		/// will be created.
		/// </summary>
		/// <param name="shader">The type of shader to retrieve.</param>
		/// <returns>A shader program object.</returns>
		public ShaderProgram GetShader(EverlookShader shader)
		{
			if (HasCachedShader(shader))
			{
				return GetCachedShader(shader);
			}

			return CreateCachedShader(shader);
		}

		/// <summary>
		/// Determines whether or not the rendering cache has a cached texture id
		/// for the specified texture file path.
		/// </summary>
		/// <param name="texturePath">The path of the texture in its package group. Used as a lookup key.</param>
		/// <returns>true if a cached textures exists with the given path as a lookup key; false otherwise</returns>
		public bool HasCachedTextureForPath(string texturePath)
		{
			if (string.IsNullOrEmpty(texturePath))
			{
				throw new ArgumentNullException(nameof(texturePath));
			}

			return this.TextureCache.ContainsKey(texturePath.ConvertPathSeparatorsToCurrentNativeSeparator().ToUpperInvariant());
		}

		/// <summary>
		/// Determines whether or not the rendering cache has a cached shader
		/// for the specified shader type.
		/// </summary>
		private bool HasCachedShader(EverlookShader shader)
		{
			if (!Enum.IsDefined(typeof(EverlookShader), shader))
			{
				throw new ArgumentException("An unknown shader was passed to the rendering cache.", nameof(shader));
			}

			return this.ShaderCache.ContainsKey(shader);
		}

		/// <summary>
		/// Gets a cached texture ID from the rendering cache.
		/// </summary>
		/// <param name="texturePath">The path of the texture in its package group. Used as a lookup key.</param>
		/// <returns>A texture object.</returns>
		public Texture2D GetCachedTexture(string texturePath)
		{
			return this.TextureCache[texturePath.ConvertPathSeparatorsToCurrentNativeSeparator().ToUpperInvariant()];
		}

		/// <summary>
		/// Gets a cached shader ID from the rendering cache.
		/// </summary>
		private ShaderProgram GetCachedShader(EverlookShader shader)
		{
			return this.ShaderCache[shader];
		}

		/// <summary>
		/// Creates a cached texture for the specifed texture, using the specified path
		/// as a lookup key. This method will create a new texture, and cache it.
		/// </summary>
		/// /// <param name="imageData">A bitmap containing the image data.</param>
		/// <param name="texturePath">
		/// The path to the texture in its corresponding package group. This is used as a lookup key.
		/// </param>
		/// <param name="wrappingMode">How the texture should wrap.</param>
		/// <returns>A new cached texture created from the data.</returns>
		public Texture2D CreateCachedTexture(BLP imageData, string texturePath, TextureWrapMode wrappingMode = TextureWrapMode.Repeat)
		{
			Texture2D texture = new Texture2D(imageData, wrappingMode);

			this.TextureCache.Add(texturePath.ConvertPathSeparatorsToCurrentNativeSeparator().ToUpperInvariant(), texture);
			return texture;
		}

		/// <summary>
		/// Creates a cached texture for the specifed texture, using the specified path
		/// as a lookup key. This method will create a new texture, and cache it.
		/// </summary>
		/// <param name="imageData">A bitmap containing the image data.</param>
		/// <param name="texturePath">
		/// The path to the texture in its corresponding package group. This is used as a lookup key.
		/// </param>
		/// <returns>A new cached texture created from the data.</returns>
		public Texture2D CreateCachedTexture(Bitmap imageData, string texturePath)
		{
			Texture2D texture = new Texture2D(imageData);

			this.TextureCache.Add(texturePath.ConvertPathSeparatorsToCurrentNativeSeparator().ToUpperInvariant(), texture);
			return texture;
		}

		/// <summary>
		/// Creates a cached shader for the specifed shader, using the specified shader enumeration
		/// as a lookup key.
		/// </summary>
		private ShaderProgram CreateCachedShader(EverlookShader shader)
		{
			if (!Enum.IsDefined(typeof(EverlookShader), shader))
			{
				throw new ArgumentException("An unknown shader was passed to the rendering cache.", nameof(shader));
			}

			Log.Info($"Creating cached shader for \"{shader}\"");

			ShaderProgram shaderProgram;
			switch (shader)
			{
				case EverlookShader.Plain2D:
				{
					shaderProgram = new Plain2DShader();
					break;
				}
				case EverlookShader.WorldModel:
				{
					shaderProgram = new WorldModelShader();
					break;
				}
				case EverlookShader.BoundingBox:
				{
					shaderProgram = new BoundingBoxShader();
					break;
				}
				case EverlookShader.GameModel:
				case EverlookShader.Model:
				case EverlookShader.ParticleSystem:
				default:
				{
					throw new ArgumentOutOfRangeException(nameof(shader), "No implemented shader class for this shader.");
				}
			}

			this.ShaderCache.Add(shader, shaderProgram);
			return shaderProgram;
		}

		/// <summary>
		/// Disposes of the rendering cache, deleting any cached textures or shaders.
		/// </summary>
		public void Dispose()
		{
			foreach (KeyValuePair<string, Texture2D> cachedTexture in this.TextureCache)
			{
				cachedTexture.Value?.Dispose();
			}

			foreach (KeyValuePair<EverlookShader, ShaderProgram> cachedShader in this.ShaderCache)
			{
				cachedShader.Value?.Dispose();
			}
		}
	}
}
