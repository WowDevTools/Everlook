# Everlook
[![Latest Download](https://img.shields.io/badge/Latest-Download-blue.svg)](https://ci.appveyor.com/api/projects/majorcyto/everlook/artifacts/) [![DoxygenDoc](https://img.shields.io/badge/Docs-Doxygen-red.svg)](http://everlookdocs.wowdev.info/)
[![Bountysource](https://www.bountysource.com/badge/tracker?tracker_id=34637447)](https://www.bountysource.com/trackers/34637447-wowdevtools-everlook?utm_source=44433103&utm_medium=shield&utm_campaign=TRACKER_BADGE)

## Build Status

CI | Build | Defects
:------------: | :------------: | :------------:
AppVeyor | [![Build status](https://ci.appveyor.com/api/projects/status/lf5swhbglpcuni33/branch/master?svg=true)](https://ci.appveyor.com/project/majorcyto/everlook/branch/master) | Coverity Badge Soon
Travis | [![Build Status](https://travis-ci.org/WowDevTools/Everlook.svg?branch=master)](https://travis-ci.org/WowDevTools/Everlook) | 

# About #
Everlook is a cross-platform, open-source World of Warcraft model viewer, created to showcase the capabilities of libwarcraft.

Everlook will be capable of browsing, exporting and converting most World of Warcraft formats up until 
Wrath of the Lich King, and is under active development. The current goal is to act as an open, simple
and feature-complete replacement for World of Warcraft Model Viewer.

Currently, Everlook is in early development and may not be usable in your day-to-day activities.

### Features
* Explore multiple game versions in one application
* Explore games on an archive-by-archive basis, or as a unified virtual file tree
* Export files from the archives
* View textures stored in most major image formats, as well as BLP
* View WMO models

![Everlook](https://i.imgur.com/ZusgxJ7.png)

### Known Isssues
* No format-specific export functions have been implemented.
* The export queue does not work beyond the UI.
* Everlook lacks any testing on Windows-based systems.
* The UI becomes sluggish when loading directories with many files in them (mainly Textures/Minimap and Textures/BakedNPCTextures, who both have thousands of files under a single directory).
* Standard models are not rendered in the viewport.

### Compiling
In order to compile Everlook, you will need a Nuget-capable IDE that supports the C# language. The most commonly used ones are Visual Studio, MonoDevelop and more recently Project Rider. 

Beyond that, downloading and compiling Everlook is as simple as the following commands:

    $ git clone git@github.com:WowDevTools/Everlook.git
    $ cd Everlook
    $ git submodule update --init --recursive
    $ nuget restore

If you're running Windows, you also need the GTK# 3 libraries, which are available here:
* [GTK# 3 for Windows](https://download.gnome.org/binaries/win32/gtk-sharp/2.99/gtk-sharp-2.99.3.msi)

For Debian-based Linux distributions, the following package should suffice:
* mono-complete (>= 4.4.2.11-0xamarin1)

### Binary Packages
There are a number of ways you could get Everlook. For Windows users, the current method is, unfortunately, limited to downloading and compiling from source. You get the latest version, but it's a bit more of a hassle. In the future, Everlook may become available as an installer.

Ubuntu (and Ubuntu derivations) can simply add this PPA to get the application and a number of other helper packages, such as mime types and the underlying libraries used in the development of Everlook.

* [[PPA] blizzard-development-tools](https://launchpad.net/~jarl-gullberg/+archive/ubuntu/blizzard-dev-tools)

Debian users can manually download packages from the PPA, or add it manually to their sources.list file. Maybe someday it'll be in the main repo? We can hope!

Other Linux users can get tarballs of the binaries from the PPA as well. I plan on figuring out some better format for you soon. If someone who uses Arch sees this, I'd love some help getting it onto the AUR.Currently, Everlook does not provide any binary packages or installers due to its early state.

### Why?
World of Warcraft modding and development in general relies on a number of different command-line utilities, halfway finished applications and various pieces of abandonware, many which lack any source code. Furthermore, most of them are written for a specific operating system (most commonly Windows), which limits their use for developers on other systems.

libwarcraft (and, by extension, Everlook) is intended to solve at least a few of these problems, providing a common library for all Warcraft file formats upon which more specialized applications can be built - such as Everlook. 

Everlook itself stems from my frustration with WMV and its utter inability to compile on any of my systems, as well as its broken model export functions which were more harm than help. I have naught but respect for the creator of WMV, but it does not meet my requirements for a model viewer and exporter. Thus, Everlook.
