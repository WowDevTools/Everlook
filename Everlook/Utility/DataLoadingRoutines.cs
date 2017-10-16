//
//  DataLoadingRoutines.cs
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
using Everlook.Explorer;
using Everlook.Viewport.Rendering;
using Everlook.Viewport.Rendering.Interfaces;
using log4net;
using Warcraft.BLP;
using Warcraft.Core;
using Warcraft.Core.Extensions;
using Warcraft.MDX;
using Warcraft.MDX.Geometry.Skin;
using Warcraft.WMO;
using Warcraft.WMO.GroupFile;

namespace Everlook.Utility
{
	/// <summary>
	/// DataLoadingRoutines contains a set of procedures for loading and creating deserialized objects out
	/// of file data, using classes from libwarcraft. These helper functions allow for asynchronous loading of
	/// objects into the UI, for example.
	/// </summary>
	public static class DataLoadingRoutines
	{
		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(DataLoadingRoutines));

		/// <summary>
		/// Loads the specified WMO file from the archives and deserialize it.
		/// </summary>
		/// <param name="fileReference">The archive reference to the WMO root object.</param>
		/// <returns>A WMO object.</returns>
		public static WMO LoadWorldModel(FileReference fileReference)
		{
			if (fileReference == null)
			{
				throw new ArgumentNullException(nameof(fileReference));
			}

			WMO worldModel;
			try
			{
				byte[] fileData = fileReference.Extract();
				worldModel = new WMO(fileData);

				string modelPathWithoutExtension = $"{fileReference.FileDirectory.Replace('/', '\\')}\\{Path.GetFileNameWithoutExtension(fileReference.Filename)}";
				for (int i = 0; i < worldModel.GroupCount; ++i)
				{
					// Extract the groups as well
					string modelGroupPath = $"{modelPathWithoutExtension}_{i:D3}.wmo";

					try
					{
						byte[] modelGroupData = fileReference.Context.Assets.ExtractFile(modelGroupPath);
						worldModel.AddModelGroup(new ModelGroup(modelGroupData));
					}
					catch (FileNotFoundException fex)
					{
						Log.Warn($"Failed to load model group \"{modelGroupPath}\": {fex}.");
						throw;
					}
				}
			}
			catch (InvalidFileSectorTableException fex)
			{
				Log.Warn(
					$"Failed to load the model \"{fileReference.FilePath}\" due to an invalid sector table (\"{fex.Message}\").");
				throw;
			}

			return worldModel;
		}

		/// <summary>
		/// Loads the specified WMO group file from the archives and deserialize it.
		/// </summary>
		/// <param name="fileReference">The archive reference to the model group.</param>
		/// <returns>A WMO object, containing just the specified model group.</returns>
		public static WMO LoadWorldModelGroup(FileReference fileReference)
		{
			if (fileReference == null)
			{
				throw new ArgumentNullException(nameof(fileReference));
			}

			// Get the file name of the root object
			string modelRootPath = fileReference.FilePath.Remove(fileReference.FilePath.Length - 8, 4);

			WMO worldModel;
			// Extract it and load just this model group
			try
			{
				byte[] rootData = fileReference.Context.Assets.ExtractFile(modelRootPath);
				worldModel = new WMO(rootData);

				byte[] modelGroupData = fileReference.Extract();
				worldModel.AddModelGroup(new ModelGroup(modelGroupData));
			}
			catch (FileNotFoundException fex)
			{
				Log.Warn($"Failed to load the model group \"{fileReference.FilePath}\": {fex}");
				throw;
			}
			catch (InvalidFileSectorTableException fex)
			{
				Log.Warn($"Failed to load the model group \"{fileReference.FilePath}\" due to an invalid sector table (\"{fex}\")");
				throw;
			}

			return worldModel;
		}

		/// <summary>
		/// Creates a renderable object from the specified WMO object, and the specified package group it belongs to.
		/// NOTE: This method *must* be called in the UI thread after the OpenGL context has been made current.
		/// The <see cref="IRenderable"/> constructors commonly make extensive use of OpenGL methods.
		/// </summary>
		/// <param name="worldModel">The model object.</param>
		/// <param name="fileReference">The reference it was constructed from.</param>
		/// <returns>An encapsulated renderable OpenGL object.</returns>
		public static IRenderable CreateRenderableWorldModel(WMO worldModel, FileReference fileReference)
		{
			var warcraftContext = fileReference.Context as WarcraftGameContext;
			if (warcraftContext == null)
			{
				// TODO: This is bad practice. Refactor
				throw new ArgumentException("The given context must be a warcraft-typed context.", nameof(fileReference.Context));
			}

			RenderableWorldModel renderableWorldModel = new RenderableWorldModel(worldModel, warcraftContext);
			renderableWorldModel.LoadDoodads();

			return renderableWorldModel;
		}

		/// <summary>
		/// Loads the specified BLP image from the archives and deserialize it.
		/// </summary>
		/// <param name="fileReference">A reference to a BLP image.</param>
		/// <returns>A BLP object containing the image data pointed to by the reference.</returns>
		public static BLP LoadBinaryImage(FileReference fileReference)
		{
			if (fileReference == null)
			{
				throw new ArgumentNullException(nameof(fileReference));
			}

			BLP image;
			try
			{
				byte[] fileData = fileReference.Extract();

				try
				{
					image = new BLP(fileData);
				}
				catch (FileLoadException fex)
				{
					Log.Warn(
						$"FileLoadException when loading BLP image: {fex.Message}\n" +
						$"Please report this on GitHub or via email.");
					throw;
				}
			}
			catch (FileNotFoundException fex)
			{
				Log.Warn($"Failed to extract image: {fex}");
				throw;
			}

			return image;
		}

		/// <summary>
		/// Creates a renderable object from the specified BLP object, aand the specified <see cref="FileReference"/>
		/// it is associated with.
		/// NOTE: This method *must* be called in the UI thread after the OpenGL context has been made current.
		/// The <see cref="IRenderable"/> constructors commonly make extensive use of OpenGL methods.
		/// </summary>
		/// <param name="binaryImage">The image object.</param>
		/// <param name="fileReference">The reference it was constructed from.</param>
		/// <returns>An encapsulated renderable OpenGL object.</returns>
		public static IRenderable CreateRenderableBinaryImage(BLP binaryImage, FileReference fileReference)
		{
			RenderableBLP renderableImage = new RenderableBLP(binaryImage, fileReference.FilePath);

			return renderableImage;
		}

		/// <summary>
		/// Loads the specified image from the archives and deserializes it into a bitmap.
		/// </summary>
		/// <param name="fileReference">A reference to an image.</param>
		/// <returns>A bitmap containing the image data pointed to by the reference.</returns>
		public static Bitmap LoadBitmapImage(FileReference fileReference)
		{
			if (fileReference == null)
			{
				throw new ArgumentNullException(nameof(fileReference));
			}

			Bitmap image;
			try
			{
				byte[] fileData = fileReference.Extract();
				using (MemoryStream ms = new MemoryStream(fileData))
				{
					image = new Bitmap(ms);
				}
			}
			catch (FileNotFoundException fex)
			{
				Log.Warn(
					$"Failed to load the image \"{fileReference.FilePath}\": {fex}");
				throw;
			}

			return image;
		}

		/// <summary>
		/// Creates a renderable object from the specified <see cref="Bitmap"/> object, and the specified
		/// <see cref="FileReference"/> it is associated with.
		/// NOTE: This method *must* be called in the UI thread after the OpenGL context has been made current.
		/// The <see cref="IRenderable"/> constructors commonly make extensive use of OpenGL methods.
		/// </summary>
		/// <param name="bitmapImage">The image object.</param>
		/// <param name="fileReference">The reference it was constructed from.</param>
		/// <returns>An encapsulated renderable OpenGL object.</returns>
		public static IRenderable CreateRenderableBitmapImage(Bitmap bitmapImage, FileReference fileReference)
		{
			RenderableBitmap renderableImage = new RenderableBitmap(bitmapImage, fileReference.FilePath);

			return renderableImage;
		}

		/// <summary>
		/// Loads the specified game model from the archives and deserializes it into an <see cref="MDX"/> model.
		/// </summary>
		/// <param name="fileReference">A reference to a model.</param>
		/// <returns>An object containing the model data pointed to by the reference.</returns>
		public static MDX LoadGameModel(FileReference fileReference)
		{
			if (fileReference == null)
			{
				throw new ArgumentNullException(nameof(fileReference));
			}

			MDX model;
			try
			{
				byte[] fileData = fileReference.Extract();
				model = new MDX(fileData);

				if (model.Version >= WarcraftVersion.Wrath)
				{
					// Load external skins
					var modelFilename = Path.GetFileNameWithoutExtension(fileReference.Filename);
					var modelDirectory = fileReference.FileDirectory.Replace(Path.DirectorySeparatorChar, '\\');

					List<MDXSkin> skins = new List<MDXSkin>();
					for (int i = 0; i < model.SkinCount; ++i)
					{
						var modelSkinPath = $"{modelDirectory}\\{modelFilename}{i:D2}.skin";
						var skinData = fileReference.Context.Assets.ExtractFile(modelSkinPath);

						using (var ms = new MemoryStream(skinData))
						{
							using (var br = new BinaryReader(ms))
							{
								var skinIdentifier = new string(br.ReadChars(4));
								if (skinIdentifier != "SKIN")
								{
									break;
								}

								skins.Add(br.ReadMDXSkin(model.Version));
							}
						}
					}

					model.SetSkins(skins);
				}
			}
			catch (FileNotFoundException fex)
			{
				Log.Warn($"Failed to load the model \"{fileReference.FilePath}\": {fex}");
				throw;
			}
			catch (InvalidFileSectorTableException fex)
			{
				Log.Warn($"Failed to load the model \"{fileReference.FilePath}\" due to an invalid sector table (\"{fex.Message}\").");
				throw;
			}

			return model;
		}

		/// <summary>
		/// Creates a renderable object from the specified <see cref="MDX"/> object, and the specified
		/// <see cref="FileReference"/> it is associated with.
		/// NOTE: This method *must* be called in the UI thread after the OpenGL context has been made current.
		/// The <see cref="IRenderable"/> constructors commonly make extensive use of OpenGL methods.
		/// </summary>
		/// <param name="gameModel">The model object.</param>
		/// <param name="fileReference">The reference it was constructed from.</param>
		/// <returns>An encapsulated renderable OpenGL object.</returns>
		public static IRenderable CreateRenderableGameModel(MDX gameModel, FileReference fileReference)
		{
			var warcraftContext = fileReference.Context as WarcraftGameContext;
			if (warcraftContext == null)
			{
				// TODO: This is bad practice. Refactor
				throw new ArgumentException("The given context must be a warcraft-typed context.", nameof(fileReference.Context));
			}

			RenderableGameModel renderableModel = new RenderableGameModel(gameModel, warcraftContext, fileReference.FilePath);

			return renderableModel;
		}
	}
}
