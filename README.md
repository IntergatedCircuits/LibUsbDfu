# LibUsbDfu

[![License](http://img.shields.io/:license-mit-blue.svg?style=flat-square)](http://badges.mit-license.org)

**LibUsbDfu** is a C# USB DFU firmware upgrade utility using [LibUsbDotNet][LibUsbDotNet] and [Mono.Options][Mono.Options].
The program performs the entire DFU upgrade procedure - booting to update mode, downloading the firmware and manifesting it - by a single command.
It accepts `.hex`, `.s19` and `.dfu` image file formats as input.

## Features
The DeviceProgramming project implements
* Generic classes for device firmware and memory layout representation (`DeviceProgramming.Memory`)
* Parsers for common firmware image file formats such as Intel HEX and Motorola SREC (`DeviceProgramming.FileFormats`)
* USB DFU class logic (`DeviceProgramming.Dfu`) and file parser (`DeviceProgramming.FileFormats.Dfu`)
supporting both the latest official USB specification (version 1.1) and the ST Microelectronics Extension (version 1.1a)

## Example
```
LibUsbDfu.Cli -d 483:5740 -v 1.12 -i "newfw.hex"
```
***Note:*** The USB VID:PID and version are overwritten from the `.dfu` file if that format is provided.

## Footnotes
Unlike HID or MSC, the USB DFU class isn't recognized natively by today's OSes, therefore the interface driver must be created and distributed for each device.
[LibUsbDotNet] is used as an underlying USB device interface, as it provides the most direct USB access on the widest platform range.

## Development

Since LibUsbDfu depends on DeviceProgramming package, development update of the latter requires the use of a local NuGet Feed.

1. Set up a folder for the local NuGet Feed, e.g. `C:\NuGetLocalFeed`,
configurable in Visual Studio under `Tools` -> `Options` -> `NuGet Package Manager` -> `Package Sources`,
or via `dotnet nuget add source C:\NuGetLocalFeed -n LocalFeed`
2. After the changes are made, update the package version, and deploy the package to the local feed:
`dotnet pack DeviceProgramming/DeviceProgramming.csproj -c Release -o C:\NuGetLocalFeed`
3. Update the dependent package version in `LibUsbDfu.csproj` file, and build it.

[LibUsbDotNet]: https://github.com/LibUsbDotNet/LibUsbDotNet
[Mono.Options]: https://github.com/xamarin/XamarinComponents/tree/master/XPlat/Mono.Options
