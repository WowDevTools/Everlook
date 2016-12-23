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
using System.IO;
using IniParser;
using IniParser.Model;
using System.Collections.Generic;
using Everlook.Export.Audio;
using Everlook.Export.Image;
using Everlook.Export.Model;
using Gdk;

namespace Everlook.Configuration
{
	/// <summary>
	/// Everlook configuration handler. Reads and writes local user configuration of the application.
	/// This class is threadsafe.
	/// </summary>
	public class EverlookConfiguration
	{
		/// <summary>
		/// The publicly accessibly instance of the configuration.
		/// </summary>
		public static EverlookConfiguration Instance = new EverlookConfiguration();

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

					data.Sections.AddSection("General");
					data.Sections.AddSection("Export");
					data.Sections.AddSection("Privacy");

					data["General"].AddKey("ViewportBackgroundColour", "rgb(133, 146, 173)");
					data["General"].AddKey("bShowUnknownFilesWhenFiltering", "true");

					data["Export"].AddKey("DefaultExportDirectory", "./Export");

					KeyData modelExportKeyData = new KeyData("DefaultExportModelFormat")
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

					data["Export"].AddKey(modelExportKeyData);

					KeyData imageExportKeyData = new KeyData("DefaultExportImageFormat")
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

					data["Export"].AddKey(imageExportKeyData);

					KeyData audioExportKeyData = new KeyData("DefaultExportAudioFormat")
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

					data["Export"].AddKey(audioExportKeyData);

					data["Export"].AddKey("KeepFileDirectoryStructure", "false");

					data["Privacy"].AddKey("AllowSendAnonymousStats", "true");

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
					AddNewConfigurationOption(data, "General", "bShowUnknownFilesWhenFiltering", "true");

					// May 4th, 2016 - Implemented support for multiple games at once.
					// Makes the GameDirectory option obsolete.
					if (data["General"].ContainsKey("GameDirectory"))
					{
						GamePathStorage.Instance.StorePath(data["General"]["GameDirectory"]);
						data["General"].RemoveKey("GameDirectory");
					}

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
			}

			if (!configData.Sections[keySection][keyName].Equals(keyData))
			{
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

				RGBA viewportBackgroundColour = new RGBA();
				if (viewportBackgroundColour.Parse(data["General"]["ViewportBackgroundColour"]))
				{
					return viewportBackgroundColour;
				}
				else
				{
					viewportBackgroundColour.Parse("rgb(133, 146, 173)");
					return viewportBackgroundColour;
				}
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

				data["General"]["ViewportBackgroundColour"] = colour.ToString();

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

				string path = data["Export"]["DefaultExportDirectory"];
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

				data["Export"]["DefaultExportDirectory"] = exportDirectory;

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
				if (int.TryParse(data["Export"]["DefaultExportModelFormat"], out modelFormat))
				{
					return (ModelFormat)modelFormat;
				}
				else
				{
					return ModelFormat.Collada;
				}
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

				data["Export"]["DefaultExportModelFormat"] = ((int)modelFormat).ToString();

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
				if (int.TryParse(data["Export"]["DefaultExportImageFormat"], out imageFormat))
				{
					return (ImageFormat)imageFormat;
				}
				else
				{
					return ImageFormat.PNG;
				}
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

				data["Export"]["DefaultExportImageFormat"] = ((int)imageFormat).ToString();

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
				if (int.TryParse(data["Export"]["DefaultExportAudioFormat"], out audioFormat))
				{
					return (AudioFormat)audioFormat;
				}
				else
				{
					return AudioFormat.WAV;
				}
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

				data["Export"]["DefaultExportAudioFormat"] = ((int)audioFormat).ToString();

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
				if (bool.TryParse(data["Export"]["KeepFileDirectoryStructure"], out keepDirectoryStructure))
				{
					return keepDirectoryStructure;
				}
				else
				{
					return false;
				}
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

				data["Export"]["KeepFileDirectoryStructure"] = keepStructure.ToString().ToLower();

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
				if (bool.TryParse(data["Privacy"]["AllowSendAnonymousStats"], out allowSendAnonymousStats))
				{
					return allowSendAnonymousStats;
				}
				else
				{
					return true;
				}
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

				data["Privacy"]["AllowSendAnonymousStats"] = allowSendAnonymousStats.ToString().ToLower();

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

