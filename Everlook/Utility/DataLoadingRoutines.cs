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
using System.Drawing;
using System.IO;
using Everlook.Explorer;
using Everlook.Viewport.Rendering;
using Everlook.Viewport.Rendering.Interfaces;
using log4net;
using Warcraft.BLP;
using Warcraft.Core;
using Warcraft.MDX;
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
		/// TODO: Refactor this and LoadWorldModelGroup
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

			try
			{
				byte[] fileData = fileReference.Extract();
				if (fileData != null)
				{
					WMO worldModel = new WMO(fileData);

					string modelPathWithoutExtension = Path.GetFileNameWithoutExtension(fileReference.FilePath);
					for (int i = 0; i < worldModel.GroupCount; ++i)
					{
						// Extract the groups as well
						string modelGroupPath = $"{modelPathWithoutExtension}_{i:D3}.wmo";

						try
						{
							byte[] modelGroupData = fileReference.PackageGroup.ExtractFile(modelGroupPath);

							if (modelGroupData != null)
							{
								worldModel.AddModelGroup(new ModelGroup(modelGroupData));
							}
						}
						catch (InvalidFileSectorTableException fex)
						{
							Log.Warn(
								$"Failed to load the model group \"{modelGroupPath}\" due to an invalid sector table (\"{fex.Message}\").");
							return null;
						}
					}

					return worldModel;
				}
			}
			catch (InvalidFileSectorTableException fex)
			{
				Log.Warn(
					$"Failed to load the model \"{fileReference.FilePath}\" due to an invalid sector table (\"{fex.Message}\").");
				return null;
			}

			Log.Warn(
				$"Failed to load the model \"{fileReference.FilePath}\". The file data could not be extracted.");
			return null;
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

			// Extract it and load just this model group
			try
			{
				byte[] fileData = fileReference.PackageGroup.ExtractFile(modelRootPath);
				if (fileData != null)
				{
					WMO worldModel = new WMO(fileData);
					byte[] modelGroupData = fileReference.Extract();

					if (modelGroupData != null)
					{
						worldModel.AddModelGroup(new ModelGroup(modelGroupData));
					}

					return worldModel;
				}
			}
			catch (InvalidFileSectorTableException fex)
			{
				Log.Warn(
					$"Failed to load the model group \"{fileReference.FilePath}\" due to an invalid sector table (\"{fex.Message}\").");
				return null;
			}

			Log.Warn(
				$"Failed to load the model group \"{fileReference.FilePath}\". The file data could not be extracted.");
			return null;
		}

		/// <summary>
		/// Creates a renderable object from the specified WMO object, and the specified package group it belongs to.
		/// NOTE: This method *must* be called in the UI thread after the OpenGL context has been made current.
		/// The <see cref="IRenderable"/> constructors commonly make extensive use of OpenGL methods.
		/// </summary>
		/// <param name="worldModel">The model object.</param>
		/// <param name="fileReference">The reference it was constructed from.</param>
		/// <param name="version">The contextually relevant version.</param>
		/// <returns>An encapsulated renderable OpenGL object.</returns>
		public static IRenderable CreateRenderableWorldModel(WMO worldModel, FileReference fileReference, WarcraftVersion version)
		{
			if (worldModel == null)
			{
				return null;
			}

			RenderableWorldModel renderableWorldModel = new RenderableWorldModel(worldModel, fileReference.PackageGroup, version);
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

			byte[] fileData = fileReference.Extract();
			if (fileData != null)
			{
				try
				{
					return new BLP(fileData);
				}
				catch (FileLoadException fex)
				{
					Log.Warn($"FileLoadException when loading BLP image: {fex.Message}\n" +
							 $"Please report this on GitHub or via email.");
				}
			}

			Log.Warn(
				$"Failed to load the image \"{fileReference.FilePath}\". The file data could not be extracted.");
			return null;
		}

		/// <summary>
		/// Creates a renderable object from the specified BLP object, aand the specified <see cref="FileReference"/>
		/// it is associated with.
		/// NOTE: This method *must* be called in the UI thread after the OpenGL context has been made current.
		/// The <see cref="IRenderable"/> constructors commonly make extensive use of OpenGL methods.
		/// </summary>
		/// <param name="binaryImage">The image object.</param>
		/// <param name="fileReference">The reference it was constructed from.</param>
		/// <param name="version">Unused.</param>
		/// <returns>An encapsulated renderable OpenGL object.</returns>
		public static IRenderable CreateRenderableBinaryImage(BLP binaryImage, FileReference fileReference, WarcraftVersion version)
		{
			if (binaryImage == null)
			{
				return null;
			}

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

			byte[] fileData = fileReference.Extract();
			if (fileData != null)
			{
				try
				{
					using (MemoryStream ms = new MemoryStream(fileData))
					{
						return new Bitmap(ms);
					}
				}
				catch (FileLoadException fex)
				{
					Log.Warn($"FileLoadException when loading bitmap image: {fex.Message}\n" +
							 $"Please report this on GitHub or via email.");
				}
			}

			Log.Warn(
				$"Failed to load the image \"{fileReference.FilePath}\". The file data could not be extracted.");
			return null;
		}

		/// <summary>
		/// Creates a renderable object from the specified <see cref="Bitmap"/> object, and the specified
		/// <see cref="FileReference"/> it is associated with.
		/// NOTE: This method *must* be called in the UI thread after the OpenGL context has been made current.
		/// The <see cref="IRenderable"/> constructors commonly make extensive use of OpenGL methods.
		/// </summary>
		/// <param name="bitmapImage">The image object.</param>
		/// <param name="fileReference">The reference it was constructed from.</param>
		/// <param name="version">Unused.</param>
		/// <returns>An encapsulated renderable OpenGL object.</returns>
		public static IRenderable CreateRenderableBitmapImage(Bitmap bitmapImage, FileReference fileReference, WarcraftVersion version)
		{
			if (bitmapImage == null)
			{
				return null;
			}

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

			try
			{
				byte[] fileData = fileReference.Extract();
				if (fileData != null)
				{
					return new MDX(fileData);
				}
			}
			catch (InvalidFileSectorTableException fex)
			{
				Log.Warn($"Failed to load the model \"{fileReference.FilePath}\" due to an invalid sector table (\"{fex.Message}\").");
				return null;
			}

			Log.Warn($"Failed to load the model \"{fileReference.FilePath}\". The file data could not be extracted.");
			return null;
		}

		/// <summary>
		/// Creates a renderable object from the specified <see cref="MDX"/> object, and the specified
		/// <see cref="FileReference"/> it is associated with.
		/// NOTE: This method *must* be called in the UI thread after the OpenGL context has been made current.
		/// The <see cref="IRenderable"/> constructors commonly make extensive use of OpenGL methods.
		/// </summary>
		/// <param name="gameModel">The model object.</param>
		/// <param name="fileReference">The reference it was constructed from.</param>
		/// <param name="version">The contextually relevant version.</param>
		/// <returns>An encapsulated renderable OpenGL object.</returns>
		public static IRenderable CreateRenderableGameModel(MDX gameModel, FileReference fileReference, WarcraftVersion version)
		{
			if (gameModel == null)
			{
				return null;
			}

			RenderableGameModel renderableModel = new RenderableGameModel(gameModel, fileReference.PackageGroup, version, fileReference.FilePath);

			return renderableModel;
		}
	}
}
