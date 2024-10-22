# String Resource Visualizer

![Works with Visual Studio 2022](https://img.shields.io/static/v1.svg?label=VS&message=2022&color=A853C7)
![Works with Visual Studio 2019](https://img.shields.io/static/v1.svg?label=VS&message=2019&color=5F2E96)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
![Visual Studio Marketplace 5 Stars](https://img.shields.io/badge/VS%20Marketplace-★★★★★-green)

[![Build](https://github.com/mrlacey/StringResourceVisualizer/actions/workflows/build.yaml/badge.svg)](https://github.com/mrlacey/StringResourceVisualizer/actions/workflows/build.yaml)
![Tests](https://gist.githubusercontent.com/mrlacey/c586ff0f495b4a8dd76ab0dbdf9c89e0/raw/StringResourceVisualizer.badge.svg)

A [Visual Studio extension](https://marketplace.visualstudio.com/items?itemName=MattLaceyLtd.StringResourceVisualizer) that shows the text of a string resource (.resx) when used inline in code.

![screenshot](./art/screenshot.png)

The default (language/culture agnostic) resource file is used to find the text to display but you can override this by specifying a **Preferred Culture** in settings. (Go to **Tools > Options > String Resource Visualizer**)

![setting](./art/settings.png)

If a string is not specified for the preferred culture, the default value is used instead.

See the [change log](CHANGELOG.md) for changes and road map.

## Contribute

Check out the [contribution guidelines](CONTRIBUTING.md) if you want to contribute to this project.

For cloning and building this project yourself, make sure to install the
[Extensibility Tools](https://visualstudiogallery.msdn.microsoft.com/ab39a092-1343-46e2-b0f1-6a3f91155aa6)
extension for Visual Studio which enables some features used by this project.
