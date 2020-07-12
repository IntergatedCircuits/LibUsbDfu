using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibUsbDotNet;
using DeviceProgramming.Dfu;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;

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

        /// <summary>
        /// Disposes the underlying non-managed resources of the device.
        /// </summary>
        public void Dispose()
        {
            var d = device as IDisposable;
            if (d != null)
            {
                d.Dispose();
            }
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
                    var s = new UsbSetupPacket(0x20, 0x0a, 0, InterfaceID, 1);
                    byte[] buffer = new byte[1];
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
                    UsbSetupPacket s = new UsbSetupPacket(0x20, 0x0b, value, InterfaceID, 0);
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
                result = String.Empty;
            return result;
        }

        protected override void ControlTransfer(Request request, ushort value = 0)
        {
            UsbSetupPacket s = new UsbSetupPacket(0x21, (byte)request, value, InterfaceID, 0);
            ControlTransfer(s, null, 0);
        }

        protected override void ControlTransfer(Request request, ushort value, byte[] outdata)
        {
            UsbSetupPacket s = new UsbSetupPacket(0x21, (byte)request, value, InterfaceID, outdata.Length);
            ControlTransfer(s, outdata, outdata.Length);
        }

        protected override void ControlTransfer(Request request, ushort value, ref byte[] indata)
        {
            UsbSetupPacket s = new UsbSetupPacket(0xa1, (byte)request, value, InterfaceID, indata.Length);
            ControlTransfer(s, indata, indata.Length);
        }

        protected void ControlTransfer(UsbSetupPacket setupPacket, object buffer, int bufferLength)
        {
            int lengthTransferred;
            device.ControlTransfer(ref setupPacket, buffer, bufferLength, out lengthTransferred);
        }

        public override void Close()
        {
            device.Close();
            Dispose();
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
