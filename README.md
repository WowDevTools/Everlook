# Everlook
Everlook is a cross-platform, open-source World of Warcraft model viewer, created to showcase the capabilities of libwarcraft.

Everlook is capable of browsing, exporting and converting most World of Warcraft formats up until 
Wrath of the Lich King, and is under active development. The current goal is to act as an open, simple
and feature-complete replacement for World of Warcraft Model Viewer.

Currently, Everlook is in early development and may not be usable in your day-to-day activities.

### Features
* Explore multiple game versions in one application
* Explore games on an archive-by-archive basis, or as a unified virtual file tree
* Export files from the archives
* View textures stored in most major image formats, as well as BLP

![Everlook](https://i.imgur.com/ZusgxJ7.png)

### Known Isssues
* No format-specific export functions have been implemented.
* The export queue does not work beyond the UI.
* Everlook lacks any testing on Windows-based systems.
* The UI becomes sluggish when loading directories with many files in them (mainly Textures/Minimap and Textures/BakedNPCTextures, who both have thousands of files under a single directory).
* Models are not rendered in the viewport.

### Compiling
In order to compile Everlook, you will need a Nuget-capable IDE that supports the C# language. The most commonly used ones are Visual Studio, MonoDevelop and more recently Project Rider. Additionally, you need recent copies of the following projects: 

* [libwarcraft](https://github.com/Nihlus/libwarcraft)
* [liblistfile](https://github.com/Nihlus/liblistfile)

If you're running Windows, you also need the GTK# 3 libraries, which are available here:
* [GTK# 3 for Windows](https://download.gnome.org/binaries/win32/gtk-sharp/2.99/gtk-sharp-2.99.3.msi)

For Debian-based Linux distributions, the following package should suffice:
* mono-complete (>= 4.4.2.11-0xamarin1)

Beyond that, it's pretty straightforward - hit compile, run, and develop.

### Binary Packages
Currently, Everlook does not provide any binary packages or installers due to its early state. Packages will, 
in the future, be available as a debian repository and zip archives. 

### Why?
World of Warcraft modding and development in general relies on a number of different command-line utilities, halfway finished applications and various pieces of abandonware, many which lack any source code. Furthermore, most of them are written for a specific operating system (most commonly Windows), which limits their use for developers on other systems.

libwarcraft (and, by extension, Everlook) is intended to solve at least a few of these problems, providing a common library for all Warcraft file formats upon which more specialized applications can be built - such as Everlook. 

Everlook itself stems from my frustration with WMV and its utter inability to compile on any of my systems, as well as its broken model export functions which were more harm than help. I have naught but respect for the creator of WMV, but it does not meet my requirements for a model viewer and exporter. Thus, Everlook.
