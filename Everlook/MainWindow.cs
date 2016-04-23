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
using Everlook.Configuration;
using Everlook.Viewport;
using System.IO;
using Everlook.Utility;
using Warcraft.Core;
using Everlook.Renderables;
using Warcraft.BLP;
using Everlook.Explorer;
using System.Drawing;
using Everlook.Package;
using Everlook.Export.Image;
using Everlook.Export.Directory;
using System.Linq;

namespace Everlook
{
	/// <summary>
	/// Main UI class for Everlook. The "partial" qualifier is not strictly needed, but prevents the compiler from 
	/// generating errors about the autoconnected members that relate to UI elements.
	/// </summary>
	public partial class MainWindow: Gtk.Window
	{
		/*
			Main UI elements
		*/
		[UI] ToolButton AboutButton;
		[UI] AboutDialog AboutDialog;
		[UI] ToolButton PreferencesButton;

		[UI] Gtk.Viewport MainViewportContainer;
		[UI] Gtk.Image MainDrawingArea;

		/*
			Export queue elements
		*/
		[UI] TreeView ExportQueueTreeView;
		[UI] ListStore ExportQueueListStore;

		[UI] Menu QueueContextMenu;
		[UI] ImageMenuItem RemoveQueueItem;

		/*
			Game explorer elements
		*/
		[UI] TreeView GameExplorerTreeView;
		[UI] TreeStore GameExplorerTreeStore;

		[UI] Menu FileContextMenu;
		[UI] ImageMenuItem ExtractItem;
		[UI] ImageMenuItem ExportItem;
		[UI] ImageMenuItem OpenItem;
		[UI] ImageMenuItem CopyItem;
		[UI] ImageMenuItem QueueItem;

		/*
			General item control elements
		*/

		[UI] Notebook ItemControlNotebook;

		/*
			Image control elements
		*/
		[UI] ComboBox MipLevelComboBox;
		[UI] ListStore MipLevelListStore;
		[UI] CheckButton RenderAlphaCheckButton;
		[UI] CheckButton RenderRedCheckButton;
		[UI] CheckButton RenderGreenCheckButton;
		[UI] CheckButton RenderBlueCheckButton;

		/*
			Model control elements
		*/

		/*
			Animation control elements
		*/

		/*
			Audio control elements
		*/

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
		/// Background file explorer tree builder. Handles enumeration of files in the archives.
		/// </summary>
		private readonly ExplorerBuilder explorerBuilder = new ExplorerBuilder();

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
			GameExplorerTreeView.Selection.Changed += OnGameExplorerSelectionChanged;
			GameExplorerTreeView.ButtonPressEvent += OnGameExplorerButtonPressed;

			ExportQueueTreeView.ButtonPressEvent += OnExportQueueButtonPressed;	

			ExtractItem.Activated += OnExtractContextItemActivated;
			ExportItem.Activated += OnExportItemContextItemActivated;
			OpenItem.Activated += OnOpenContextItemActivated;
			CopyItem.Activated += OnCopyContextItemActivated;
			QueueItem.Activated += OnQueueContextItemActivated;

			RemoveQueueItem.Activated += OnQueueRemoveContextItemActivated;

			viewportRenderer.FrameRendered += OnFrameRendered;
			viewportRenderer.Start();

			explorerBuilder.PackageGroupAdded += OnPackageGroupAdded;
			explorerBuilder.PackageEnumerated += OnPackageEnumerated;
			explorerBuilder.DirectoryEnumerated += OnDirectoryEnumerated;
			explorerBuilder.FileEnumerated += OnFileEnumerated;
			explorerBuilder.Start();

			/*
				Set up item control sections
			*/
			// Image
			MipLevelComboBox.Changed += OnSelectedMipLevelChanged;

			RenderAlphaCheckButton.Sensitive = false;
			RenderRedCheckButton.Sensitive = false;
			RenderGreenCheckButton.Sensitive = false;
			RenderBlueCheckButton.Sensitive = false;

			// Model

			// Animation

			// Audio
		}

		/// <summary>
		/// Enables the specified control page and brings it to the front.
		/// </summary>
		/// <param name="Page">Page.</param>
		protected void EnableControlPage(ControlPage Page)
		{
			if (Enum.IsDefined(typeof(ControlPage), Page))
			{
				ItemControlNotebook.Page = (int)Page;

				if (Page == ControlPage.Image)
				{
					MipLevelComboBox.Sensitive = true;
					RenderAlphaCheckButton.Sensitive = true;
					RenderRedCheckButton.Sensitive = true;
					RenderGreenCheckButton.Sensitive = true;
					RenderBlueCheckButton.Sensitive = true;
				}
				else if (Page == ControlPage.Model)
				{

				}
				else if (Page == ControlPage.Animation)
				{

				}
				else if (Page == ControlPage.Audio)
				{

				}
			}
		}

		/// <summary>
		/// Disables the specified control page.
		/// </summary>
		/// <param name="Page">Page.</param>
		protected void DisableControlPage(ControlPage Page)
		{
			if (Enum.IsDefined(typeof(ControlPage), Page))
			{
				if (Page == ControlPage.Image)
				{
					MipLevelComboBox.Sensitive = false;
					RenderAlphaCheckButton.Sensitive = false;
					RenderRedCheckButton.Sensitive = false;
					RenderGreenCheckButton.Sensitive = false;
					RenderBlueCheckButton.Sensitive = false;
				}
				else if (Page == ControlPage.Model)
				{

				}
				else if (Page == ControlPage.Animation)
				{
				
				}
				else if (Page == ControlPage.Audio)
				{

				}
			}
		}

		/// <summary>
		/// Handles the export item context item activated event.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnExportItemContextItemActivated(object sender, EventArgs e)
		{
			TreeIter selectedIter;
			GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			ItemReference fileReference = GetItemReferenceFromIter(selectedIter);
			if (fileReference != null && !String.IsNullOrEmpty(fileReference.ItemPath))
			{	
				WarcraftFileType fileType = fileReference.GetReferencedFileType();
				switch (fileType)
				{
					case WarcraftFileType.Directory:
						{
							if (fileReference.IsFullyEnumerated)
							{
								string PackagePath = explorerBuilder.PackagePathMapping[fileReference.PackageName];
								using (EverlookDirectoryExportDialog ExportDialog = EverlookDirectoryExportDialog.Create(fileReference, PackagePath))
								{
									if (ExportDialog.Run() == (int)ResponseType.Ok)
									{
										ExportDialog.RunExport();
									}
									ExportDialog.Destroy();
								}
							}
							else
							{
								// TODO: Implement wait message when the directory and its subdirectories have not yet been enumerated.
							}
							break;
						}
					case WarcraftFileType.BinaryImage:
						{
							string PackagePath = explorerBuilder.PackagePathMapping[fileReference.PackageName];
							using (EverlookImageExportDialog ExportDialog = EverlookImageExportDialog.Create(fileReference, PackagePath))
							{
								if (ExportDialog.Run() == (int)ResponseType.Ok)
								{
									ExportDialog.RunExport();
								}
								ExportDialog.Destroy();
							}
							break;
						}
					default:
						{
							break;
						}
				}
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

			ItemReference fileReference = GetItemReferenceFromIter(selectedIter);

			string CleanFilepath = Utilities.CleanPath(fileReference.ItemPath);
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

			string PackagePath = explorerBuilder.PackagePathMapping[fileReference.PackageName];
			using (PackageInteractionHandler PackageHandler = new PackageInteractionHandler(PackagePath))
			{
				byte[] file = PackageHandler.ExtractReference(fileReference);
				if (file != null)
				{
					File.WriteAllBytes(exportpath, file);
				}
			}
		}

		/// <summary>
		/// Handles changing the mip level that is being rendered in the viewport based
		/// on the user selection.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnSelectedMipLevelChanged(object sender, EventArgs e)
		{
			if (viewportRenderer.IsActive)
			{
				int qualityLevel = MipLevelComboBox.Active;

				if (qualityLevel >= 0)
				{
					viewportRenderer.SetRequestedQualityLevel((uint)qualityLevel);
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

			ItemReference itemReference = GetItemReferenceFromIter(selectedIter);
			if (!itemReference.IsFile)
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

			clipboard.Text = GetItemReferenceFromIter(selectedIter).ToString();
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

			ItemReference itemReference = GetItemReferenceFromIter(selectedIter);

			string CleanFilepath = Utilities.CleanPath(itemReference.ItemPath);

			if (String.IsNullOrEmpty(CleanFilepath))
			{
				CleanFilepath = itemReference.PackageName;
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
			using (EverlookPreferences PreferencesDialog = EverlookPreferences.Create())
			{
				if (PreferencesDialog.Run() == (int)ResponseType.Ok)
				{
					PreferencesDialog.SavePreferences();
					ReloadRuntimeValues();
				}

				PreferencesDialog.Destroy();
			}
		}

		/// <summary>
		/// Reloads visible runtime values that the user can change in the preferences, such as the colour
		/// of the viewport or the loaded packages.
		/// </summary>
		protected void ReloadRuntimeValues()
		{
			MainDrawingArea.OverrideBackgroundColor(StateFlags.Normal, Config.GetViewportBackgroundColour());
		
			if (explorerBuilder.HasPackageDirectoryChanged())
			{
				GameExplorerTreeStore.Clear();
				explorerBuilder.Reload();
			}
		}

		/// <summary>
		/// Handles the Frame Rendered event. Takes the input frame from the rendering thread and 
		/// draws it in the viewport on the GUI thread.
		/// </summary>
		/// <param name="sender">Sending object (a viewport rendering thread).</param>
		/// <param name="e">Frame renderer arguments, containing the frame and frame delta.</param>
		protected void OnFrameRendered(object sender, FrameRendererEventArgs e)
		{			
			Application.Invoke(delegate
				{
					if (MainDrawingArea.Pixbuf != null)
					{
						MainDrawingArea.Pixbuf.Dispose();
					}

					MainDrawingArea.Pixbuf = e.Frame;
				});
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

			ItemReference parentReference = GetItemReferenceFromIter(e.Iter);
			foreach (ItemReference childReference in parentReference.ChildReferences)
			{
				if (childReference.IsDirectory)
				{
					explorerBuilder.SubmitWork(childReference);
				}
			}
		}

		/// <summary>
		/// Handles selection of files in the game explorer, displaying them to the user and routing 
		/// whatever rendering functionality the file needs to the viewport.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnGameExplorerSelectionChanged(object sender, EventArgs e)
		{
			TreeIter selectedIter;
			GameExplorerTreeView.Selection.GetSelected(out selectedIter);

			ItemReference fileReference = GetItemReferenceFromIter(selectedIter);
			if (fileReference != null && !String.IsNullOrEmpty(fileReference.ItemPath))
			{
				string fileName = System.IO.Path.GetFileName(Utilities.CleanPath(fileReference.ItemPath));
				switch (fileReference.GetReferencedFileType())
				{
					case WarcraftFileType.AddonManifest:
						{
							break;
						}
					case WarcraftFileType.AddonManifestSignature:
						{
							break;
						}
					case WarcraftFileType.MoPaQArchive:
						{
							break;
						}
					case WarcraftFileType.ConfigurationFile:
						{
							break;
						}
					case WarcraftFileType.DatabaseContainer:
						{
							break;
						}
					case WarcraftFileType.Shader:
						{
							break;
						}
					case WarcraftFileType.TerrainWater:
						{
							break;
						}
					case WarcraftFileType.TerrainLiquid:
						{
							break;
						}
					case WarcraftFileType.TerrainLevel:
						{
							break;
						}
					case WarcraftFileType.TerrainTable:
						{
							break;
						}
					case WarcraftFileType.TerrainData:
						{
							break;
						}
					case WarcraftFileType.BinaryImage:
						{
							if (!viewportRenderer.IsActive)
							{
								viewportRenderer.Start();							
							}

							string PackagePath = explorerBuilder.PackagePathMapping[fileReference.PackageName];
							using (PackageInteractionHandler PackageHandler = new PackageInteractionHandler(PackagePath))
							{
								byte[] fileData = PackageHandler.ExtractReference(fileReference);
								if (fileData != null)
								{
									BLP blp = new BLP(fileData);
									RenderableBLP image = new RenderableBLP(blp);
									viewportRenderer.SetRenderTarget(image);

									MipLevelListStore.Clear();
									foreach (string mipString in blp.GetMipMapLevelStrings())
									{
										MipLevelListStore.AppendValues(mipString);
									}

									viewportRenderer.SetRequestedQualityLevel(0);
									MipLevelComboBox.Active = 0;

									EnableControlPage(ControlPage.Image);
								}
							}


							break;
						}
					case WarcraftFileType.Hashmap:
						{
							break;
						}
					case WarcraftFileType.GameObjectModel:
						{
							break;
						}
					case WarcraftFileType.WorldObjectModel:
						{
							break;
						}
				}

				// Try some "normal" files
				if (fileName.EndsWith(".jpg") || fileName.EndsWith(".gif") || fileName.EndsWith(".png"))
				{
					if (!viewportRenderer.IsActive)
					{
						viewportRenderer.Start();							
					}

					string PackagePath = explorerBuilder.PackagePathMapping[fileReference.PackageName];
					using (PackageInteractionHandler PackageHandler = new PackageInteractionHandler(PackagePath))
					{
						byte[] fileData = PackageHandler.ExtractReference(fileReference);
						if (fileData != null)
						{
							using (MemoryStream ms = new MemoryStream(fileData))
							{
								Bitmap Image = new Bitmap(ms);
								RenderableBitmap Renderable = new RenderableBitmap(Image);

								viewportRenderer.SetRenderTarget(Renderable);
							}

							// Normal image files don't have mipmap levels
							MipLevelListStore.Clear();
							EnableControlPage(ControlPage.Image);
						}
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

			ItemReference currentItemReference = null;
			if (path != null)
			{
				TreeIter iter;
				GameExplorerTreeStore.GetIterFromString(out iter, path.ToString());
				currentItemReference = GetItemReferenceFromIter(iter);
			}

			if (e.Event.Type == EventType.ButtonPress && e.Event.Button == 3)
			{		
				if (currentItemReference == null || String.IsNullOrEmpty(currentItemReference.ItemPath))
				{
					ExtractItem.Sensitive = false;
					OpenItem.Sensitive = false;
					QueueItem.Sensitive = false;
					CopyItem.Sensitive = false;
				}
				else
				{
					if (!currentItemReference.IsFile)
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

			ItemReference currentReference = null;
			if (path != null)
			{
				TreeIter iter;
				ExportQueueListStore.GetIterFromString(out iter, path.ToString());
				currentReference = GetItemReferenceFromIter(iter);
			}

			if (e.Event.Type == EventType.ButtonPress && e.Event.Button == 3)
			{			
				if (currentReference == null || String.IsNullOrEmpty(currentReference.ItemPath))
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
		/// Handles the package group added event from the explorer builder.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnPackageGroupAdded(object sender, ItemEnumeratedEventArgs e)
		{
			Application.Invoke(delegate
				{
					AddPackageGroupNode(e.Item);
				});
		}

		/// <summary>
		/// Adds a package group node to the game explorer view
		/// </summary>
		/// <param name="groupReference">Group reference.</param>
		private void AddPackageGroupNode(ItemReference groupReference)
		{
			// Add the group node
			TreeIter PackageGroupNode = GameExplorerTreeStore.AppendValues("user-home", groupReference.Group.GroupName, "", "");
			explorerBuilder.PackageItemNodeMapping.Add(groupReference, PackageGroupNode);
			explorerBuilder.PackageNodeItemMapping.Add(PackageGroupNode, groupReference);

			// Add the package folder subnode
			TreeIter PackageFolderNode = GameExplorerTreeStore.AppendValues(PackageGroupNode, "applications-other", "Packages", "", "");
			explorerBuilder.PackageItemNodeMapping.Add(groupReference.ChildReferences.First(), PackageFolderNode);
			explorerBuilder.PackageNodeItemMapping.Add(PackageFolderNode, groupReference.ChildReferences.First());
		}

		/// <summary>
		/// Handles the package enumerated event from the explorer builder.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnPackageEnumerated(object sender, ItemEnumeratedEventArgs e)
		{
			Application.Invoke(delegate
				{
					AddPackageNode(e.Item.ParentReference, e.Item);
				});
		}

		/// <summary>
		/// Adds a package node to the game explorer view.
		/// </summary>
		/// <param name="parentReference">Parent reference where the package should be added.</param>
		/// <param name="packageReference">Item reference pointing to the package..</param>
		private void AddPackageNode(ItemReference parentReference, ItemReference packageReference)
		{
			// I'm a new root node
			TreeIter parentNode;
			explorerBuilder.PackageItemNodeMapping.TryGetValue(parentReference, out parentNode);

			if (GameExplorerTreeStore.IterIsValid(parentNode))
			{
				// Add myself to that node
				if (!explorerBuilder.PackageItemNodeMapping.ContainsKey(packageReference))
				{
					TreeIter PackageNode = GameExplorerTreeStore.AppendValues(parentNode, "package-x-generic", packageReference.PackageName, "", "");
					explorerBuilder.PackageItemNodeMapping.Add(packageReference, PackageNode);
					explorerBuilder.PackageNodeItemMapping.Add(PackageNode, packageReference);
				}
			}

		}

		/// <summary>
		/// Handles the directory enumerated event from the explorer builder.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnDirectoryEnumerated(object sender, ItemEnumeratedEventArgs e)
		{
			Application.Invoke(delegate
				{
					AddDirectoryNode(e.Item.ParentReference, e.Item);
				});
		}

		// TODO: Rework
		/// <summary>
		/// Adds a directory node to the game explorer view, attachedt to the provided parent
		/// package and directory.
		/// </summary>
		/// <param name="parentReference">Parent reference where the new directory should be added.</param>
		/// <param name="childReference">Child reference representing the directory..</param>
		private void AddDirectoryNode(ItemReference parentReference, ItemReference childReference)
		{
			TreeIter parentNode;
			explorerBuilder.PackageItemNodeMapping.TryGetValue(parentReference, out parentNode);

			if (GameExplorerTreeStore.IterIsValid(parentNode))
			{
				// Add myself to that node
				if (!explorerBuilder.PackageItemNodeMapping.ContainsKey(childReference))
				{
					TreeIter node = GameExplorerTreeStore.AppendValues(parentNode, Stock.Directory, childReference.GetReferencedItemName(), "", "");
					explorerBuilder.PackageItemNodeMapping.Add(childReference, node);
					explorerBuilder.PackageNodeItemMapping.Add(node, childReference);
				}
			}
		}

		/// <summary>
		/// Handles the file enumerated event from the explorer builder.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		protected void OnFileEnumerated(object sender, ItemEnumeratedEventArgs e)
		{
			Application.Invoke(delegate
				{
					AddFileNode(e.Item.ParentReference, e.Item);
				});
		}

		/// <summary>
		/// Adds a file node to the game explorer view, attached to the provided parent
		/// package and directory.
		/// </summary>
		/// <param name="parentReference">Parent file reference</param>
		/// <param name="childReference">Child file reference.</param>
		private void AddFileNode(ItemReference parentReference, ItemReference childReference)
		{
			TreeIter parentNode;
			explorerBuilder.PackageItemNodeMapping.TryGetValue(parentReference, out parentNode);

			if (GameExplorerTreeStore.IterIsValid(parentNode))
			{
				// Add myself to that node
				if (!explorerBuilder.PackageItemNodeMapping.ContainsKey(childReference))
				{
					TreeIter node = GameExplorerTreeStore.AppendValues(parentNode, Utilities.GetIconForFiletype(childReference.ItemPath), childReference.GetReferencedItemName(), "", "");
					explorerBuilder.PackageItemNodeMapping.Add(childReference, node);
					explorerBuilder.PackageNodeItemMapping.Add(node, childReference);
				}
			}			
		}

		/// <summary>
		/// Converts a TreeIter into a file path. The final path is returned as a file reference
		/// object.
		/// </summary>
		/// <returns>The file path from iter.</returns>
		/// <param name="iter">Iter.</param>
		private ItemReference GetItemReferenceFromIter(TreeIter iter)
		{
			ItemReference reference;
			if (explorerBuilder.PackageNodeItemMapping.TryGetValue(iter, out reference))
			{
				return reference;
			}
			else
			{
				return null;
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

			if (explorerBuilder.IsActive)
			{
				explorerBuilder.Stop();
			}

			Application.Quit();
			a.RetVal = true;
		}
	}

	/// <summary>
	/// Available control pages in the Everlook UI.
	/// </summary>
	public enum ControlPage
	{
		/// <summary>
		/// Image control page. Handles mip levels and rendered channels.
		/// </summary>
		Image = 0,

		/// <summary>
		/// Model control page. Handles vertex joining, geoset rendering and other model
		/// settings.
		/// </summary>
		Model = 1,

		/// <summary>
		/// Animation control page. Handles active animations and their settings.
		/// </summary>
		Animation = 2,

		/// <summary>
		/// Audio control page. Handles playback of audio.
		/// </summary>
		Audio = 3
	}
}