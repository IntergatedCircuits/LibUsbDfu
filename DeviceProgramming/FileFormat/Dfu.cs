using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeviceProgramming.Dfu;
using System.IO;
using System.Runtime.InteropServices;
using DeviceProgramming.Memory;

namespace DeviceProgramming.FileFormat
{
    public class Dfu
    {
        /// <summary>
        /// Tests if the file extension is associated with this file format.
        /// </summary>
        /// <param name="ext">The file extension, starting with the dot</param>
        /// <returns>True if the file extension is supported, false otherwise</returns>
        public static bool IsExtensionSupported(string ext)
        {
            return ext.Equals(".dfu", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The purpose of the DFU suffix is to allow the operating system in general,
        /// and the DFU operator interface application in particular, to have a-priori knowledge of
        /// whether a firmware download is likely to complete correctly. In other words, these bytes
        /// allow the host software to detect and prevent attempts to download incompatible firmware.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Suffix
        {
            /// <summary>
            /// Total size of this structure in bytes.
            /// </summary>
            public static readonly int Size = Marshal.SizeOf(typeof(Suffix));

            /// <summary>
            /// The valid suffix signature.
            /// </summary>
            public static readonly string Signature = "UFD";

            /// <summary>
            /// The release number of the device associated with this file.
            /// Either FFFFh or a BCD firmware release or version number.
            /// </summary>
            public readonly ushort bcdDevice;

            /// <summary>
            /// The product ID associated with this file. Either FFFFh or must match device’s product ID.
            /// </summary>
            public readonly ushort idProduct;

            /// <summary>
            /// The vendor ID associated with this file. Either FFFFh or must match device’s vendor ID.
            /// </summary>
            public readonly ushort idVendor;

            /// <summary>
            /// DFU specification number.
            /// </summary>
            public readonly ushort bcdDFU;

            /// <summary>
            /// The unique DFU signature field.
            /// </summary>
            public string sDfuSignature { get { return new string(ucDfuSignature); } }
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public readonly char[] ucDfuSignature;

            /// <summary>
            /// The length of this DFU suffix including dwCRC.
            /// </summary>
            public readonly byte bLength;

            /// <summary>
            /// The CRC of the entire file, excluding dwCRC.
            /// </summary>
            public readonly uint dwCRC;
        }

        #region DFU SE file section headers
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SePrefix
        {
            /// <summary>
            /// Total size of this structure in bytes.
            /// </summary>
            public static readonly int Size = Marshal.SizeOf(typeof(SePrefix));

            /// <summary>
            /// The valid prefix signature.
            /// </summary>
            public static readonly string Signature = "DfuSe";

            /// <summary>
            /// The unique signature field.
            /// </summary>
            public string sSignature { get { return new string(ucSignature); } }
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public readonly char[] ucSignature;

            /// <summary>
            /// DfuSe file type version (== 1).
            /// </summary>
            public readonly byte bVersion;

            /// <summary>
            /// Size of the firmware image
            /// </summary>
            public readonly uint dwImageSize;

            /// <summary>
            /// The number of targets defined in the file
            /// </summary>
            public readonly byte bTargets;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SeTargetPrefix
        {
            /// <summary>
            /// Total size of this structure in bytes.
            /// </summary>
            public static readonly int Size = Marshal.SizeOf(typeof(SeTargetPrefix));

            /// <summary>
            /// The valid prefix signature.
            /// </summary>
            public static readonly string Signature = "Target";

            /// <summary>
            /// The unique signature field.
            /// </summary>
            public string sSignature { get { return new string(ucSignature); } }
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public readonly char[] ucSignature;

            /// <summary>
            /// Specifies the alternate setting of the DFU interface to select to flash the current target
            /// </summary>
            public readonly byte bAlternateSetting;

            /// <summary>
            /// Flag to indicate if the target is named.
            /// </summary>
            public readonly bool bTargetNamed;

            /// <summary>
            /// Descriptive name of the target.
            /// </summary>
            public string sTargetName
            {
                get
                {
                    // need to get rid of random memory after \0
                    var encoding = Encoding.ASCII;
                    return encoding.GetString(encoding.GetBytes(ucTargetName.TakeWhile(c => !c.Equals('\0')).ToArray()));
                }
            }
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 255)]
            public readonly char[] ucTargetName;

            /// <summary>
            /// Total size of the target data.
            /// </summary>
            public readonly uint dwTargetSize;

            /// <summary>
            /// Number of memory elements (segments).
            /// </summary>
            public readonly uint dwNbElements;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SeElementHeader
        {
            /// <summary>
            /// Total size of this structure in bytes.
            /// </summary>
            public static readonly int Size = Marshal.SizeOf(typeof(SeElementHeader));

            public readonly uint dwElementAddress;

            public readonly uint dwElementSize;
        }
        #endregion

        /// <summary>
        /// Simple helper class to hold the maximal amount of information that can be encoded into a DFU file.
        /// </summary>
        public class FileContent
        {
            public Device.Identification DeviceInfo { get; private set; }
            public Dictionary<byte, NamedMemory> ImagesByAltSetting { get; private set; }

            public FileContent(Device.Identification devInfo)
            {
                DeviceInfo = devInfo;
                ImagesByAltSetting = new Dictionary<byte, NamedMemory>();
            }
        }

        private static Suffix ReadSuffix(BinaryReader reader)
        {
            // this is the part of the file that the CRC is calculated on
            byte[] content = new byte[reader.BaseStream.Length - 4];
            reader.BaseStream.Position = 0;
            reader.Read(content, 0, content.Length);

            byte[] suffixdata = new byte[Suffix.Size];
            reader.BaseStream.Position = reader.BaseStream.Length - Suffix.Size;
            reader.Read(suffixdata, 0, Suffix.Size);
            var suffix = suffixdata.ToStruct<Suffix>();

            // verify suffix
            if (suffix.dwCRC != Crc32.Calculate(content))
            {
                throw new ArgumentException("The selected dfu file has invalid CRC.");
            }
            if (suffix.bLength < Suffix.Size)
            {
                throw new ArgumentException("The selected dfu file has invalid suffix length.");
            }
            if (suffix.sDfuSignature != Suffix.Signature)
            {
                throw new ArgumentException("The selected dfu file has invalid suffix signature.");
            }

            return suffix;
        }

        /// <summary>
        /// Extracts the device and firmware version information of a DFU file.
        /// </summary>
        /// <param name="filepath">Path to the DFU file</param>
        /// <returns>The device and image version information</returns>
        public static Device.Identification ParseFileInfo(string filepath)
        {
            using (BinaryReader reader = new BinaryReader(File.Open(filepath, FileMode.Open)))
            {
                var suffix = ReadSuffix(reader);
                return new Device.Identification(suffix);
            }
        }

        /// <summary>
        /// Extracts the contents of a DFU file.
        /// </summary>
        /// <param name="filepath">Path to the DFU file</param>
        /// <returns>The device and memory image information</returns>
        public static FileContent ParseFile(string filepath)
        {
            using (BinaryReader reader = new BinaryReader(File.Open(filepath, FileMode.Open)))
            {
                var suffix = ReadSuffix(reader);
                var devInfo = new Device.Identification(suffix);
                var fc = new FileContent(devInfo);

                // remove the suffix from the contents
                byte[] content = new byte[reader.BaseStream.Length - suffix.bLength];
                reader.BaseStream.Position = 0;
                reader.Read(content, 0, content.Length);

                // if the protocol version is according to USB spec
                if (fc.DeviceInfo.DfuVersion <= Protocol.LatestVersion)
                {
                    // no name nor address is stored in the file, use whatever
                    var mem = new NamedMemory("default");

                    // the rest of the contents is entirely the firmware
                    mem.TryAddSegment(new Segment(~0ul, content));
                    fc.ImagesByAltSetting.Add(0, mem);
                }
                // if the protocol version is according to STMicroelectronics Extension
                else if (fc.DeviceInfo.DfuVersion == Protocol.SeVersion)
                {
                    long fileOffset = 0;
                    BufferAllocDelegate getDataChunk = (chunkSize) =>
                    {
                        byte[] chunkData = new byte[chunkSize];
                        Array.Copy(content, fileOffset, chunkData, 0, chunkSize);
                        fileOffset += chunkSize;
                        return chunkData;
                    };

                    // DfuSe prefix
                    var prefix = getDataChunk(SePrefix.Size).ToStruct<SePrefix>();

                    if (prefix.sSignature != SePrefix.Signature)
                    {
                        throw new ArgumentException("The selected dfu file has invalid DfuSe prefix signature.");
                    }

                    // there are a number of targets, each of which is mapped to a different alternate setting
                    for (int tt = 0; tt < prefix.bTargets; tt++)
                    {
                        // image target prefix
                        var target = getDataChunk(SeTargetPrefix.Size).ToStruct<SeTargetPrefix>();

                        if (target.sSignature != SeTargetPrefix.Signature)
                        {
                            throw new ArgumentException("The selected dfu file has invalid DfuSe target prefix signature.");
                        }
                        // TODO
                        //if (!target.bTargetNamed)

                        var nmem = new NamedMemory(target.sTargetName);

                        // each target contains a number of elements (memory segments)
                        for (uint e = 0; e < target.dwNbElements; e++)
                        {
                            var elem = getDataChunk(SeElementHeader.Size).ToStruct<SeElementHeader>();
                            nmem.TryAddSegment(new Segment(elem.dwElementAddress, getDataChunk(elem.dwElementSize)));
                        }

                        // the target's alternate setting is the dictionary index
                        fc.ImagesByAltSetting.Add(target.bAlternateSetting, nmem);
                    }

                    // no leftover data is allowed
                    if ((fileOffset + suffix.bLength) != reader.BaseStream.Length)
                    {
                        throw new ArgumentException(String.Format("The selected dfu file has unprocessed data starting at {0}.", fileOffset));
                    }
                }
                else
                {
                    throw new ArgumentException("The selected dfu file has unsupported DFU specification version.");
                }

                return fc;
            }
        }

        private delegate byte[] BufferAllocDelegate(long size);
    }
}
