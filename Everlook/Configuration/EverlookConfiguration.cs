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
			FileIniDataParser Parser = new FileIniDataParser();

			lock (ReadLock)
			{
				if (!File.Exists(GetConfigurationFilePath()))
				{
					// Create the default configuration file

					Directory.CreateDirectory(Directory.GetParent(GetConfigurationFilePath()).FullName);
					File.Create(GetConfigurationFilePath()).Close();
					IniData data = Parser.ReadFile(GetConfigurationFilePath());

					data.Sections.AddSection("General");
					data.Sections.AddSection("Export");
					data.Sections.AddSection("Privacy");

					data["General"].AddKey("ViewportBackgroundColour", "rgb(133, 146, 173)");
					data["General"].AddKey("bShowUnknownFilesWhenFiltering", "true");

					data["Export"].AddKey("DefaultExportDirectory", "./Export");

					KeyData ModelExportKeyData = new KeyData("DefaultExportModelFormat");
					ModelExportKeyData.Value = "0";

					List<string> ModelExportKeyComments = new List<string>();
					ModelExportKeyComments.Add("Valid options: ");
					ModelExportKeyComments.Add("0: Collada");
					ModelExportKeyComments.Add("1: Wavefront OBJ");
					ModelExportKeyData.Comments = ModelExportKeyComments;

					data["Export"].AddKey(ModelExportKeyData);

					KeyData ImageExportKeyData = new KeyData("DefaultExportImageFormat");
					ImageExportKeyData.Value = "0";

					List<string> ImageExportKeyComments = new List<string>();
					ImageExportKeyComments.Add("Valid options: ");
					ImageExportKeyComments.Add("0: PNG");
					ImageExportKeyComments.Add("1: JPG");
					ImageExportKeyComments.Add("2: TGA");
					ImageExportKeyComments.Add("3: TIF");
					ImageExportKeyComments.Add("4: BMP");
					ImageExportKeyData.Comments = ImageExportKeyComments;

					data["Export"].AddKey(ImageExportKeyData);

					KeyData AudioExportKeyData = new KeyData("DefaultExportAudioFormat");
					AudioExportKeyData.Value = "0";

					List<string> AudioExportKeyComments = new List<string>();
					AudioExportKeyComments.Add("Valid options: ");
					AudioExportKeyComments.Add("0: WAV");
					AudioExportKeyComments.Add("1: MP3");
					AudioExportKeyComments.Add("2: OGG");
					AudioExportKeyComments.Add("3: FLAC");
					AudioExportKeyData.Comments = AudioExportKeyComments;

					data["Export"].AddKey(AudioExportKeyData);

					data["Export"].AddKey("KeepFileDirectoryStructure", "false");

					data["Privacy"].AddKey("AllowSendAnonymousStats", "true");

					lock (WriteLock)
					{
						WriteConfig(Parser, data);
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
					IniData data = Parser.ReadFile(GetConfigurationFilePath());

					// May 4th, 2016 - Added option to show unknown file types in the tree view.
					AddNewConfigurationOption(data, "General", "bShowUnknownFilesWhenFiltering", "true");

					// May 4th, 2016 - Implemented support for multiple games at once.
					// Makes the GameDirectory option obsolete.
					if (data["General"].ContainsKey("GameDirectory"))
					{
						GamePathStorage.Instance.StorePath(data["General"]["GameDirectory"]);
						data["General"].RemoveKey("GameDirectory");
					}

					lock (WriteLock)
					{
						WriteConfig(Parser, data);
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
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

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
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["General"]["ViewportBackgroundColour"] = colour.ToString();

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		/// <summary>
		/// Gets the default export directory.
		/// </summary>
		/// <returns>The default export directory.</returns>
		public string GetDefaultExportDirectory()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

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
		/// <param name="ExportDirectory">Export directory.</param>
		public void SetDefaultExportDirectory(string ExportDirectory)
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["Export"]["DefaultExportDirectory"] = ExportDirectory;

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		/// <summary>
		/// Gets the default model format.
		/// </summary>
		/// <returns>The default model format.</returns>
		public ModelFormat GetDefaultModelFormat()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

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
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["Export"]["DefaultExportModelFormat"] = ((int)modelFormat).ToString();

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		/// <summary>
		/// Gets the default image format.
		/// </summary>
		/// <returns>The default image format.</returns>
		public ImageFormat GetDefaultImageFormat()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

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
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["Export"]["DefaultExportImageFormat"] = ((int)imageFormat).ToString();

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		/// <summary>
		/// Gets the default audio format.
		/// </summary>
		/// <returns>The default audio format.</returns>
		public AudioFormat GetDefaultAudioFormat()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

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
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["Export"]["DefaultExportAudioFormat"] = ((int)audioFormat).ToString();

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		/// <summary>
		/// Gets the should keep file directory structure.
		/// </summary>
		/// <returns><c>true</c>, if should keep file directory structure was gotten, <c>false</c> otherwise.</returns>
		public bool GetShouldKeepFileDirectoryStructure()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

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
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["Export"]["KeepFileDirectoryStructure"] = keepStructure.ToString().ToLower();

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		/// <summary>
		/// Gets whether or not the application should be allowed to send anonymous stats.
		/// </summary>
		/// <returns><c>true</c>, if sending anonymous stats is allowed, <c>false</c> otherwise.</returns>
		public bool GetAllowSendAnonymousStats()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

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
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["Privacy"]["AllowSendAnonymousStats"] = allowSendAnonymousStats.ToString().ToLower();

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		/// <summary>
		/// Writes the config data to disk. This method is thread-blocking, and all write operations
		/// are synchronized via lock(WriteLock).
		/// </summary>
		/// <param name="Parser">The parser dealing with the current data.</param>
		/// <param name="Data">The data which should be written to file.</param>
		private static void WriteConfig(FileIniDataParser Parser, IniData Data)
		{
			Parser.WriteFile(GetConfigurationFilePath(), Data);
		}

		private static string GetConfigurationFilePath()
		{
			return
				$"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}{Path.DirectorySeparatorChar}" +
				$"Everlook{Path.DirectorySeparatorChar}everlook.ini";
		}
	}
}

