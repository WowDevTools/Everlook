//
//  Texture2D.cs
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
using System.Drawing;
using System.Drawing.Imaging;
using log4net;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using Warcraft.BLP;
using Warcraft.Core.Structures;
using GLPixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using SysPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Everlook.Viewport.Rendering.Core
{
	public class Texture2D : IDisposable
	{
		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(Texture2D));

		private int NativeTextureID;

		/// <summary>
		/// Gets or sets the filter used when magnifying the texture.
		/// </summary>
		/// <returns></returns>
		public TextureMagFilter MagnificationFilter
		{
			get => GetMagnificationFilter();
			set => SetMagnificationFilter(value);
		}

		/// <summary>
		/// Gets or sets the filter used when miniturizing the texture.
		/// </summary>
		/// <returns></returns>
		public TextureMinFilter MiniaturizationFilter
		{
			get => GetMiniaturizationFilter();
			set => SetMiniaturizationFilter(value);
		}

		/// <summary>
		/// Sets the wrapping mode of the texture on the S and T axes.
		/// </summary>
		/// <returns></returns>
		public TextureWrapMode WrappingMode
		{
			set => SetWrappingMode(value, value);
		}

		/// <summary>
		/// Gets or sets the wrapping mode of the texture on the S axis.
		/// </summary>
		/// <returns></returns>
		public TextureWrapMode WrappingModeS
		{
			get => GetWrappingModeS();
			set => SetWrappingModeS(value);
		}

		/// <summary>
		/// Gets or sets the wrapping mode of the texture on the T axis.
		/// </summary>
		/// <returns></returns>
		public TextureWrapMode WrappingModeT
		{
			get => GetWrappingModeT();
			set => SetWrappingModeT(value);
		}

		/// <summary>
		/// Initializes a new <see cref="Texture2D"/> object.
		/// </summary>
		protected Texture2D()
		{
			this.NativeTextureID = GL.GenTexture();
		}

		/// <summary>
		/// Initializes a new <see cref="Texture2D"/> from a given bitmap. This will generate mipmaps for
		/// the bitmap.
		/// </summary>
		/// <param name="imageData">The image data to create the texture from.</param>
		/// <param name="wrapMode">Optional. The wrapping mode to use for the texture.</param>
		/// <exception cref="ArgumentNullException">Thrown if the image data is null.</exception>
		public Texture2D(Bitmap imageData, TextureWrapMode wrapMode = TextureWrapMode.Repeat) : this()
		{
			if (imageData == null)
			{
				throw new ArgumentNullException(nameof(imageData));
			}

			CreateFromBitmap(imageData);

			this.MagnificationFilter = TextureMagFilter.Linear;
			this.MiniaturizationFilter = TextureMinFilter.LinearMipmapLinear;
			this.WrappingMode = wrapMode;
		}

		/// <summary>
		/// Initializes a new <see cref="Texture2D"/> from a given compressed texture. Mipmaps are loaded
		/// from the compressed texture.
		/// </summary>
		/// <param name="imageData">The image data to create the texture from.</param>
		/// <param name="wrapMode">Optional. The wrapping mode to use for the texture.</param>
		/// <exception cref="ArgumentNullException">Thrown if the image data is null.</exception>
		public Texture2D(BLP imageData, TextureWrapMode wrapMode = TextureWrapMode.Repeat) : this()
		{
			if (imageData == null)
			{
				throw new ArgumentNullException(nameof(imageData));
			}

			if (imageData.GetCompressionType() == TextureCompressionType.DXTC)
			{
				try
				{
					CreateFromDXT(imageData);
				}
				catch (GraphicsErrorException gex)
				{
					Log.Warn($"GraphicsErrorException in CreateFromDXT (failed to create DXT texture): {gex.Message}\n" +
					         "The texture will be loaded as a bitmap instead.");
				}
				finally
				{
					// Load a fallback bitmap instead
					using (Bitmap mipZero = imageData.GetMipMap(0))
					{
						CreateFromBitmap(mipZero);
					}
				}
			}
			else
			{
				using (Bitmap mipZero = imageData.GetMipMap(0))
				{
					CreateFromBitmap(mipZero);
				}
			}

			this.MagnificationFilter = TextureMagFilter.Linear;
			this.MiniaturizationFilter = TextureMinFilter.LinearMipmapLinear;
			this.WrappingMode = wrapMode;
		}

		/// <summary>
		/// Sets the filter used when magnifying the texture.
		/// </summary>
		/// <returns></returns>
		private void SetMagnificationFilter(TextureMagFilter magFilter)
		{
			Bind();
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);
		}

		/// <summary>
		/// Sets the filter used when miniaturizing the texture.
		/// </summary>
		/// <returns></returns>
		private void SetMiniaturizationFilter(TextureMinFilter minFilter)
		{
			Bind();
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)minFilter);
		}

		/// <summary>
		/// Gets the filter used when magnifying the texture.
		/// </summary>
		/// <returns></returns>
		private TextureMagFilter GetMagnificationFilter()
		{
			Bind();
			GL.GetTexParameter(TextureTarget.Texture2D, GetTextureParameter.TextureMagFilter, out int magFilter);

			return (TextureMagFilter)magFilter;
		}

		/// <summary>
		/// Gets the filter used when miniaturizing the texture.
		/// </summary>
		/// <returns></returns>
		private TextureMinFilter GetMiniaturizationFilter()
		{
			Bind();
			GL.GetTexParameter(TextureTarget.Texture2D, GetTextureParameter.TextureMinFilter, out int minFilter);

			return (TextureMinFilter)minFilter;
		}

		/// <summary>
		/// Sets the texture's wrapping mode on the S and T axes.
		/// </summary>
		/// <param name="wrapModeS"></param>
		/// <param name="wrapModeT"></param>
		private void SetWrappingMode(TextureWrapMode wrapModeS, TextureWrapMode wrapModeT)
		{
			Bind();
			SetWrappingModeS(wrapModeS);
			SetWrappingModeT(wrapModeT);
		}

		/// <summary>
		/// Sets the texture's wrapping mode on the S axis.
		/// </summary>
		/// <param name="wrapModeS"></param>
		private void SetWrappingModeS(TextureWrapMode wrapModeS)
		{
			Bind();
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapModeS);
		}

		/// <summary>
		/// Sets the texture's wrapping mode on the T axis.
		/// </summary>
		/// <param name="wrapModeT"></param>
		private void SetWrappingModeT(TextureWrapMode wrapModeT)
		{
			Bind();
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapModeT);
		}

		/// <summary>
		/// Gets the texture's wrapping mode on the S axis.
		/// </summary>
		/// <returns></returns>
		private TextureWrapMode GetWrappingModeS()
		{
			Bind();
			GL.GetTexParameter(TextureTarget.Texture2D, GetTextureParameter.TextureWrapS, out int wrapModeS);

			return (TextureWrapMode) wrapModeS;
		}

		/// <summary>
		/// Gets the texture's wrapping mode on the T axis.
		/// </summary>
		/// <returns></returns>
		private TextureWrapMode GetWrappingModeT()
		{
			Bind();
			GL.GetTexParameter(TextureTarget.Texture2D, GetTextureParameter.TextureWrapT, out int wrapModeT);
			return (TextureWrapMode) wrapModeT;
		}

		/// <summary>
		/// Creates a native OpenGL texture from compressed DXT data.
		/// </summary>
		/// <param name="inTextureData"></param>
		/// <exception cref="ArgumentException"></exception>
		private void CreateFromDXT(BLP inTextureData)
		{
			Bind();

			for (uint i = 0; i < inTextureData.GetMipMapCount(); ++i)
			{
				byte[] compressedMipMap = inTextureData.GetRawMipMap(i);
				Resolution mipResolution = inTextureData.GetMipLevelResolution(i);

				PixelInternalFormat compressionFormat;
				switch (inTextureData.GetPixelFormat())
				{
					case BLPPixelFormat.DXT1:
					{
						compressionFormat = PixelInternalFormat.CompressedRgbaS3tcDxt1Ext;
						break;
					}
					case BLPPixelFormat.DXT3:
					{
						compressionFormat = PixelInternalFormat.CompressedRgbaS3tcDxt3Ext;
						break;
					}
					case BLPPixelFormat.DXT5:
					{
						compressionFormat = PixelInternalFormat.CompressedRgbaS3tcDxt5Ext;
						break;
					}
					default:
					{
						throw new ArgumentException($"Image format (DXTC) did not match pixel format: {inTextureData.GetPixelFormat()}",
							nameof(Image));
					}
				}

				// Load the mipmap into the texture
				GL.CompressedTexImage2D(TextureTarget.Texture2D, (int)i,
					compressionFormat,
					(int)mipResolution.X,
					(int)mipResolution.Y,
					0,
					compressedMipMap.Length,
					compressedMipMap);
			}
		}

		/// <summary>
		/// Creates a native OpenGL texture from bitmap data.
		/// </summary>
		/// <param name="inTextureData"></param>
		private void CreateFromBitmap(Bitmap inTextureData)
		{
			Bind();

			// Extract raw RGB data from the largest bitmap
			BitmapData pixels = inTextureData.LockBits(new Rectangle(0, 0, inTextureData.Width, inTextureData.Height),
				ImageLockMode.ReadOnly, SysPixelFormat.Format32bppArgb);

			GL.TexImage2D(
				TextureTarget.Texture2D,
				0, // level
				PixelInternalFormat.Rgba,
				pixels.Width,
				pixels.Height,
				0, // border
				GLPixelFormat.Bgra,
				PixelType.UnsignedByte,
				pixels.Scan0);

			GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

			inTextureData.UnlockBits(pixels);
		}

		/// <summary>
		/// Binds the texture as the current one in the OpenGL pipeline. This is only needed for actions
		/// which the texture itself is not directly responsible for.
		/// </summary>
		public void Bind()
		{
			GL.BindTexture(TextureTarget.Texture2D, this.NativeTextureID);
		}

		/// <summary>
		/// Disposes this <see cref="Texture2D"/>, deleting the underlying data.
		/// </summary>
		public void Dispose()
		{
			GL.DeleteTexture(this.NativeTextureID);
			this.NativeTextureID = -1;
		}
	}
}