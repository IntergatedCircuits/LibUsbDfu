using System;
using System.Runtime.InteropServices;

namespace DeviceProgramming.Dfu
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FunctionalDescriptor
    {
        /// <summary>
        /// Total size of this structure in bytes.
        /// </summary>
        public static readonly int Size = Marshal.SizeOf(typeof(FunctionalDescriptor));
        private byte bLength;

        /// <summary>
        ///  Descriptor type.
        /// </summary>
        public static readonly int Type = 0x21;
        private byte bDescriptorType;

        [Flags]
        public enum Attributes : byte
        {
            None = 0,
            CanDnload = 1,
            CanUpload = 2,
            ManifestationTolerant = 4,
            WillDetach = 8
        };
        private Attributes bmAttributes;

        /// <summary>
        /// download capable
        /// </summary>
        public bool CanDownload { get { return (bmAttributes & Attributes.CanDnload) != 0; } }

        /// <summary>
        /// upload capable
        /// </summary>
        public bool CanUpload { get { return (bmAttributes & Attributes.CanUpload) != 0; } }

        /// <summary>
        /// device is able to communicate via USB after Manifestation phase.
        /// </summary>
        public bool ManifestationTolerant { get { return (bmAttributes & Attributes.ManifestationTolerant) != 0; } }

        /// <summary>
        /// device will perform a bus detach-attach sequence when it receives a DFU_DETACH request.
        /// The host must not issue a USB Reset.
        /// </summary>
        public bool WillDetach { get { return (bmAttributes & Attributes.WillDetach) != 0; } }

        /// <summary>
        /// Time, in milliseconds, that the device will wait after receipt of the DFU_DETACH request.
        /// If this time elapses without a USB reset, then the device will terminate the Reconfiguration phase
        /// and revert back to normal operation. This represents the maximum time that the device can wait
        /// (depending on its timers, etc.). The host may specify a shorter timeout in the DFU_DETACH request.
        /// </summary>
        public int DetachTimeout { get { return wDetachTimeout; } }
        private ushort wDetachTimeout;

        /// <summary>
        /// Maximum number of bytes that the device can accept per control-write transaction.
        /// </summary>
        public int TransferSize { get { return wTransferSize; } }
        private ushort wTransferSize;

        /// <summary>
        /// Numeric expression identifying the version of the DFU Specification release.
        /// </summary>
        public Version DfuVersion { get { return new Version(bcdDFUVersion >> 8, bcdDFUVersion & 0xFF); } }
        public readonly ushort bcdDFUVersion;

        public FunctionalDescriptor(byte[] data, int offset = 0)
        {
            if ((data.Length - offset) < Size)
            {
                throw new ArgumentException("Invalid FunctionalDescriptor buffer size", "data");
            }

            this.bLength = data[offset];
            if (this.bLength != Size)
            {
                throw new ArgumentException("Invalid FunctionalDescriptor bLength", "data");
            }

            this.bDescriptorType = data[offset + 1];
            if (this.bDescriptorType != Type)
            {
                throw new ArgumentException("Invalid FunctionalDescriptor bDescriptorType", "data");
            }

            this.bmAttributes = (Attributes)data[offset + 2];
            this.wDetachTimeout = BitConverter.ToUInt16(data, offset + 3);
            this.wTransferSize = BitConverter.ToUInt16(data, offset + 5);
            this.bcdDFUVersion = BitConverter.ToUInt16(data, offset + 7);
        }

        public FunctionalDescriptor(Attributes attr, ushort detachTimeout, ushort transferSize, ushort dfuVersion)
        {
            this.bLength = (byte)Size;
            this.bDescriptorType = (byte)Type;
            this.bmAttributes = attr;
            this.wDetachTimeout = detachTimeout;
            this.wTransferSize = transferSize;
            this.bcdDFUVersion = dfuVersion;
        }
    }
}
