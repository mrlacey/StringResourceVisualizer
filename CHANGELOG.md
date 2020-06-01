# Road map

- [ ] Performance improvements
- [ ] Support for wrapped lines
- [ ] RESW support
- [ ] [Open to suggestions...](https://github.com/mrlacey/StringResourceVisualizer/issues/new)

Features that have a checkmark are complete and available for
download in the
[CI build](http://vsixgallery.com/extension/StringResourceVisualizer.a05f89b1-98f8-4b37-8f84-4fdebc44aa25/).

# Change log

These are the changes to each version that has been released
on the official Visual Studio extension gallery.

## 1.6

- [x] Add support for ILocalizer in *.cshtml files

## 1.5

- [x] Allow specifying a 'preferred culture' to use when looking up strings to display.
- [x] Small perf improvements.

## 1.4

- [x] Only pad between lines when definitely something to show
- [x] Internal optimization of referenced resources


## 1.3

- [x] Support VS 2019
- [x] Don't show adorner if usage is in a collapsed section
- [x] Support showing multiple resources in the same line of code
- [x] Prevent resources overlapping if on the same line
- [x] Truncate multi-line strings in adornment and indicate truncation/wrapping
- [x] Use output pane for logging details, not the status bar

## 1.2

- [x] Improve performance and reduce CPU usage
- [x] Support resource files added to the project once opened

## 1.1

- [x] Support VB.Net
- [x] Identify resources followed by a comma or curly brace
- [x] Fix crash when switching branches or opening certain projects
- [x] Enable working with any project type or solution structure

## 1.0

- [x] Initial release
