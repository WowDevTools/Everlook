# Everlook
Everlook is a cross-platform, open-source World of Warcraft model viewer, created to showcase the capabilities of libwarcraft.

Everlook is capable of browsing, exporting and converting most World of Warcraft formats up until 
Wrath of the Lich King, and is under active development. The current goal is to act as an open, simple
and feature-complete replacement for World of Warcraft Model Viewer.

Currently, Everlook is in early development and may not be usable in your day-to-day activities.

### Features
* Explore multiple MPQ archives at once
* Export files from the archives

![Everlook](https://i.imgur.com/d71bEm3.png)

### Known Isssues
* Nothing is displayed in the viewport for any selected file.
* No format-specific export functions have been implemented.
* The export queue does not work beyond the UI.
* Certain files cause a crash when they are exported (underlying bug in libwarcraft)
* Everlook lacks any testing on Windows-based systems.

### Compiling
In order to compile Everlook, you will need a Nuget-capable IDE that supports the C# language. The most commonly
used ones are Visual Studio, MonoDevelop and more recently Project Rider. Additionally, you need a recent copy of 
libwarcraft, which can be cloned from here: https://github.com/Nihlus/libwarcraft

Beyond that, it's pretty straightforward - hit compile, run and develop.

### Binary Packages
Currently, Everlook does not provide any binary packages or installers due to its early state. Packages will, 
in the future, be available as a debian repository and zip archives. 

### Why?
World of Warcraft modding and development in general relies on a number of different command-line utilities,
halfway finished applications and various pieces of abandonware, many which lack any source code. Furthermore, 
most of them are written for a specific operating system (most commonly Windows), which limits their use for
developers on other systems.

libwarcraft (and, by extension, Everlook) is intended to solve at least a few of these problems, providing a common
library for all Warcraft file formats upon which more specialized applications can be built - such as Everlook. Everlook
itself stems from my frustration with WMV and its utter inability to compile on any of my systems, as well as its broken
model export functions which were more harm than help. I have naught but respect for the creator of WMV, but it does not 
meet my requirements for a model viewer and exporter. Thus, Everlook.
