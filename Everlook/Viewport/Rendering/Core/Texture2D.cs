//
//  Texture2D.cs
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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

using log4net;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Warcraft.BLP;
using Warcraft.Core.Structures;
using GLPixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using Image = System.Drawing.Image;
using Rectangle = System.Drawing.Rectangle;
using SysPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Everlook.Viewport.Rendering.Core
{
    /// <summary>
    /// Wraps functionality around an OpenGL texture.
    /// </summary>
    public sealed class Texture2D : IDisposable
    {
        /// <summary>
        /// Logger instance for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(Texture2D));

        private readonly object _textureLock = new object();
        private int _nativeTextureID;

        /// <summary>
        /// Gets or sets the filter used when magnifying the texture.
        /// </summary>
        public TextureMagFilter MagnificationFilter
        {
            get => GetMagnificationFilter();
            set => SetMagnificationFilter(value);
        }

        /// <summary>
        /// Gets or sets the filter used when miniturizing the texture.
        /// </summary>
        public TextureMinFilter MiniaturizationFilter
        {
            get => GetMiniaturizationFilter();
            set => SetMiniaturizationFilter(value);
        }

        /// <summary>
        /// Sets the wrapping mode of the texture on the S and T axes.
        /// </summary>
        public TextureWrapMode WrappingMode
        {
            set => SetWrappingMode(value, value);
        }

        /// <summary>
        /// Gets or sets the wrapping mode of the texture on the S axis.
        /// </summary>
        public TextureWrapMode WrappingModeS
        {
            get => GetWrappingModeS();
            set => SetWrappingModeS(value);
        }

        /// <summary>
        /// Gets or sets the wrapping mode of the texture on the T axis.
        /// </summary>
        public TextureWrapMode WrappingModeT
        {
            get => GetWrappingModeT();
            set => SetWrappingModeT(value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Texture2D"/> class.
        /// </summary>
        private Texture2D()
        {
            _nativeTextureID = GL.GenTexture();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Texture2D"/> class from a given bitmap. This will generate
        /// mipmaps for the bitmap.
        /// </summary>
        /// <param name="imageData">The image data to create the texture from.</param>
        /// <param name="wrapMode">Optional. The wrapping mode to use for the texture.</param>
        /// <exception cref="ArgumentNullException">Thrown if the image data is null.</exception>
        public Texture2D(Bitmap imageData, TextureWrapMode wrapMode = TextureWrapMode.Repeat)
            : this()
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
        /// Initializes a new instance of the <see cref="Texture2D"/> class from a given compressed texture.
        /// Mipmaps are loaded from the compressed texture.
        /// </summary>
        /// <param name="imageData">The image data to create the texture from.</param>
        /// <param name="magFilter">Optional. The magnification filter to use for the texture.</param>
        /// <param name="minFilter">Optional. The miniaturization filter to use for the texture.</param>
        /// <exception cref="ArgumentNullException">Thrown if the image data is null.</exception>
        public Texture2D(BLP imageData, TextureMagFilter magFilter, TextureMinFilter minFilter)
            : this(imageData, TextureWrapMode.Repeat, TextureWrapMode.Repeat, magFilter, minFilter)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Texture2D"/> class from a given compressed texture.
        /// Mipmaps are loaded from the compressed texture.
        /// </summary>
        /// <param name="imageData">The image data to create the texture from.</param>
        /// <param name="magFilter">The magnification filter to use for the texture.</param>
        /// <param name="minFilter">The miniaturization filter to use for the texture.</param>
        /// <exception cref="ArgumentNullException">Thrown if the image data is null.</exception>
        public Texture2D(BLP imageData, TextureMinFilter minFilter, TextureMagFilter magFilter)
            : this(imageData, magFilter, minFilter)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Texture2D"/> class from a given compressed texture.
        /// Mipmaps are loaded from the compressed texture.
        /// </summary>
        /// <param name="imageData">The image data to create the texture from.</param>
        /// <param name="wrapModeS">The wrapping mode to use for the texture on the S axis.</param>
        /// <param name="wrapModeT">The wrapping mode to use for the texture on the T axis.</param>
        /// <param name="magFilter">The magnification filter to use for the texture.</param>
        /// <param name="minFilter">The miniaturization filter to use for the texture.</param>
        /// <exception cref="ArgumentNullException">Thrown if the image data is null.</exception>
        public Texture2D(
            BLP imageData,
            TextureWrapMode wrapModeS = TextureWrapMode.Repeat,
            TextureWrapMode wrapModeT = TextureWrapMode.Repeat,
            TextureMagFilter magFilter = TextureMagFilter.Linear,
            TextureMinFilter minFilter = TextureMinFilter.LinearMipmapLinear)
            : this()
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
                    using (var mipZero = imageData.GetMipMap(0))
                    {
                        CreateFromImage(mipZero);
                    }
                }
            }
            else
            {
                using (var mipZero = imageData.GetMipMap(0))
                {
                    CreateFromImage(mipZero);
                }
            }

            this.MagnificationFilter = magFilter;
            this.MiniaturizationFilter = minFilter;
            this.WrappingModeS = wrapModeS;
            this.WrappingModeT = wrapModeT;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="Texture2D"/> class.
        /// </summary>
        ~Texture2D()
        {
            Dispose();
        }

        /// <summary>
        /// Sets the filter used when magnifying the texture.
        /// </summary>
        private void SetMagnificationFilter(TextureMagFilter magFilter)
        {
            lock (_textureLock)
            {
                Bind();
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);
            }
        }

        /// <summary>
        /// Sets the filter used when miniaturizing the texture.
        /// </summary>
        private void SetMiniaturizationFilter(TextureMinFilter minFilter)
        {
            lock (_textureLock)
            {
                Bind();
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)minFilter);
            }
        }

        /// <summary>
        /// Gets the filter used when magnifying the texture.
        /// </summary>
        /// <returns>The magnification filter.</returns>
        private TextureMagFilter GetMagnificationFilter()
        {
            lock (_textureLock)
            {
                Bind();
                GL.GetTexParameter(TextureTarget.Texture2D, GetTextureParameter.TextureMagFilter, out int magFilter);
                return (TextureMagFilter)magFilter;
            }
        }

        /// <summary>
        /// Gets the filter used when miniaturizing the texture.
        /// </summary>
        /// <returns>The miniaturization filter.</returns>
        private TextureMinFilter GetMiniaturizationFilter()
        {
            lock (_textureLock)
            {
                Bind();
                GL.GetTexParameter(TextureTarget.Texture2D, GetTextureParameter.TextureMinFilter, out int minFilter);
                return (TextureMinFilter)minFilter;
            }
        }

        /// <summary>
        /// Sets the texture's wrapping mode on the S and T axes.
        /// </summary>
        /// <param name="wrapModeS">The wrapping mode on the S axis.</param>
        /// <param name="wrapModeT">The wrapping mode on the T axis.</param>
        private void SetWrappingMode(TextureWrapMode wrapModeS, TextureWrapMode wrapModeT)
        {
            lock (_textureLock)
            {
                Bind();
                SetWrappingModeS(wrapModeS);
                SetWrappingModeT(wrapModeT);
            }
        }

        /// <summary>
        /// Sets the texture's wrapping mode on the S axis.
        /// </summary>
        /// <param name="wrapModeS">The wrapping mode on the S axis.</param>
        private void SetWrappingModeS(TextureWrapMode wrapModeS)
        {
            lock (_textureLock)
            {
                Bind();
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapModeS);
            }
        }

        /// <summary>
        /// Sets the texture's wrapping mode on the T axis.
        /// </summary>
        /// <param name="wrapModeT">The wrapping mode on the T axis.</param>
        private void SetWrappingModeT(TextureWrapMode wrapModeT)
        {
            lock (_textureLock)
            {
                Bind();
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapModeT);
            }
        }

        /// <summary>
        /// Gets the texture's wrapping mode on the S axis.
        /// </summary>
        /// <returns>The wrapping mode on the S axis.</returns>
        private TextureWrapMode GetWrappingModeS()
        {
            lock (_textureLock)
            {
                Bind();
                GL.GetTexParameter(TextureTarget.Texture2D, GetTextureParameter.TextureWrapS, out int wrapModeS);
                return (TextureWrapMode)wrapModeS;
            }
        }

        /// <summary>
        /// Gets the texture's wrapping mode on the T axis.
        /// </summary>
        /// <returns>The wrapping mode on the T axis.</returns>
        private TextureWrapMode GetWrappingModeT()
        {
            lock (_textureLock)
            {
                Bind();
                GL.GetTexParameter(TextureTarget.Texture2D, GetTextureParameter.TextureWrapT, out int wrapModeT);
                return (TextureWrapMode)wrapModeT;
            }
        }

        /// <summary>
        /// Creates a native OpenGL texture from compressed DXT data.
        /// </summary>
        /// <param name="inTextureData">The bitmap data.</param>
        /// <exception cref="ArgumentException">Thrown if the image format doesn't match the pixel format.</exception>
        private void CreateFromDXT(BLP inTextureData)
        {
            lock (_textureLock)
            {
                Bind();

                for (uint i = 0; i < inTextureData.GetMipMapCount(); ++i)
                {
                    var compressedMipMap = inTextureData.GetRawMipMap(i);
                    var mipResolution = inTextureData.GetMipLevelResolution(i);

                    InternalFormat compressionFormat;
                    switch (inTextureData.GetPixelFormat())
                    {
                        case BLPPixelFormat.DXT1:
                        {
                            compressionFormat = InternalFormat.CompressedRgbaS3tcDxt1Ext;
                            break;
                        }
                        case BLPPixelFormat.DXT3:
                        {
                            compressionFormat = InternalFormat.CompressedRgbaS3tcDxt3Ext;
                            break;
                        }
                        case BLPPixelFormat.DXT5:
                        {
                            compressionFormat = InternalFormat.CompressedRgbaS3tcDxt5Ext;
                            break;
                        }
                        default:
                        {
                            throw new ArgumentException
                            (
                                $"Image format (DXTC) did not match pixel format: {inTextureData.GetPixelFormat()}",
                                nameof(Image)
                            );
                        }
                    }

                    // Load the mipmap into the texture
                    GL.CompressedTexImage2D
                    (
                        TextureTarget.Texture2D,
                        (int)i,
                        compressionFormat,
                        (int)mipResolution.X,
                        (int)mipResolution.Y,
                        0,
                        compressedMipMap.Length,
                        compressedMipMap
                    );
                }
            }
        }

        /// <summary>
        /// Creates a native OpenGL texture from bitmap data.
        /// </summary>
        /// <param name="inTextureData">The bitmap data.</param>
        private void CreateFromBitmap(Bitmap inTextureData)
        {
            lock (_textureLock)
            {
                Bind();

                // Extract raw RGB data from the largest bitmap
                var pixels = inTextureData.LockBits
                (
                    new Rectangle(0, 0, inTextureData.Width, inTextureData.Height),
                    ImageLockMode.ReadOnly,
                    SysPixelFormat.Format32bppArgb
                );

                GL.TexImage2D
                (
                    TextureTarget.Texture2D,
                    0, // level
                    PixelInternalFormat.Rgba,
                    pixels.Width,
                    pixels.Height,
                    0, // border
                    GLPixelFormat.Bgra,
                    PixelType.UnsignedByte,
                    pixels.Scan0
                );

                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

                inTextureData.UnlockBits(pixels);
            }
        }

        private void CreateFromImage(Image<Rgba32> inTextureData)
        {
            lock (_textureLock)
            {
                Bind();

                // Extract raw RGB data from the largest bitmap
                unsafe
                {
                    fixed (void* ptr = &inTextureData.GetPixelSpan().GetPinnableReference())
                    {
                        GL.TexImage2D
                        (
                            TextureTarget.Texture2D,
                            0, // level
                            PixelInternalFormat.Rgba,
                            inTextureData.Width,
                            inTextureData.Height,
                            0, // border
                            GLPixelFormat.Rgba,
                            PixelType.UnsignedByte,
                            new IntPtr(ptr)
                        );
                    }
                }

                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            }
        }

        /// <summary>
        /// Binds the texture as the current one in the OpenGL pipeline. This is only needed for actions
        /// which the texture itself is not directly responsible for.
        /// </summary>
        public void Bind()
        {
            lock (_textureLock)
            {
                GL.BindTexture(TextureTarget.Texture2D, _nativeTextureID);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_textureLock)
            {
                GC.SuppressFinalize(this);

                GL.DeleteTexture(_nativeTextureID);
                _nativeTextureID = -1;
            }
        }
    }
}
