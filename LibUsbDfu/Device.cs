using System;
using System.Collections.Generic;
using LibUsbDotNet;
using DeviceProgramming.Dfu;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;
using System.Threading;

namespace LibUsbDfu
{
    public class Device : DeviceProgramming.Dfu.Device, IDisposable
    {
        private byte configIndex;
        private byte interfaceIndex;
        private UsbDevice device;
        private Identification info;
        private FunctionalDescriptor dfuDesc;

        public override FunctionalDescriptor DfuDescriptor { get { return dfuDesc; } }
        public override Identification Info { get { return info; } }
        private UsbConfigInfo ConfigInfo { get { return device.Configs[configIndex]; } }
        private UsbInterfaceInfo InterfaceInfo { get { return ConfigInfo.InterfaceInfoList[interfaceIndex]; } }
        private byte InterfaceID { get { return InterfaceInfo.Descriptor.InterfaceID; } }

        public override string ToString()
        {
            return device.UsbRegistryInfo.DevicePath;
        }

        /// <summary>
        /// Disposes the underlying non-managed resources of the device.
        /// </summary>
        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // called by Dispose()
                // free managed resources (disposable member objects, expensive resources)
                var d = device as IDisposable;
                if (d != null)
                {
                    d.Dispose();
                }
            }
            else
            {
                // called from a ~finalizer()
            }

            // free unmanaged resources
        }

        /// <summary>
        /// Gets one (or none) DFU device from the device list with the specified parameters.
        /// (If no exact match found, a second round of search uses only the Vendor ID.)
        /// </summary>
        /// <param name="deviceList">The device list to use</param>
        /// <param name="vid">Vendor ID of the USB device</param>
        /// <param name="pid">Product ID of the USB device</param>
        /// <returns>The first DFU device that matched the parameters</returns>
        public static Device OpenFirst(UsbRegDeviceList deviceList, int vid, int pid)
        {
            var registries = deviceList.FindAll(new UsbDeviceFinder(vid, pid));
            var devs = OpenAll(registries);

            // it's possible that the device is already in DFU mode, in which case only the VID has to match
            if (devs.Count == 0)
            {
                registries = deviceList.FindAll(new UsbDeviceFinder(vid));
                devs = OpenAll(registries);
            }

            if (devs.Count == 0)
            {
                throw new ArgumentException(String.Format("No DFU device was found with {0:X}:{1:X}", vid, pid));
            }
            // if more than one are connected, print a warning, and use the first on the list
            else if (devs.Count > 1)
            {
                for (int i = 1; i < devs.Count; i++)
                {
                    devs[i].Close();
                }
            }
            return devs[0];
        }

        /// <summary>
        /// Finds and opens all DFU devices from the device list.
        /// </summary>
        /// <param name="deviceList">The device list to use</param>
        /// <returns>A list of opened DFU devices</returns>
        public static List<Device> OpenAll(UsbRegDeviceList deviceList)
        {
            List<Device> devs = new List<Device>();
            foreach (UsbRegistry item in deviceList)
            {
                Device dev;
                if (Device.TryOpen(item, out dev))
                {
                    devs.Add(dev);
                }
            }
            return devs;
        }

        /// <summary>
        /// Attempts to open a USB registry as a USB DFU device.
        /// </summary>
        /// <param name="registry">The input USB registry of a connected device</param>
        /// <param name="dfuDevice">The opened DFU device in case of success</param>
        /// <returns>True if the DFU device is successfully opened</returns>
        public static bool TryOpen(UsbRegistry registry, out Device dfuDevice)
        {
            dfuDevice = null;
            UsbDevice dev;
            byte cfIndex = 0;
            byte ifIndex = 0;

            if (!registry.Open(out dev))
            {
                return false;
            }

            var confInfo = dev.Configs[cfIndex];

            // This is a "whole" USB device. Before it can be used,
            // the desired configuration and interface must be selected.
            IUsbDevice usbDevice = dev as IUsbDevice;
            if (usbDevice != null)
            {
                // Select config
                usbDevice.SetConfiguration(confInfo.Descriptor.ConfigID);
            }

            // find DFU interface
            for (ifIndex = 0; ifIndex < confInfo.InterfaceInfoList.Count; ifIndex++)
            {
                var iface = confInfo.InterfaceInfoList[ifIndex];

                if (!IsDfuInterface(iface))
                {
                    continue;
                }

                if (usbDevice != null)
                {
                    // Claim interface
                    usbDevice.ClaimInterface(iface.Descriptor.InterfaceID);
                }
                break;
            }

            try
            {
                if (ifIndex == confInfo.InterfaceInfoList.Count)
                {
                    throw new ArgumentException("The device doesn't have valid DFU interface");
                }
                dfuDevice = new Device(dev, cfIndex, ifIndex);
                return true;
            }
            catch (Exception)
            {
                var d = dev as IDisposable;
                d.Dispose();
                return false;
            }
        }

        private Device(UsbDevice dev, byte conf, byte interf)
        {
            this.configIndex = conf;
            this.interfaceIndex = interf;
            this.device = dev;

            this.dfuDesc = new FunctionalDescriptor(InterfaceInfo.CustomDescriptors[0]);

            this.info = new Identification((ushort)device.Info.Descriptor.VendorID,
                (ushort)device.Info.Descriptor.ProductID,
                (ushort)device.Info.Descriptor.BcdDevice,
                dfuDesc.bcdDFUVersion);
        }

        private static bool IsDfuInterface(UsbInterfaceInfo iinfo)
        {
            return ((byte)iinfo.Descriptor.Class == InterfaceClass) &&
                (iinfo.Descriptor.SubClass == InterfaceSubClass) &&
                ((iinfo.Descriptor.Protocol == InterfaceProtocol_Runtime) || (iinfo.Descriptor.Protocol == InterfaceProtocol_DFU)) &&
                (iinfo.CustomDescriptors.Count == 1) &&
                (iinfo.CustomDescriptors[0].Length == FunctionalDescriptor.Size);
        }

        protected override byte NumberOfAlternateSettings
        {
            get { return (byte)ConfigInfo.InterfaceInfoList.Count; }
        }

        protected override byte AlternateSetting
        {
            get
            {
                if (true)
                {
                    // use available API when possible
                    byte alt;
                    device.GetAltInterfaceSetting(InterfaceID, out alt);
                    return alt;
                }
                else
                {
                    // fallback to raw USB transfer
                    byte[] buffer = new byte[1];
                    var rtype = new RequestType(UsbEndpointDirection.EndpointIn,
                        UsbRequestType.TypeStandard, UsbRequestRecipient.RecipInterface);
                    var s = new UsbSetupPacket(rtype, (byte)UsbStandardRequest.GetInterface,
                        0, InterfaceID, buffer.Length);
                    ControlTransfer(s, buffer, buffer.Length);
                    return buffer[0];
                }
            }
            set
            {
                // save the trouble when possible
                if (AlternateSetting == value)
                    return;

                if (device is IUsbDevice)
                {
                    // use available API when possible
                    var usbdev = device as IUsbDevice;
                    usbdev.SetAltInterface(value);
                }
                else
                {
                    // fallback to raw USB transfer
                    var rtype = new RequestType(UsbEndpointDirection.EndpointOut,
                        UsbRequestType.TypeStandard, UsbRequestRecipient.RecipInterface);
                    var s = new UsbSetupPacket(rtype, (byte)UsbStandardRequest.SetInterface,
                        value, InterfaceID, 0);
                    ControlTransfer(s, null, 0);
                }
            }
        }

        protected override byte iAlternateSetting(byte altSetting)
        {
            return (byte)ConfigInfo.InterfaceInfoList[altSetting].Descriptor.StringIndex;
        }

        protected override string GetString(byte iString)
        {
            string result;
            if (!device.GetString(out result, device.Info.CurrentCultureLangID, iString))
            {
                result = String.Empty;
            }
            return result.TrimEnd(new char[] { '\0' });
        }

        protected override void ControlTransfer(Request request, ushort value = 0)
        {
            var rtype = new RequestType(UsbEndpointDirection.EndpointOut,
                UsbRequestType.TypeClass, UsbRequestRecipient.RecipInterface);
            var s = new UsbSetupPacket(rtype, (byte)request, value, InterfaceID, 0);
            ControlTransfer(s, null, 0);
        }

        protected override void ControlTransfer(Request request, ushort value, byte[] outdata)
        {
            var rtype = new RequestType(UsbEndpointDirection.EndpointOut,
                UsbRequestType.TypeClass, UsbRequestRecipient.RecipInterface);
            var s = new UsbSetupPacket(rtype, (byte)request, value, InterfaceID, outdata.Length);
            ControlTransfer(s, outdata, outdata.Length);
        }

        protected override void ControlTransfer(Request request, ushort value, ref byte[] indata)
        {
            var rtype = new RequestType(UsbEndpointDirection.EndpointIn,
                UsbRequestType.TypeClass, UsbRequestRecipient.RecipInterface);
            var s = new UsbSetupPacket(rtype, (byte)request, value, InterfaceID, indata.Length);
            ControlTransfer(s, indata, indata.Length);
        }

        protected void ControlTransfer(UsbSetupPacket setupPacket, object buffer, int bufferLength)
        {
            int retries = 0;
            do
            {
                int lengthTransferred;
                // check for transfer success:
                // 1. that the request isn't STALLed / NACKed
                // 2. that the correct number of bytes were received / transmitted
                if (device.ControlTransfer(ref setupPacket, buffer, bufferLength, out lengthTransferred) &&
                    (lengthTransferred == bufferLength))
                {
                    return;
                }
                // device/bus error, let's hope it's transitional only
                Thread.Sleep(10);
            } while (retries++ < 10);

            // elevate persistent error
            throw new ApplicationException(String.Format("Failed to perform control transfer ({0}) to target {1}", setupPacket, this));
        }

        public override void Close()
        {
            device.Close();
        }

        public override bool IsOpen()
        {
            return device.IsOpen;
        }

        protected override void BusReset()
        {
            if (device is IUsbDevice)
            {
                try
                {
                    // use available API when possible
                    var usbdev = device as IUsbDevice;
                    usbdev.ResetDevice();
                }
                catch (Exception)
                {
                    // ignore exceptions due to missing device
                }
                finally
                {
                    // close the device after reset
                    Close();
                }
            }
            else
            {
                throw new NotImplementedException("The underlying USB device driver doesn't support bus reset.");
            }
        }
    }
}
