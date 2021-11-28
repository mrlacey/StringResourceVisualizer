# String Resource Visualizer

[![Build status](https://ci.appveyor.com/api/projects/status/a8qsff6l76e04p06?svg=true)](https://ci.appveyor.com/project/mrlacey/stringresourcevisualizer)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
![Works with Visual Studio 2019](https://img.shields.io/static/v1.svg?label=VS&message=2019&color=5F2E96)

Download the extension from the [VS Marketplace](https://marketplace.visualstudio.com/items?itemName=MattLaceyLtd.StringResourceVisualizer)
or get the
[CI build](http://vsixgallery.com/extension/StringResourceVisualizer.a05f89b1-98f8-4b37-8f84-4fdebc44aa25/)

-------------------------------------

Visual Studio extension that shows the text of a string resource (.resx) when used inline in code.

![screenshot](./art/screenshot.png)

The default (language/culture agnostic) resource file is used to find the text to display but you can override this by specifying a **Preferred Culture** in settings. (Got to **Tools > Options > String Resource Visualizer**)

![setting](./art/settings.png)

If a string is not specified for the preferred culture, the default value is used instead.

See the [change log](CHANGELOG.md) for changes and road map.

## Contribute

Check out the [contribution guidelines](CONTRIBUTING.md) if you want to contribute to this project.

For cloning and building this project yourself, make sure to install the
[Extensibility Tools](https://visualstudiogallery.msdn.microsoft.com/ab39a092-1343-46e2-b0f1-6a3f91155aa6)
extension for Visual Studio which enables some features used by this project.
