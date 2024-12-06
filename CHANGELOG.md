# Road map

- [ ] Performance improvements
- [ ] Support for wrapped lines
- [ ] RESW support
- [ ] [Open to suggestions...](https://github.com/mrlacey/StringResourceVisualizer/issues/new)

Features that have a checkmark are complete and available for download in the
[CI build](http://vsixgallery.com/extension/StringResourceVisualizer.a05f89b1-98f8-4b37-8f84-4fdebc44aa25/).

# Change log

These are the changes to each version that has been released
on the official Visual Studio extension gallery.

## 1.23.1

- [x] Increased logging on exceptions to try and identify the cause of resource load failures.


## 1.23

- [x] Fix for possible crash when opening files not as part of a project.

## 1.22

- [x] Update minimum supported version of VS to 17.10
- [x] Address security vulnerabilities in dependencies.
- [x] Add basic usage telemetry.

## 1.21

- [x] Avoid locking UI when parsing the solution.

## 1.20

- [x] Startup performance improvements.

## 1.19

- [x] Improve support for Razor files.
- [x] Handle changing solutions without restarting VS.
- [x] Improve extension loading.
- [x] Support opening .csproj files directly (not as part of a solution.)

## 1.18

- [x] Small performance (responsiveness) improvement.

## 1.17

- [x] Fix exception when reloading a solution while the package is loading.

## 1.16

- [x] Added tracking to try and identify the cause of performance issues.

## 1.15

- [x] Improve handling of invalid code in editor.

## 1.14

- [x] Handle internal exception scenario.

## 1.13

- [x] Support adjusting the spacing around the text indicator.

## 1.12

- [x] Increase spacing between indicator and the line above.

## 1.11

- [x] Fix for possible errors when opening some (more) document types.

## 1.10

- [x] Fix for handling some document types in VS2022.

## 1.9

- [x] Fix occassional crash when opening some projects.

## 1.8

- [x] Add Sponsor Request hint.
- [x] Support VS2022.

## 1.7

- [x] Drop support for VS2017 😢 Sorry, I can't get it to compile and work there correctly anymore.

## 1.6

- [x] Add support for ILocalizer in *.cshtml & *.cs files
- [x] Allow ILocalizer keys to be constants
- [x] Support using aliases

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
