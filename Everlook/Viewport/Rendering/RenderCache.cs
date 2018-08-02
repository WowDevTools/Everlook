//
//  RenderCache.cs
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
using System.Drawing;
using System.IO;
using Everlook.Utility;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Shaders;
using log4net;
using OpenTK.Graphics.OpenGL;
using Warcraft.BLP;
using Warcraft.Core;
using Warcraft.MDX.Visual;
using Warcraft.MPQ;
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
	public sealed class RenderCache : IDisposable
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
		/// Gets or sets a value indicating whether this object has been disposed.
		/// </summary>
		private bool IsDisposed { get; set; }

		/// <summary>
		/// Gets the the fallback texture.
		/// </summary>
		public Texture2D FallbackTexture
		{
			get
			{
				ThrowIfDisposed();

				if (this.FallbackTextureInternal == null)
				{
					this.FallbackTextureInternal = new Texture2D(ResourceManager.GetFallbackImage());
				}

				return this.FallbackTextureInternal;
			}
		}

		private Texture2D FallbackTextureInternal;

		/// <summary>
		/// A singleton instance of the rendering cache.
		/// </summary>
		public static readonly RenderCache Instance = new RenderCache();

		/// <summary>
		/// Finalizes an instance of the <see cref="RenderCache"/> class.
		/// </summary>
		~RenderCache()
		{
			Dispose();
		}

		/// <summary>
		/// Gets a <see cref="ShaderProgram"/> for the specified shader type. If one is not already in the cache, it
		/// will be created.
		/// </summary>
		/// <param name="shader">The type of shader to retrieve.</param>
		/// <returns>A shader program object.</returns>
		public ShaderProgram GetShader(EverlookShader shader)
		{
			ThrowIfDisposed();

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
		/// <returns>true if a cached textures exists with the given path as a lookup key; false otherwise.</returns>
		public bool HasCachedTextureForPath(string texturePath)
		{
			ThrowIfDisposed();

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
			ThrowIfDisposed();

			return this.TextureCache[texturePath.ConvertPathSeparatorsToCurrentNativeSeparator().ToUpperInvariant()];
		}

		/// <summary>
		/// Gets a cached shader ID from the rendering cache.
		/// </summary>
		private ShaderProgram GetCachedShader(EverlookShader shader)
		{
			ThrowIfDisposed();

			return this.ShaderCache[shader];
		}

		/// <summary>
		/// Gets a <see cref="Texture2D"/> instance from the cache. If the texture is not already cached, it is
		/// extracted from the given <see cref="IGameContext"/>. If it is cached, the cached version is returned. If no
		/// texture can be extracted, a fallback texture is returned.
		/// </summary>
		/// <param name="texture">The texture definition.</param>
		/// <param name="gameContext">The context of the texture definition.</param>
		/// <param name="texturePathOverride">Optional. Overrides the filename in the texture definition.</param>
		/// <returns>A <see cref="Texture2D"/> object.</returns>
		public Texture2D GetTexture(MDXTexture texture, IGameContext gameContext, string texturePathOverride = null)
		{
			ThrowIfDisposed();

			string filename = texture.Filename;
			if (string.IsNullOrEmpty(texture.Filename))
			{
				if (string.IsNullOrEmpty(texturePathOverride))
				{
					Log.Warn("Texture with empty filename requested.");
					return this.FallbackTexture;
				}

				filename = texturePathOverride;
			}

			var wrapS = texture.Flags.HasFlag(EMDXTextureFlags.TextureWrapX)
				? TextureWrapMode.Repeat
				: TextureWrapMode.Clamp;

			var wrapT = texture.Flags.HasFlag(EMDXTextureFlags.TextureWrapY)
				? TextureWrapMode.Repeat
				: TextureWrapMode.Clamp;

			return GetTexture(filename, gameContext.Assets, wrapS, wrapT);
		}

		/// <summary>
		/// Gets a <see cref="Texture2D"/> instance from the cache. If the texture is not already cached, it is
		/// extracted from the given <see cref="IPackage"/>. If it is cached, the cached version is returned. If no
		/// texture can be extracted, a fallback texture is returned.
		/// </summary>
		/// <param name="texturePath">The path to the texture in the package.</param>
		/// <param name="package">The package where the texture is stored.</param>
		/// <param name="wrappingModeS">The wrapping mode to use for the texture on the S axis.</param>
		/// <param name="wrappingModeT">The wrapping mode to use for the texture on the T axis.</param>
		/// <returns>A <see cref="Texture2D"/> object.</returns>
		public Texture2D GetTexture(string texturePath, IPackage package, TextureWrapMode wrappingModeS = TextureWrapMode.Repeat, TextureWrapMode wrappingModeT = TextureWrapMode.Repeat)
		{
			ThrowIfDisposed();

			if (HasCachedTextureForPath(texturePath))
			{
				return GetCachedTexture(texturePath);
			}

			try
			{
				WarcraftFileType textureType = FileInfoUtilities.GetFileType(texturePath);
				switch (textureType)
				{
					case WarcraftFileType.BinaryImage:
					{
						var textureData = package.ExtractFile(texturePath);

						if (textureData == null)
						{
							return this.FallbackTexture;
						}

						BLP texture = new BLP(textureData);
						return CreateCachedTexture(texture, texturePath, wrappingModeS, wrappingModeT);
					}
					case WarcraftFileType.BitmapImage:
					case WarcraftFileType.GIFImage:
					case WarcraftFileType.IconImage:
					case WarcraftFileType.PNGImage:
					case WarcraftFileType.JPGImage:
					case WarcraftFileType.TargaImage:
					{
						using (MemoryStream ms = new MemoryStream(package.ExtractFile(texturePath)))
						{
							Bitmap texture = new Bitmap(ms);
							return CreateCachedTexture(texture, texturePath);
						}
					}
				}
			}
			catch (InvalidFileSectorTableException fex)
			{
				Log.Warn
				(
					$"Failed to load the texture \"{texturePath}\" due to an invalid sector table (\"{fex.Message}\").\nA fallback texture has been loaded instead."
				);
			}

			return this.FallbackTexture;
		}

		/// <summary>
		/// Creates a cached texture for the specifed texture, using the specified path
		/// as a lookup key. This method will create a new texture, and cache it.
		/// </summary>
		/// /// <param name="imageData">A bitmap containing the image data.</param>
		/// <param name="texturePath">
		/// The path to the texture in its corresponding package group. This is used as a lookup key.
		/// </param>
		/// <param name="wrappingModeS">How the texture should wrap on the S axis.</param>
		/// <param name="wrappingModeT">How the texture should wrap on the T axis.</param>
		/// <returns>A new cached texture created from the data.</returns>
		public Texture2D CreateCachedTexture(BLP imageData, string texturePath, TextureWrapMode wrappingModeS = TextureWrapMode.Repeat, TextureWrapMode wrappingModeT = TextureWrapMode.Repeat)
		{
			ThrowIfDisposed();

			Texture2D texture = new Texture2D(imageData, wrappingModeS, wrappingModeT);

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
			ThrowIfDisposed();

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
				{
					shaderProgram = new GameModelShader();
					break;
				}
				case EverlookShader.BaseGrid:
				{
					shaderProgram = new BaseGridShader();
					break;
				}
				default:
				{
					throw new ArgumentOutOfRangeException(nameof(shader), "No implemented shader class for this shader.");
				}
			}

			this.ShaderCache.Add(shader, shaderProgram);
			return shaderProgram;
		}

		/// <summary>
		/// Throws an <see cref="ObjectDisposedException"/> if the object has been disposed.
		/// </summary>
		/// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
		private void ThrowIfDisposed()
		{
			if (this.IsDisposed)
			{
				throw new ObjectDisposedException(ToString());
			}
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (this.IsDisposed)
			{
				return;
			}

			this.IsDisposed = true;

			foreach (KeyValuePair<string, Texture2D> cachedTexture in this.TextureCache)
			{
				cachedTexture.Value?.Dispose();
			}
			this.TextureCache.Clear();

			foreach (KeyValuePair<EverlookShader, ShaderProgram> cachedShader in this.ShaderCache)
			{
				cachedShader.Value?.Dispose();
			}
			this.ShaderCache.Clear();
		}
	}
}
