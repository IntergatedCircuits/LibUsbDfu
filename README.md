# LibUsbDfu

[![License](http://img.shields.io/:license-mit-blue.svg?style=flat-square)](http://badges.mit-license.org)

**LibUsbDfu** is a C# USB DFU firmware upgrade utility using [LibUsbDotNet][LibUsbDotNet],
[DeviceProgramming][DeviceProgramming] and [Mono.Options][Mono.Options].
The program performs the entire DFU upgrade procedure - booting to update mode, downloading the firmware and manifesting it - by a single command.
It accepts `.hex`, `.s19` and `.dfu` image file formats as input.

## Example
```
LibUsbDfu -d 483:5740 -v 1.12 -i "newfw.hex"
```
***Note:*** The USB VID:PID and version are overwritten from the `.dfu` file if that format is provided.

## Footnotes
Unlike HID or MSC, the USB DFU class isn't recognized natively by today's OSes, therefore the interface driver must be created and distributed for each device.
[LibUsbDotNet] is used as an underlying USB device interface, as it provides the most direct USB access on the widest platform range.

[DeviceProgramming]: https://github.com/IntergatedCircuits/DeviceProgramming
[LibUsbDotNet]: https://github.com/LibUsbDotNet/LibUsbDotNet
[Mono.Options]: https://github.com/xamarin/XamarinComponents/tree/master/XPlat/Mono.Options
