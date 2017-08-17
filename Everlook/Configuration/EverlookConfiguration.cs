//
//  EverlookConfiguration.cs
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
using System.IO;
using Everlook.Export.Audio;
using Everlook.Export.Image;
using Everlook.Export.Model;
using Gdk;
using IniParser;
using IniParser.Model;

namespace Everlook.Configuration
{
	/// <summary>
	/// Everlook configuration handler. Reads and writes local user configuration of the application.
	/// This class is threadsafe.
	/// </summary>
	public class EverlookConfiguration
	{
		/*
			Section names
		*/

		private const string General = nameof(General);
		private const string Export = nameof(Export);
		private const string Privacy = nameof(Privacy);
		private const string Model = nameof(Model);
		private const string Explorer = nameof(Explorer);

		/*
			Key names
		*/

		/*
			General
		*/

		private const string ViewportBackgroundColour = nameof(ViewportBackgroundColour);

		/*
			Export
		*/

		private const string DefaultExportDirectory = nameof(DefaultExportDirectory);
		private const string DefaultExportModelFormat = nameof(DefaultExportModelFormat);
		private const string DefaultExportImageFormat = nameof(DefaultExportImageFormat);
		private const string DefaultExportAudioFormat = nameof(DefaultExportAudioFormat);
		private const string KeepFileDirectoryStructure = nameof(KeepFileDirectoryStructure);

		/*
			Privacy
		*/

		private const string AllowSendAnonymousStats = nameof(AllowSendAnonymousStats);
		private const string SendMachineID = nameof(SendMachineID);
		private const string SendInstallID = nameof(SendInstallID);
		private const string SendOS = nameof(SendOS);
		private const string SendAppVersion = nameof(SendAppVersion);
		private const string SendRuntimeInfo = nameof(SendRuntimeInfo);

		/*
			Models
		*/

		private const string WireframeColour = nameof(WireframeColour);
		private const string OccludeBoundingBoxes = nameof(OccludeBoundingBoxes);

		/*
			Explorer
		*/

		private const string ShowUnknownFilesWhenFiltering = nameof(ShowUnknownFilesWhenFiltering);
		private const string AutoplayAudioFiles = nameof(AutoplayAudioFiles);

		/// <summary>
		/// The publicly accessibly instance of the configuration.
		/// </summary>
		public static readonly EverlookConfiguration Instance = new EverlookConfiguration();

		private readonly object ReadLock = new object();
		private readonly object WriteLock = new object();

		private EverlookConfiguration()
		{
			FileIniDataParser parser = new FileIniDataParser();

			lock (this.ReadLock)
			{
				if (!File.Exists(GetConfigurationFilePath()))
				{
					// Create the default configuration file
					Directory.CreateDirectory(Directory.GetParent(GetConfigurationFilePath()).FullName);
					File.Create(GetConfigurationFilePath()).Close();
					IniData data = parser.ReadFile(GetConfigurationFilePath());

					data.Sections.AddSection(General);
					data.Sections.AddSection(Export);
					data.Sections.AddSection(Privacy);
					data.Sections.AddSection(Model);
					data.Sections.AddSection(Explorer);

					data[General].AddKey(ViewportBackgroundColour, "rgb(133, 146, 173)");

					data[Export].AddKey(DefaultExportDirectory, Export);

					KeyData modelExportKeyData = new KeyData(DefaultExportModelFormat)
					{
						Value = "0"
					};

					List<string> modelExportKeyComments = new List<string>
					{
						"Valid options: ",
						"0: Collada",
						"1: Wavefront OBJ"
					};
					modelExportKeyData.Comments = modelExportKeyComments;

					data[Export].AddKey(modelExportKeyData);

					KeyData imageExportKeyData = new KeyData(DefaultExportImageFormat)
					{
						Value = "0"
					};

					List<string> imageExportKeyComments = new List<string>
					{
						"Valid options: ",
						"0: PNG",
						"1: JPG",
						"2: TGA",
						"3: TIF",
						"4: BMP"
					};
					imageExportKeyData.Comments = imageExportKeyComments;

					data[Export].AddKey(imageExportKeyData);

					KeyData audioExportKeyData = new KeyData(DefaultExportAudioFormat)
					{
						Value = "0"
					};

					List<string> audioExportKeyComments = new List<string>
					{
						"Valid options: ",
						"0: WAV",
						"1: MP3",
						"2: OGG",
						"3: FLAC"
					};
					audioExportKeyData.Comments = audioExportKeyComments;

					data[Export].AddKey(audioExportKeyData);

					data[Export].AddKey(KeepFileDirectoryStructure, "false");

					data[Privacy].AddKey(AllowSendAnonymousStats, "false");
					data[Privacy].AddKey(SendMachineID, "true");
					data[Privacy].AddKey(SendInstallID, "true");
					data[Privacy].AddKey(SendOS, "true");
					data[Privacy].AddKey(SendAppVersion, "true");
					data[Privacy].AddKey(SendRuntimeInfo, "true");

					data[Model].AddKey(WireframeColour, "rgb(234, 161, 0)");
					data[Model].AddKey(OccludeBoundingBoxes, "false");

					data[Explorer].AddKey(ShowUnknownFilesWhenFiltering, "true");
					data[Explorer].AddKey(AutoplayAudioFiles, "true");

					lock (this.WriteLock)
					{
						WriteConfig(parser, data);
					}
				}
				else
				{
					/*
						This section is for updating old configuration files
						with new sections introduced in updates.

						It's good practice to wrap each updating section in a
						small informational header with the date and change.
					*/

					// Uncomment this when needed
					IniData data = parser.ReadFile(GetConfigurationFilePath());

					// May 4th, 2016 - Added option to show unknown file types in the tree view.
					// June 13t, 2017 - Removed hungarian prefix.
					AddNewConfigurationOption(data, General, ShowUnknownFilesWhenFiltering, "true");

					// May 4th, 2016 - Implemented support for multiple games at once.
					// Makes the GameDirectory option obsolete.
					if (data[General].ContainsKey("GameDirectory"))
					{
						// May 15th, 2017 - Added path aliasing, breaking support for path import
						// GamePathStorage.Instance.StorePath(data[General]["GameDirectory"]);
						data[General].RemoveKey("GameDirectory");
					}

					// June 13, 2017 - Added model configuration and wireframe colour
					AddNewConfigurationSection(data, Model);
					AddNewConfigurationOption(data, Model, WireframeColour, "rgb(234, 161, 0)");

					// June 13, 2017 - Removed hungarian prefix.
					RenameConfigurationOption(data, General, "bShowUnknownFilesWhenFiltering", ShowUnknownFilesWhenFiltering);

					// August 17, 2016 - Add extended privacy options, bounding box occlusion, and audio autoplay
					AddNewConfigurationSection(data, Explorer);
					MoveConfigurationOption(data, General, Explorer, ShowUnknownFilesWhenFiltering);
					AddNewConfigurationOption(data, Explorer, AutoplayAudioFiles, "true");

					AddNewConfigurationOption(data, Privacy, SendMachineID, "true");
					AddNewConfigurationOption(data, Privacy, SendInstallID, "true");
					AddNewConfigurationOption(data, Privacy, SendOS, "true");
					AddNewConfigurationOption(data, Privacy, SendAppVersion, "true");
					AddNewConfigurationOption(data, Privacy, SendRuntimeInfo, "true");

					AddNewConfigurationOption(data, Model, OccludeBoundingBoxes, "false");

					lock (this.WriteLock)
					{
						WriteConfig(parser, data);
					}
				}
			}
		}

		private void AddNewConfigurationSection(IniData configData, string keySection)
		{
			if (!configData.Sections.ContainsSection(keySection))
			{
				configData.Sections.AddSection(keySection);
			}
		}

		private void AddNewConfigurationOption(IniData configData, string keySection, string keyName, string keyData)
		{
			if (!configData.Sections[keySection].ContainsKey(keyName))
			{
				configData.Sections[keySection].AddKey(keyName);
				configData.Sections[keySection][keyName] = keyData;
			}
		}

		private void RenameConfigurationOption(IniData configData, string keySection, string oldKeyName, string newKeyName)
		{
			if (configData.Sections[keySection].ContainsKey(oldKeyName))
			{
				string oldKeyData = configData.Sections[keySection][oldKeyName];

				AddNewConfigurationOption(configData, keySection, newKeyName, oldKeyData);

				configData.Sections[keySection].RemoveKey(oldKeyName);
			}
		}

		private void RenameConfigurationSection(IniData configData, string oldSectionName, string newSectionName)
		{
			if (configData.Sections.ContainsSection(oldSectionName))
			{
				SectionData oldSectionData = configData.Sections.GetSectionData(oldSectionName);
				configData.Sections.RemoveSection(oldSectionName);
				configData.Sections.AddSection(newSectionName);
				configData.Sections.SetSectionData(newSectionName, oldSectionData);
			}
		}

		private void MoveConfigurationOption(IniData configData, string oldKeySection, string newKeySection, string keyName)
		{
			if (!configData.Sections[newKeySection].ContainsKey(keyName))
			{
				string keyValue = configData.Sections[oldKeySection][keyName];
				configData.Sections[newKeySection].AddKey(keyName, keyValue);

				configData.Sections[oldKeySection].RemoveKey(keyName);
			}
		}

		/// <summary>
		/// Gets a value indicating whether to display unknown files when filtering the explorer tree.
		/// </summary>
		/// <returns>true if unknown files should be shown; false otherwise.</returns>
		public bool GetShowUnknownFilesWhenFiltering()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				bool showUnknown;
				if (bool.TryParse(data[Explorer][ShowUnknownFilesWhenFiltering], out showUnknown))
				{
					return showUnknown;
				}

				return showUnknown;
			}
		}

		/// <summary>
		/// Sets a value indicating whether to display unknown files when filtering the explorer tree.
		/// </summary>
		/// <param name="showUnknown">Whether or not to show unknown files.</param>
		public void SetShowUnknownFilesWhenFiltering(bool showUnknown)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Explorer][ShowUnknownFilesWhenFiltering] = showUnknown.ToString().ToLower();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether to send the machine ID when sending statistics.
		/// </summary>
		/// <returns>true if the machine ID should be sent; false otherwise.</returns>
		public bool GetSendMachineID()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				bool sendData;
				if (bool.TryParse(data[Privacy][SendMachineID], out sendData))
				{
					return sendData;
				}

				return sendData;
			}
		}

		/// <summary>
		/// Sets a value indicating whether to send the machine ID when sending statistics.
		/// </summary>
		/// <param name="sendData">Whether or not to send the data.</param>
		public void SetSendMachineID(bool sendData)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Privacy][SendMachineID] = sendData.ToString().ToLower();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether to send the installation ID when sending statistics.
		/// </summary>
		/// <returns>true if the installation ID should be sent; false otherwise.</returns>
		public bool GetSendInstallID()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				bool sendData;
				if (bool.TryParse(data[Privacy][SendInstallID], out sendData))
				{
					return sendData;
				}

				return sendData;
			}
		}

		/// <summary>
		/// Sets a value indicating whether to send the installation ID when sending statistics.
		/// </summary>
		/// <param name="sendData">Whether or not to send the data.</param>
		public void SetSendInstallID(bool sendData)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Privacy][SendInstallID] = sendData.ToString().ToLower();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether to send the operating system when sending statistics.
		/// </summary>
		/// <returns>true if the operating system should be sent; false otherwise.</returns>
		public bool GetSendOS()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				bool sendData;
				if (bool.TryParse(data[Privacy][SendOS], out sendData))
				{
					return sendData;
				}

				return sendData;
			}
		}

		/// <summary>
		/// Sets a value indicating whether to send the operating system when sending statistics.
		/// </summary>
		/// <param name="sendData">Whether or not to send the data.</param>
		public void SetSendOS(bool sendData)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Privacy][SendOS] = sendData.ToString().ToLower();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether to send the application version when sending statistics.
		/// </summary>
		/// <returns>true if the application version should be sent; false otherwise.</returns>
		public bool GetSendAppVersion()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				bool sendData;
				if (bool.TryParse(data[Privacy][SendAppVersion], out sendData))
				{
					return sendData;
				}

				return sendData;
			}
		}

		/// <summary>
		/// Sets a value indicating whether to send the application version when sending statistics.
		/// </summary>
		/// <param name="sendData">Whether or not to send the data.</param>
		public void SetSendAppVersion(bool sendData)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Privacy][SendAppVersion] = sendData.ToString().ToLower();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Sets a value indicating whether to send information about the runtime when sending statistics.
		/// </summary>
		/// <param name="sendData">Whether or not to send the data.</param>
		public void SetSendRuntimeInfo(bool sendData)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Privacy][SendRuntimeInfo] = sendData.ToString().ToLower();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether to send information about the runtime when sending statistics.
		/// </summary>
		/// <returns>true if the application version should be sent; false otherwise.</returns>
		public bool GetSendRuntimeInfo()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				bool sendData;
				if (bool.TryParse(data[Privacy][SendRuntimeInfo], out sendData))
				{
					return sendData;
				}

				return sendData;
			}
		}

		/// <summary>
		/// Sets a value indicating whether bounding boxes should be occluded by geometry.
		/// </summary>
		/// <param name="occlude">Whether or not to occlude bounding boxes.</param>
		public void SetOccludeBoundingBoxes(bool occlude)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Model][OccludeBoundingBoxes] = occlude.ToString().ToLower();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether bounding boxes should be occluded by geometry.
		/// </summary>
		/// <returns>true if boxes should be occluded; false otherwise.</returns>
		public bool GetOccludeBoundingBoxes()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				bool occlude;
				if (bool.TryParse(data[Model][OccludeBoundingBoxes], out occlude))
				{
					return occlude;
				}

				return occlude;
			}
		}

		/// <summary>
		/// Sets a value indicating whether to automatically play audio files when selected.
		/// </summary>
		/// <param name="autoplay">Whether or not to automatically play audio.</param>
		public void SetAutoplayAudio(bool autoplay)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Explorer][AutoplayAudioFiles] = autoplay.ToString().ToLower();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether to automatically play audio files when selected.
		/// </summary>
		/// <returns>true if audio should be automatically played; false otherwise.</returns>
		public bool GetAutoplayAudio()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				bool autoplay;
				if (bool.TryParse(data[Explorer][AutoplayAudioFiles], out autoplay))
				{
					return autoplay;
				}

				return autoplay;
			}
		}

		/// <summary>
		/// Gets the viewport background colour.
		/// </summary>
		/// <returns>The viewport background colour.</returns>
		public RGBA GetViewportBackgroundColour()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				RGBA viewportBackgroundColour = default(RGBA);
				if (viewportBackgroundColour.Parse(data[General][ViewportBackgroundColour]))
				{
					return viewportBackgroundColour;
				}

				viewportBackgroundColour.Parse("rgb(133, 146, 173)");
				return viewportBackgroundColour;
			}
		}

		/// <summary>
		/// Sets the viewport background colour.
		/// </summary>
		/// <param name="colour">Colour.</param>
		public void SetViewportBackgroundColour(RGBA colour)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[General][ViewportBackgroundColour] = colour.ToString();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets the wireframe colour for models.
		/// </summary>
		/// <returns>The colour of the wireframe.</returns>
		public RGBA GetWireframeColour()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				RGBA wireframeColour = default(RGBA);
				if (wireframeColour.Parse(data[Model][WireframeColour]))
				{
					return wireframeColour;
				}

				wireframeColour.Parse("rgb(234, 161, 0)");
				return wireframeColour;
			}
		}

		/// <summary>
		/// Sets the wireframe colour for models.
		/// </summary>
		/// <param name="colour">Colour.</param>
		public void SetWireframeColour(RGBA colour)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Model][WireframeColour] = colour.ToString();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets the default export directory.
		/// </summary>
		/// <returns>The default export directory.</returns>
		public string GetDefaultExportDirectory()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				string path = data[Export][DefaultExportDirectory];
				if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
				{
					path += Path.DirectorySeparatorChar;
				}

				return path;
			}
		}

		/// <summary>
		/// Sets the default export directory.
		/// </summary>
		/// <param name="exportDirectory">Export directory.</param>
		public void SetDefaultExportDirectory(string exportDirectory)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Export][DefaultExportDirectory] = exportDirectory;

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets the default model format.
		/// </summary>
		/// <returns>The default model format.</returns>
		public ModelFormat GetDefaultModelFormat()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				int modelFormat;
				if (int.TryParse(data[Export][DefaultExportModelFormat], out modelFormat))
				{
					return (ModelFormat)modelFormat;
				}
				return ModelFormat.Collada;
			}
		}

		/// <summary>
		/// Sets the default model format.
		/// </summary>
		/// <param name="modelFormat">Model format.</param>
		public void SetDefaultModelFormat(ModelFormat modelFormat)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Export][DefaultExportModelFormat] = ((int)modelFormat).ToString();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets the default image format.
		/// </summary>
		/// <returns>The default image format.</returns>
		public ImageFormat GetDefaultImageFormat()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				int imageFormat;
				if (int.TryParse(data[Export][DefaultExportImageFormat], out imageFormat))
				{
					return (ImageFormat)imageFormat;
				}
				return ImageFormat.PNG;
			}
		}

		/// <summary>
		/// Sets the default image format.
		/// </summary>
		/// <param name="imageFormat">Image format.</param>
		public void SetDefaultImageFormat(ImageFormat imageFormat)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Export][DefaultExportImageFormat] = ((int)imageFormat).ToString();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets the default audio format.
		/// </summary>
		/// <returns>The default audio format.</returns>
		public AudioFormat GetDefaultAudioFormat()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				int audioFormat;
				if (int.TryParse(data[Export][DefaultExportAudioFormat], out audioFormat))
				{
					return (AudioFormat)audioFormat;
				}
				return AudioFormat.WAV;
			}
		}

		/// <summary>
		/// Sets the default audio format.
		/// </summary>
		/// <param name="audioFormat">Audio format.</param>
		public void SetDefaultAudioFormat(AudioFormat audioFormat)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Export][DefaultExportAudioFormat] = ((int)audioFormat).ToString();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets the should keep file directory structure.
		/// </summary>
		/// <returns><c>true</c>, if should keep file directory structure was gotten, <c>false</c> otherwise.</returns>
		public bool GetShouldKeepFileDirectoryStructure()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				bool keepDirectoryStructure;
				if (bool.TryParse(data[Export][KeepFileDirectoryStructure], out keepDirectoryStructure))
				{
					return keepDirectoryStructure;
				}
				return false;
			}
		}

		/// <summary>
		/// Sets the keep file directory structure.
		/// </summary>
		/// <param name="keepStructure">If set to <c>true</c> keep structure.</param>
		public void SetKeepFileDirectoryStructure(bool keepStructure)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Export][KeepFileDirectoryStructure] = keepStructure.ToString().ToLower();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Gets whether or not the application should be allowed to send anonymous stats.
		/// </summary>
		/// <returns><c>true</c>, if sending anonymous stats is allowed, <c>false</c> otherwise.</returns>
		public bool GetAllowSendAnonymousStats()
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				bool allowSendAnonymousStats;
				if (bool.TryParse(data[Privacy][AllowSendAnonymousStats], out allowSendAnonymousStats))
				{
					return allowSendAnonymousStats;
				}
				return true;
			}
		}

		/// <summary>
		/// Sets whether or not the application should be allowed to send anonymous stats.
		/// </summary>
		/// <param name="allowSendAnonymousStats">If set to <c>true</c> allow sending of anonymous stats.</param>
		public void SetAllowSendAnonymousStats(bool allowSendAnonymousStats)
		{
			lock (this.ReadLock)
			{
				FileIniDataParser parser = new FileIniDataParser();
				IniData data = parser.ReadFile(GetConfigurationFilePath());

				data[Privacy][AllowSendAnonymousStats] = allowSendAnonymousStats.ToString().ToLower();

				lock (this.WriteLock)
				{
					WriteConfig(parser, data);
				}
			}
		}

		/// <summary>
		/// Writes the config data to disk. This method is thread-blocking, and all write operations
		/// are synchronized via lock(WriteLock).
		/// </summary>
		/// <param name="dataParser">The parser dealing with the current data.</param>
		/// <param name="data">The data which should be written to file.</param>
		private static void WriteConfig(FileIniDataParser dataParser, IniData data)
		{
			dataParser.WriteFile(GetConfigurationFilePath(), data);
		}

		private static string GetConfigurationFilePath()
		{
			return
				$"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}{Path.DirectorySeparatorChar}" +
				$"Everlook{Path.DirectorySeparatorChar}everlook.ini";
		}
	}
}
