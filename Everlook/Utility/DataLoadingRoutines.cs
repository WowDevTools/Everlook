//
//  DataLoadingRoutines.cs
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

using System.Drawing;
using System.IO;
using Everlook.Explorer;
using Everlook.Viewport.Rendering;
using Everlook.Viewport.Rendering.Interfaces;
using log4net;
using Warcraft.BLP;
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
			byte[] fileData = fileReference.Extract();
			if (fileData != null)
			{
				WMO worldModel = new WMO(fileData);

				string modelPathWithoutExtension = Path.GetFileNameWithoutExtension(fileReference.FilePath);
				for (int i = 0; i < worldModel.GroupCount; ++i)
				{
					// Extract the groups as well
					string modelGroupPath = $"{modelPathWithoutExtension}_{i:D3}.wmo";
					byte[] modelGroupData = fileReference.PackageGroup.ExtractFile(modelGroupPath);

					if (modelGroupData != null)
					{
						worldModel.AddModelGroup(new ModelGroup(modelGroupData));
					}
				}

				return worldModel;
			}

			return null;
		}

		/// <summary>
		/// Loads the specified WMO group file from the archives and deserialize it.
		/// </summary>
		/// <param name="fileReference">The archive reference to the model group.</param>
		/// <returns>A WMO object, containing just the specified model group.</returns>
		public static WMO LoadWorldModelGroup(FileReference fileReference)
		{
			// Get the file name of the root object
			string modelRootPath = fileReference.FilePath.Remove(fileReference.FilePath.Length - 8, 4);

			// Extract it and load just this model group
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

			return null;
		}

		/// <summary>
		/// Creates a renderable object from the specified WMO object, and the specified package group it belongs to.
		/// NOTE: This method *must* be called in the UI thread after the OpenGL context has been made current.
		///
		/// The <see cref="IRenderable"/> constructors commonly make extensive use of OpenGL methods.
		/// </summary>
		/// <param name="worldModel">The model object.</param>
		/// <param name="fileReference">The package group it belongs to.</param>
		/// <returns>An encapsulated renderable OpenGL object.</returns>
		public static IRenderable CreateRenderableWorldModel(WMO worldModel, FileReference fileReference)
		{
			if (worldModel == null)
			{
				return null;
			}

			IRenderable renderableWorldModel = new RenderableWorldModel(worldModel, fileReference.PackageGroup);
			return renderableWorldModel;
		}

		/// <summary>
		/// Loads the specified BLP image from the archives and deserialize it.
		/// </summary>
		/// <param name="fileReference">A reference to a BLP image.</param>
		/// <returns></returns>
		public static BLP LoadBinaryImage(FileReference fileReference)
		{
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

			return null;
		}

		/// <summary>
		/// Creates a renderable object from the specified BLP object, aand the specified <see cref="FileReference"/>
		/// it is associated with.
		/// NOTE: This method *must* be called in the UI thread after the OpenGL context has been made current.
		///
		/// The <see cref="IRenderable"/> constructors commonly make extensive use of OpenGL methods.
		/// </summary>
		/// <param name="binaryImage">The image object.</param>
		/// <param name="fileReference">The package group it belongs to.</param>
		/// <returns>An encapsulated renderable OpenGL object.</returns>
		public static IRenderable CreateRenderableBinaryImage(BLP binaryImage, FileReference fileReference)
		{
			if (binaryImage == null)
			{
				return null;
			}

			RenderableBLP renderableImage = new RenderableBLP(binaryImage, fileReference.FilePath);
			return renderableImage;
		}

		/// <summary>
		/// Loads the specified BLP image from the archives and deserialize it.
		/// </summary>
		/// <param name="fileReference">A reference to a BLP image.</param>
		/// <returns></returns>
		public static Bitmap LoadBitmapImage(FileReference fileReference)
		{
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

			return null;
		}

		/// <summary>
		/// Creates a renderable object from the specified <see cref="Bitmap"/> object, and the specified
		/// <see cref="FileReference"/> it is associated with.
		/// NOTE: This method *must* be called in the UI thread after the OpenGL context has been made current.
		///
		/// The <see cref="IRenderable"/> constructors commonly make extensive use of OpenGL methods.
		/// </summary>
		/// <param name="bitmapImage">The image object.</param>
		/// <param name="fileReference">The package group it belongs to.</param>
		/// <returns>An encapsulated renderable OpenGL object.</returns>
		public static IRenderable CreateRenderableBitmapImage(Bitmap bitmapImage, FileReference fileReference)
		{
			if (bitmapImage == null)
			{
				return null;
			}

			RenderableBitmap renderableImage = new RenderableBitmap(bitmapImage, fileReference.FilePath);
			return renderableImage;
		}
	}
}