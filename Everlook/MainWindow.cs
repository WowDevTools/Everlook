//
//  MainWindow.cs
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
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;
using Gdk;
using System.Collections.Generic;
using Everlook.Configuration;
using Everlook.Viewport;
using Warcraft.MPQ;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

namespace Everlook
{
	/// <summary>
	/// Main UI class for Everlook. The "partial" qualifier is not strictly needed, but prevents the compiler from 
	/// generating errors about the autoconnected members that relate to UI elements.
	/// </summary>
	public partial class MainWindow: Gtk.Window
	{
		[UI] ToolButton AboutButton;
		[UI] AboutDialog AboutDialog;
		[UI] ToolButton PreferencesButton;

		[UI] DrawingArea MainDrawingArea;

		[UI] Menu FileContextMenu;
		[UI] ImageMenuItem ExtractItem;
		[UI] ImageMenuItem OpenItem;
		[UI] ImageMenuItem CopyItem;
		[UI] ImageMenuItem QueueItem;

		[UI] Menu QueueContextMenu;
		[UI] ImageMenuItem RemoveQueueItem;

		[UI] TreeView ExportQueueTreeView;
		[UI] ListStore ExportQueueListStore;

		[UI] TreeView GameExplorerTreeView;
		[UI] TreeStore GameExplorerTreeStore;

		/// <summary>
		/// The cached package directory. Used when the user changes game directory during runtime.
		/// </summary>
		private string CachedPackageDirectory;

		/// <summary>
		/// The package path mapping. Maps the package names to their paths on disk.
		/// </summary>
		private readonly Dictionary<string, string> PackagePathMapping = new Dictionary<string, string>();

		/// <summary>
		/// The package listfiles. 
		/// Key: The package name.
		/// Value: A list of all files present in the package.
		/// </summary>
		private readonly Dictionary<string, List<string>> PackageListfiles = new Dictionary<string, List<string>>();

		/// <summary>
		/// The package folder dictionary. Holds values in the following configuration:
		///	Key: Package Path.
		/// Value: Dictionary of folders and files in the package.
		/// Value.Key: Parent director Name.
		/// Value.Value: List of subfolders and files.
		/// </summary>
		private readonly Dictionary<string, List<string>> PackageSubfolderContent = new Dictionary<string, List<string>>();

		/// <summary>
		/// The package folder node mapping. Maps package names and paths to tree nodes.
		/// Key: Path of package, folder or file.
		/// Value: TreeIter that maps to the package, folder or file.
		/// </summary>
		private readonly Dictionary<string, TreeIter> PackageFolderNodeMapping = new Dictionary<string,TreeIter>();

		/// <summary>
		/// A path pointing to the currently selected item in the game explorer.
		/// </summary>
		private TreePath CurrentGameExplorerPath;

		/// <summary>
		/// Static reference to the configuration handler.
		/// </summary>
		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

		/// <summary>
		/// Background viewport renderer. Handles all rendering in the viewport.
		/// </summary>
		private readonly ViewportRenderer viewportRenderer = new ViewportRenderer();

		/// <summary>
		/// Creates an instance of the MainWindow class, loading the glade XML UI as needed.
		/// </summary>
		public static MainWindow Create()
		{
			Builder builder = new Builder(null, "Everlook.interfaces.Everlook.glade", null);
			return new MainWindow(builder, builder.GetObject("MainWindow").Handle);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.MainWindow"/> class.
		/// </summary>
		/// <param name="builder">Builder.</param>
		/// <param name="handle">Handle.</param>
		protected MainWindow(Builder builder, IntPtr handle)
			: base(handle)
		{
			builder.Autoconnect(this);
			DeleteEvent += OnDeleteEvent;

			AboutButton.Clicked += OnAboutButtonClicked;
			PreferencesButton.Clicked += OnPreferencesButtonClicked;

			MainDrawingArea.OverrideBackgroundColor(StateFlags.Normal, Config.GetViewportBackgroundColour());

			GameExplorerTreeView.RowExpanded += OnGameExplorerRowExpanded;
			GameExplorerTreeView.ButtonPressEvent += OnGameExplorerButtonPressed;

			ExportQueueTreeView.ButtonPressEvent += OnExportQueueButtonPressed;	

			ExtractItem.Activated += OnExtractContextItemActivated;
			OpenItem.Activated += OnOpenContextItemActivated;
			CopyItem.Activated += OnCopyContextItemActivated;
			QueueItem.Activated += OnQueueContextItemActivated;

			RemoveQueueItem.Activated += OnQueueRemoveContextItemActivated;

			viewportRenderer.FrameRendered += OnFrameRendered;
			viewportRenderer.Start();

			// Check game directory for packages
			LoadPackages();
		}

		/// <summary>
		/// Loads all packages in the currently selected game directory. This function does not enumerate files
		/// and directories deeper than one to keep the UI responsive.
		/// </summary>
		protected void LoadPackages()
		{
			if (CachedPackageDirectory != Config.GetGameDirectory() && Directory.Exists(Config.GetGameDirectory()))
			{
				CachedPackageDirectory = Config.GetGameDirectory();
				PackageListfiles.Clear();
				PackageSubfolderContent.Clear();
				PackageFolderNodeMapping.Clear();

				GameExplorerTreeStore.Clear();

				// Grab all packages in the game directory
				List<string> PackagePaths = new List<string>();
				foreach (string file in Directory.EnumerateFiles(Config.GetGameDirectory(), "*.*", SearchOption.AllDirectories)
				.Where(s => s.EndsWith(".mpq") || s.EndsWith(".MPQ")))
				{
					PackagePaths.Add(file);
				}

				// Verify that we do have packages in the path
				if (PackagePaths.Count == 0)
				{
					MessageDialog alertDialog = new MessageDialog(this, DialogFlags.Modal, MessageType.Warning, ButtonsType.Ok, 
						                            String.Format("The selected directory did not contain any supported packages.\nPlease select another directory."));

					alertDialog.Run();
					alertDialog.Destroy();
				}

				// Load the list files from all packages
				foreach (string PackagePath in PackagePaths)
				{
					using (FileStream fs = File.OpenRead(PackagePath))
					{
						string PackageName = System.IO.Path.GetFileName(PackagePath);

						try
						{
							using (MPQ Package = new MPQ(fs))
							{
								PackageListfiles.Add(PackageName, Package.GetFileList());
								PackagePathMapping.Add(PackageName, PackagePath);
							}
						}
						catch (Exception ex)
						{												
							MessageDialog alertDialog = new MessageDialog(this, DialogFlags.Modal, MessageType.Warning, ButtonsType.Ok, 
								                            String.Format("An error occurred while loading the package \"{0}\". Package loading has been aborted.\n\n" +
									                            "Please report this error to the developer:\n\n {1}\n", PackageName, ex.Message, ex.StackTrace));

							alertDialog.Run();
							alertDialog.Destroy();

							Console.WriteLine(String.Format("Exception in LoadPackages() (Package: {0}): {1}", PackageName, ex.Message));

							break;
						}
					}
				}

				// Add top level package nodes to the game explorer view
				foreach (KeyValuePair<string, List<string>> PackageListfile in PackageListfiles)
				{
					if (PackageListfile.Value != null)
					{
						string PackageName = System.IO.Path.GetFileName(PackageListfile.Key);
						AddPackageNode(PackageName);

						EnumerateFilesAndFolders(PackageName, "");
					}
				}
			}
		}

		/// <summary>
		/// Enumerates the files and subfolders in the specified package, starting at 
		/// the provided root path.
		/// </summary>
		/// <param name="packageName">Package name.</param>
		/// <param name="rootPath">Root path where the search should start.</param>
		private void EnumerateFilesAndFolders(string packageName, string rootPath)
		{
			List<string> PackageListfile;
			if (PackageListfiles.TryGetValue(packageName, out PackageListfile))
			{
				if (String.IsNullOrWhiteSpace(rootPath) && PackageListfile != null)
				{
					// Root files and folders in package
					List<string> TopLevelItems = new List<string>();

					foreach (string FilePath in PackageListfile)
					{
						int slashIndex = FilePath.IndexOf('\\');
						string topItem = FilePath.Substring(0, slashIndex + 1);

						if (!String.IsNullOrWhiteSpace(topItem) && !TopLevelItems.Contains(topItem))
						{
							TopLevelItems.Add(topItem);
							AddDirectoryNode(packageName, rootPath, topItem);
						}
						else if (String.IsNullOrWhiteSpace(topItem) && slashIndex == -1)
						{
							// It's probably a file
							// Remove the parent from the directory line
							string strippedPath = Regex.Replace(FilePath, "^" + Regex.Escape(rootPath), "");	

							if (IsFile(strippedPath))
							{
								// We've found a file!
								AddFileNode(packageName, rootPath, strippedPath);
							}
						}
					}
				}
				else
				{
					// Subfiles and folders in package
					List<string> TopLevelDirectories = new List<string>();
					bool bHasFoundStartOfFolderBlock = false;

					foreach (string FilePath in PackageListfile)
					{
						if (FilePath.StartsWith(rootPath))
						{
							bHasFoundStartOfFolderBlock = true;

							// Remove the parent from the directory line
							string strippedPath = Regex.Replace(FilePath, "^" + Regex.Escape(rootPath), "");

							// Get the top folders
							int slashIndex = strippedPath.IndexOf('\\');
							string topDirectory = strippedPath.Substring(0, slashIndex + 1);

							if (!String.IsNullOrEmpty(topDirectory) && !TopLevelDirectories.Contains(topDirectory) && !PackageFolderNodeMapping.ContainsKey(packageName + ":" + rootPath + topDirectory))
							{
								TopLevelDirectories.Add(topDirectory);
								AddDirectoryNode(packageName, rootPath, topDirectory);
							}
							else if (String.IsNullOrEmpty(topDirectory) && slashIndex == -1 && IsFile(strippedPath))
							{
								// We've found a file!
								AddFileNode(packageName, rootPath, strippedPath);
							}
						}
						else if (bHasFoundStartOfFolderBlock)
						{
							break;
						}
					}
				}
			}
		}

		/// <summary>
		/// Converts any non-native path separators to the current native path separator,
		/// e.g backslashes to forwardslashes on *nix, and vice versa.
		/// </summary>
		/// <returns>The path.</returns>
		/// <param name="inputPath">Input path.</param>
		public static string CleanPath(string inputPath)
		{
			if (IsRunningOnUnix())
			{
				return inputPath.Replace('\\', '/');
			}
			else
			{
				return inputPath.Replace('/', '\\');
			}
		}

		/// <summary>
		/// Handles extraction of files from the archive triggered by a context menu press.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnExtractContextItemActivated(object sender, EventArgs e)
		{
			TreeIter selectedIter;
			GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			string[] parts = GetFilePathFromIter(selectedIter).Split(':');
			string packageName = parts[0];
			string filePath = parts[1];

			string packagePath;
			if (PackagePathMapping.TryGetValue(packageName, out packagePath))
			{
				using (FileStream fs = File.OpenRead(packagePath))
				{
					using (MPQ mpq = new MPQ(fs))
					{
						string CleanFilepath = CleanPath(filePath);

						string exportpath;
						if (Config.GetShouldKeepFileDirectoryStructure())
						{
							exportpath = Config.GetDefaultExportDirectory() + CleanFilepath;
							Directory.CreateDirectory(Directory.GetParent(exportpath).FullName);
						}
						else
						{
							string filename = System.IO.Path.GetFileName(CleanFilepath);
							exportpath = Config.GetDefaultExportDirectory() + filename;
						}

						byte[] file = mpq.ExtractFile(filePath);
						File.WriteAllBytes(exportpath, file);
					}
				}
			}
		}

		/// <summary>
		/// Handles opening of files from the archive triggered by a context menu press.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnOpenContextItemActivated(object sender, EventArgs e)
		{
			TreeIter selectedIter;
			GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			string path = GetFilePathFromIter(selectedIter);
			if (!IsFile(path))
			{				
				GameExplorerTreeView.ExpandRow(CurrentGameExplorerPath, false);
			}
		}

		/// <summary>
		/// Handles copying of the file path of a selected item in the archive, triggered by a
		/// context menu press.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnCopyContextItemActivated(object sender, EventArgs e)
		{
			Clipboard clipboard = Clipboard.Get(Atom.Intern("CLIPBOARD", false));

			TreeIter selectedIter;
			GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			clipboard.Text = GetFilePathFromIter(selectedIter);
		}

		/// <summary>
		/// Handles queueing of a selected file in the archive, triggered by a context
		/// menu press.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnQueueContextItemActivated(object sender, EventArgs e)
		{
			TreeIter selectedIter;
			GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			string[] parts = GetFilePathFromIter(selectedIter).Split(':');
			string packageName = parts[0];
			string filePath = parts[1];

			string CleanFilepath = CleanPath(filePath);

			if (String.IsNullOrEmpty(filePath))
			{
				CleanFilepath = packageName;
			}
			else if (String.IsNullOrEmpty(System.IO.Path.GetFileName(CleanFilepath)))
			{
				CleanFilepath = Directory.GetParent(CleanFilepath).FullName.Replace(Directory.GetCurrentDirectory(), "");
			}
			

			ExportQueueListStore.AppendValues(CleanFilepath, CleanFilepath, "Queued");
		}

		/// <summary>
		/// Displays the About dialog to the user.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnAboutButtonClicked(object sender, EventArgs e)
		{		
			AboutDialog.Run();
			AboutDialog.Hide();
		}

		/// <summary>
		/// Displays the preferences dialog to the user.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnPreferencesButtonClicked(object sender, EventArgs e)
		{
			EverlookPreferences PreferencesDialog = EverlookPreferences.Create();

			if (PreferencesDialog.Run() == (int)ResponseType.Ok)
			{
				PreferencesDialog.SavePreferences();
				ReloadRuntimeValues();
			}

			PreferencesDialog.Destroy();
		}

		/// <summary>
		/// Reloads visible runtime values that the user can change in the preferences, such as the colour
		/// of the viewport or the loaded packages.
		/// </summary>
		protected void ReloadRuntimeValues()
		{
			MainDrawingArea.OverrideBackgroundColor(StateFlags.Normal, Config.GetViewportBackgroundColour());
			MainDrawingArea.QueueDraw();

			LoadPackages();
		}

		/// <summary>
		/// Handles the Frame Rendered event. Takes the input frame from the rendering thread and 
		/// draws it in the viewport.
		/// </summary>
		/// <param name="sender">Sending object (a viewport rendering thread).</param>
		/// <param name="e">Frame renderer arguments, containing the frame and frame delta.</param>
		protected void OnFrameRendered(object sender, FrameRendererEventArgs e)
		{

		}

		/// <summary>
		/// Handles expansion of rows in the game explorer, enumerating any subfolders and
		/// files present under that row.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnGameExplorerRowExpanded(object sender, RowExpandedArgs e)
		{		
			// Whenever a row is expanded, find the subfolders in the dictionary
			// Enumerate the files and subfolders in those.
			TreeIter iterNode;
			GameExplorerTreeStore.GetIter(out iterNode, e.Path);

			string itemKey = GetFilePathFromIter(e.Iter);
			if (PackageSubfolderContent.ContainsKey(itemKey))
			{
				string[] parts = itemKey.Split(':');
				string packageName = parts[0];
				string rootPath = parts[1];

				List<string> Subfolders;
				if (PackageSubfolderContent.TryGetValue(itemKey, out Subfolders))
				{
					foreach (string Subfolder in Subfolders)
					{
						EnumerateFilesAndFolders(packageName, rootPath + Subfolder);						
					}
				}
			}
		}

		/// <summary>
		/// Handles context menu spawning for the game explorer.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		[GLib.ConnectBefore]
		protected void OnGameExplorerButtonPressed(object sender, ButtonPressEventArgs e)
		{
			TreePath path;
			GameExplorerTreeView.GetPathAtPos((int)e.Event.X, (int)e.Event.Y, out path);

			CurrentGameExplorerPath = path;

			string currentPath = "";
			if (path != null)
			{
				TreeIter iter;
				GameExplorerTreeStore.GetIterFromString(out iter, path.ToString());
				currentPath = GetFilePathFromIter(iter);
			}

			if (e.Event.Type == EventType.ButtonPress && e.Event.Button == 3)
			{			
				if (String.IsNullOrEmpty(currentPath))
				{
					ExtractItem.Sensitive = false;
					OpenItem.Sensitive = false;
					QueueItem.Sensitive = false;
					CopyItem.Sensitive = false;
				}
				else
				{
					if (!IsFile(currentPath))
					{
						ExtractItem.Sensitive = false;
						OpenItem.Sensitive = true;
						QueueItem.Sensitive = true;
						CopyItem.Sensitive = true;
					}
					else
					{
						ExtractItem.Sensitive = true;
						OpenItem.Sensitive = true;
						QueueItem.Sensitive = true;
						CopyItem.Sensitive = true;
					}
				}


				FileContextMenu.ShowAll();
				FileContextMenu.Popup();
			}
		}

		/// <summary>
		/// Handles context menu spawning for the export queue.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		[GLib.ConnectBefore]
		protected void OnExportQueueButtonPressed(object sender, ButtonPressEventArgs e)
		{
			TreePath path;
			ExportQueueTreeView.GetPathAtPos((int)e.Event.X, (int)e.Event.Y, out path);

			string currentPath = "";
			if (path != null)
			{
				TreeIter iter;
				ExportQueueListStore.GetIterFromString(out iter, path.ToString());
				currentPath = GetFilePathFromIter(iter);
			}

			if (e.Event.Type == EventType.ButtonPress && e.Event.Button == 3)
			{			
				if (String.IsNullOrEmpty(currentPath))
				{
					RemoveQueueItem.Sensitive = false;
				}
				else
				{
					RemoveQueueItem.Sensitive = true;
				}

				QueueContextMenu.ShowAll();
				QueueContextMenu.Popup();
			}
		}

		/// <summary>
		/// Handles removal of items from the export queue, triggered by a context menu press.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnQueueRemoveContextItemActivated(object sender, EventArgs e)
		{
			TreeIter selectedIter;
			ExportQueueTreeView.Selection.GetSelected(out selectedIter);

			ExportQueueListStore.Remove(ref selectedIter);
		}

		/// <summary>
		/// Adds a package node to the game explorer view.
		/// </summary>
		/// <param name="packageName">Package name.</param>
		private void AddPackageNode(string packageName)
		{
			// I'm a new root node					
			TreeIter PackageNode = GameExplorerTreeStore.AppendValues("package-x-generic", packageName.Replace("\\", ""), "", packageName);
			PackageFolderNodeMapping.Add(packageName + ":", PackageNode);
		}

		/// <summary>
		/// Adds a directory node to the game explorer view, attachedt to the provided parent
		/// package and directory.
		/// </summary>
		/// <param name="PackagePath">Package path.</param>
		/// <param name="parentNodeKey">Parent node key.</param>
		/// <param name="directoryName">Directory name.</param>
		private void AddDirectoryNode(string PackagePath, string parentNodeKey, string directoryName)
		{
			string PackageName = System.IO.Path.GetFileName(PackagePath);

			TreeIter parentNode;
			PackageFolderNodeMapping.TryGetValue(PackageName + ":" + parentNodeKey, out parentNode);

			if (!GameExplorerTreeStore.IterIsValid(parentNode))
			{				
				PackageFolderNodeMapping.TryGetValue(PackageName + ":", out parentNode);
			}

			if (GameExplorerTreeStore.IterIsValid(parentNode))
			{
				// Add myself to that node
				if (!PackageFolderNodeMapping.ContainsKey(PackageName + ":" + parentNodeKey + directoryName))
				{
					TreeIter node = GameExplorerTreeStore.AppendValues(parentNode, Stock.Directory, directoryName.Replace("\\", ""), "", PackagePath);
					PackageFolderNodeMapping.Add(PackageName + ":" + parentNodeKey + directoryName, node);

					if (PackageSubfolderContent.ContainsKey(PackageName + ":" + parentNodeKey))
					{
						List<string> ContentList;
						if (PackageSubfolderContent.TryGetValue(PackageName + ":" + parentNodeKey, out ContentList))
						{
							ContentList.Add(directoryName);
							PackageSubfolderContent.Remove(PackageName + ":" + parentNodeKey);
							PackageSubfolderContent.Add(PackageName + ":" + parentNodeKey, ContentList);
						}
					}
					else
					{
						List<string> ContentList = new List<string>();
						ContentList.Add(directoryName);
						PackageSubfolderContent.Add(PackageName + ":" + parentNodeKey, ContentList);
					}
				}
			}
		}

		/// <summary>
		/// Adds a file node to the game explorer view, attached to the provided parent
		/// package and directory.
		/// </summary>
		/// <param name="PackagePath">Package path.</param>
		/// <param name="parentNodeKey">Parent node key.</param>
		/// <param name="fileName">File name.</param>
		private void AddFileNode(string PackagePath, string parentNodeKey, string fileName)
		{
			string PackageName = System.IO.Path.GetFileName(PackagePath);

			TreeIter parentNode;
			PackageFolderNodeMapping.TryGetValue(PackageName + ":" + parentNodeKey, out parentNode);

			if (!GameExplorerTreeStore.IterIsValid(parentNode))
			{				
				PackageFolderNodeMapping.TryGetValue(PackageName + ":", out parentNode);
			}

			if (GameExplorerTreeStore.IterIsValid(parentNode))
			{
				// Add myself to that node
				if (!PackageFolderNodeMapping.ContainsKey(PackageName + ":" + parentNodeKey + fileName))
				{
					TreeIter node = GameExplorerTreeStore.AppendValues(parentNode, GetIconForFiletype(fileName), fileName.Replace("\\", ""), "", PackagePath);
					PackageFolderNodeMapping.Add(PackageName + ":" + parentNodeKey + fileName, node);
				}
			}			
		}

		/// <summary>
		/// Gets the icon that would best represent the provided file. This is
		/// usually the mimetype.
		/// </summary>
		/// <returns>The icon for the filetype.</returns>
		/// <param name="file">File.</param>
		private string GetIconForFiletype(string file)
		{
			string fileIcon = Stock.File;

			if (file.EndsWith(".m2"))
			{
				// Blender armature icon?
			}
			else if (file.EndsWith(".wmo"))
			{
				// Blender object icon?
			}
			else if (file.EndsWith(".blp") || file.EndsWith(".jpg") || file.EndsWith(".gif"))
			{
				fileIcon = "image-x-generic";
			}
			else if (file.EndsWith(".wav") || file.EndsWith(".mp3") || file.EndsWith(".ogg"))
			{
				fileIcon = "audio-x-generic";
			}
			else if (file.EndsWith(".txt"))
			{
				fileIcon = "text-x-generic";
			}
			else if (file.EndsWith(".dbc") || file.EndsWith(".wdt"))
			{
				fileIcon = "x-office-spreadsheet";
			}
			else if (file.EndsWith(".exe"))
			{
				fileIcon = "application-x-executable";
			}
			else if (file.EndsWith(".dll"))
			{
				fileIcon = "application-x-executable";
			}
			else if (file.EndsWith(".wtf") || file.EndsWith(".ini"))
			{
				fileIcon = "text-x-script";
			}
			else if (file.EndsWith(".html") || file.EndsWith(".url"))
			{
				fileIcon = "text-html";
			}
			else if (file.EndsWith(".pdf"))
			{
				fileIcon = "x-office-address-book";
			}
			else if (file.EndsWith(".ttf") || file.EndsWith(".TTF"))
			{
				fileIcon = "font-x-generic";
			}
			else if (file.EndsWith(".wdl"))
			{
				fileIcon = "text-x-generic-template";
			}

			return fileIcon;
		}

		/// <summary>
		/// Converts a TreeIter into a file path. The final path is returned in the format of
		/// [packagename]:[file path].
		/// </summary>
		/// <returns>The file path from iter.</returns>
		/// <param name="iter">Iter.</param>
		private string GetFilePathFromIter(TreeIter iter)
		{
			TreeIter parentIter;
			string finalPath;

			GameExplorerTreeStore.IterParent(out parentIter, iter);
			if (GameExplorerTreeStore.IterIsValid(parentIter))
			{
				finalPath = GetFilePathFromIter(parentIter) + (string)GameExplorerTreeStore.GetValue(iter, 1);

				if (!IsFile(finalPath))
				{
					finalPath += @"\";
				}
			}
			else
			{
				// Parentless nodes are package nodes
				finalPath = (string)GameExplorerTreeStore.GetValue(iter, 1) + ":";
			}

			return finalPath;
		}

		/// <summary>
		/// Determines whether the provided path is a file or not.
		/// </summary>
		/// <returns><c>true</c> if the path is a file; otherwise, <c>false</c>.</returns>
		/// <param name="InputPath">Input path.</param>
		private bool IsFile(string InputPath)
		{
			string strippedName = InputPath;
			if (InputPath.Contains(":"))
			{
				// Remove the package identifier
				int packageIDIndex = InputPath.IndexOf(":");
				strippedName = InputPath.Substring(packageIDIndex + 1);
			}

			if (strippedName.EndsWith("\\"))
			{
				return false;
			}

			string[] parts = strippedName.Split('.');
			return parts.Length > 1;
		}

		/// <summary>
		/// Determines if the application is running on a unix-like system.
		/// </summary>
		/// <returns><c>true</c> if is running on unix; otherwise, <c>false</c>.</returns>
		public static bool IsRunningOnUnix()
		{
			int p = (int)Environment.OSVersion.Platform;
			if ((p == 4) || (p == 6) || (p == 128))
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Handles application shutdown procedures - terminating render threads, cleaning
		/// up the UI, etc.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="a">The alpha component.</param>
		protected void OnDeleteEvent(object sender, DeleteEventArgs a)
		{
			if (viewportRenderer.IsActive)
			{
				viewportRenderer.Stop();
			}

			Application.Quit();
			a.RetVal = true;
		}
	}
}