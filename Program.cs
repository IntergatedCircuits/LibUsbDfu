using DeviceProgramming;
using DeviceProgramming.Memory;
using DeviceProgramming.FileFormat;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LibUsbDfu
{
    class Program
    {
        private static Regex UsbIdRegex = new Regex
            (@"^(?<vid>[a-fA-F0-9]{1,4}):(?<pid>[a-fA-F0-9]{1,4})$", RegexOptions.Compiled);

        private static Regex VersionRegex = new Regex
            (@"^(?<major>[0-9]{1,2})\.(?<minor>[0-9]{1,2})$", RegexOptions.Compiled);

        static void Main(string[] args)
        {
            string filePath = null;
            string fileExt = null;
            bool help = false;
            bool isDfuFile = false;
            // Vendor and Product IDs are required, set them to invalid
            int vid = 0x10000, pid = 0x10000;
            // version is optional, FF means forced update
            int vmajor = 0xFF, vminor = 0xFF;

            // parameter parsing
            OptionSet optionSet = new OptionSet()
                .Add("?|help|h",
                    "Prints out the options.", option => help = option != null)
                .Add("i|image=",
                   "Path of the image file to download. Supported formats are DFU, Intel HEX and Motorola SREC.",
                   option => filePath = option)
                .Add("d|device=",
                   "USB Device Vendor and Product ID in XXXX:XXXX format. Ignored if the file format is DFU.",
                   option =>
                   {
                       var result = UsbIdRegex.Match(option);
                       if (!result.Success)
                       {
                           help = true;
                       }
                       else
                       {
                           vid = UInt16.Parse(result.Groups["vid"].Value, NumberStyles.HexNumber);
                           pid = UInt16.Parse(result.Groups["pid"].Value, NumberStyles.HexNumber);
                       }
                   })
                .Add("v|version=",
                   "Firmware version in D.D format. Ignored if the file format is DFU.",
                   option =>
                   {
                       var result = VersionRegex.Match(option);
                       if (!result.Success)
                       {
                           help = true;
                       }
                       else
                       {
                           vmajor = Byte.Parse(result.Groups["major"].Value);
                           vminor = Byte.Parse(result.Groups["minor"].Value);
                       }
                   });

            try
            {
                // try to get required arguments
                optionSet.Parse(args);
                fileExt = Path.GetExtension(filePath);
                isDfuFile = Dfu.IsExtensionSupported(fileExt);
                if (!isDfuFile && ((vid > 0xFFFF) || (pid > 0xFFFF)))
                {
                    help = true;
                }
            }
            catch (Exception)
            {
                help = true;
            }

            if (help)
            {
                // print help text and exit
                Console.Error.WriteLine("Usage:");
                optionSet.WriteOptionDescriptions(Console.Error);
                Environment.Exit(-1);
            }

            // DFU device event printers
            int prevCursor = -1;
            EventHandler<ProgressChangedEventArgs> printDownloadProgress = (obj, e) =>
            {
                if (prevCursor == Console.CursorTop)
                {
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                }
                Console.WriteLine("Download progress: {0}%", e.ProgressPercentage);
                prevCursor = Console.CursorTop;
            };
            EventHandler<ErrorEventArgs> printDevError = (obj, e) =>
            {
                Console.Error.WriteLine("The DFU device reported the following error: {0}", e.GetException().Message);
            };

            Device device = null;
            try
            {
                Version fileVer = new Version(vmajor, vminor);
                Dfu.FileContent dfuFileData = null;
                RawMemory memory = null;

                // find the matching file parser by extension
                if (isDfuFile)
                {
                    dfuFileData = Dfu.ParseFile(filePath);
                    Console.WriteLine("DFU image parsed successfully.");

                    // DFU file specifies VID, PID and version, so override any arguments
                    vid = dfuFileData.DeviceInfo.VendorId;
                    pid = dfuFileData.DeviceInfo.ProductId;
                    fileVer = dfuFileData.DeviceInfo.ProductVersion;
                }
                else if (IntelHex.IsExtensionSupported(fileExt))
                {
                    memory = IntelHex.ParseFile(filePath);
                    Console.WriteLine("Intel HEX image parsed successfully.");
                }
                else if (SRecord.IsExtensionSupported(fileExt))
                {
                    memory = SRecord.ParseFile(filePath);
                    Console.WriteLine("SRecord image parsed successfully.");
                }
                else
                {
                    throw new ArgumentException("Image file format not recognized.");
                }

                // find the DFU device
                device = Device.OpenFirst(UsbDevice.AllDevices, vid, pid);
                device.DeviceError += printDevError;

                if (isDfuFile)
                {
                    // verify protocol version
                    if (dfuFileData.DeviceInfo.DfuVersion != device.DfuDescriptor.DfuVersion)
                    {
                        throw new InvalidOperationException(String.Format("DFU file version {0} doesn't match device DFU version {1}",
                            dfuFileData.DeviceInfo.DfuVersion,
                            device.DfuDescriptor.DfuVersion));
                    }
                }

                // if the device is in normal application mode, reconfigure it
                if (device.InAppMode())
                {
                    bool skipUpdate = fileVer <= device.Info.ProductVersion;

                    // skip update when it's deemed unnecessary
                    if (skipUpdate)
                    {
                        Console.WriteLine("The device is already up-to-date (version {0}), skipping update (version {1}).",
                            device.Info.ProductVersion,
                            fileVer);
                        return;
                    }

                    Console.WriteLine("Device found in application mode, reconfiguring device to DFU mode...");
                    device.Reconfigure();

                    // in case the device detached, we must find the DFU mode device
                    if (!device.IsOpen())
                    {
                        device.DeviceError -= printDevError;
                        device = Device.OpenFirst(UsbDevice.AllDevices, vid, pid);
                        device.DeviceError += printDevError;
                    }
                }
                else
                {
                    Console.WriteLine("Device found in DFU mode.");
                }

                // perform upgrade
                device.DownloadProgressChanged += printDownloadProgress;
                if (isDfuFile)
                {
                    device.DownloadFirmware(dfuFileData);
                }
                else
                {
                    device.DownloadFirmware(memory);
                }

                Console.WriteLine("Download successful, manifesting update...");
                device.Manifest();

                // TODO find device again to verify new version
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Device Firmware Upgrade failed with exception: {0}.", e.ToString());
                Environment.Exit(-1);
            }
            finally
            {
                if (device != null)
                {
                    device.Close();
                }
            }
        }
    }
}
