using DeviceProgramming.Memory;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace DeviceProgramming.Dfu
{
    public abstract class Device
    {
        public static readonly byte InterfaceClass = 0xFE;
        public static readonly byte InterfaceSubClass = 0x01;
        public static readonly byte InterfaceProtocol_Runtime = 0x01;
        public static readonly byte InterfaceProtocol_DFU = 0x02;

        [Serializable]
        public class InvalidStateException : Exception
        {
            public InvalidStateException(State expected, State actual)
                : base(String.Format("Dfu state {0} wasn't reached, device returned {1}.", expected.ToString(), actual.ToString()))
            {
            }

            public InvalidStateException(State expected, State actual, string error)
                : base(String.Format("Dfu state {0} wasn't reached, device returned {1} (reason: {2}).", expected.ToString(), actual.ToString(), error))
            {
            }
        }

        /// <summary>
        /// Relevant device information that identifies the target of an update.
        /// </summary>
        public struct Identification
        {
            public readonly ushort VendorId;
            public readonly ushort ProductId;
            public readonly Version ProductVersion;
            public readonly Version DfuVersion;

            public Identification(ushort vendorId, ushort productId, ushort bcdProductVersion, ushort bcdDfuVersion)
            {
                VendorId = vendorId;
                ProductId = productId;
                ProductVersion = new Version(bcdProductVersion >> 8, bcdProductVersion & 0xff);
                DfuVersion = new Version(bcdDfuVersion >> 8, bcdDfuVersion & 0xff);
            }

            internal Identification(FileFormat.Dfu.Suffix dfuSuffix)
                : this(dfuSuffix.idVendor, dfuSuffix.idProduct, dfuSuffix.bcdDevice, dfuSuffix.bcdDFU)
            {
            }
        }

        /// <summary>
        /// This event fires during the download stage to indicate the relative progress in percentages.
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> DownloadProgressChanged = delegate { };

        /// <summary>
        /// This event fires when a device reported error is detected.
        /// </summary>
        public event EventHandler<ErrorEventArgs> DeviceError = delegate { };

        #region Abstract class interface
        /// <summary>
        /// Device identifying information, assembled from device IDs and DfuDescriptor.
        /// </summary>
        public abstract Identification Info { get; }

        /// <summary>
        /// The DFU Functional Descriptor, which is either parsed from the Config descriptor,
        /// or fetched with a GetDescriptor request to the DFU interface
        /// </summary>
        public abstract FunctionalDescriptor DfuDescriptor { get; }

        /// <summary>
        /// Returns the number of available alternate settings of the DFU interface.
        /// </summary>
        protected abstract byte NumberOfAlternateSettings { get; }

        /// <summary>
        /// Gets or sets the DFU interface's alternate setting.
        /// </summary>
        protected abstract byte AlternateSetting { get; set; }

        /// <summary>
        /// Returns the string index of the specified alternate selector.
        /// </summary>
        /// <param name="altSetting">The alternate selector index</param>
        /// <returns>The string index</returns>
        protected abstract byte iAlternateSetting(byte altSetting);

        /// <summary>
        /// Reads the USB device string for a specific string index.
        /// </summary>
        /// <param name="iString">USB device string index to read with</param>
        /// <returns>The string from the device</returns>
        protected abstract string GetString(byte iString);

        /// <summary>
        /// USB class-specific control transfer to the DFU interface, with 0 length.
        /// </summary>
        /// <param name="request">Class request code</param>
        /// <param name="value">Value field of the setup request</param>
        protected abstract void ControlTransfer(Request request, ushort value = 0);

        /// <summary>
        /// USB class-specific control transfer to the DFU interface, with OUT data.
        /// </summary>
        /// <param name="request">Class request code</param>
        /// <param name="value">Value field of the setup request</param>
        /// <param name="outdata">OUT data to send to the device</param>
        protected abstract void ControlTransfer(Request request, ushort value, byte[] outdata);

        /// <summary>
        /// USB class-specific control transfer to the DFU interface, with IN data.
        /// </summary>
        /// <param name="request">Class request code</param>
        /// <param name="value">Value field of the setup request</param>
        /// <param name="indata">IN data to fill from the device</param>
        protected abstract void ControlTransfer(Request request, ushort value, ref byte[] indata);

        // not used, only added for symmetry
        //public abstract void Open();

        /// <summary>
        /// Closes the device's DFU interface.
        /// </summary>
        public abstract void Close();

        /// <summary>
        /// Checks the status of the interface.
        /// </summary>
        /// <returns>True if the DFU interface is open</returns>
        public abstract bool IsOpen();

        /// <summary>
        /// Performs a USB bus reset for the device (also closing the device in the operation).
        /// Only required for devices which don't WillDetach, or are ManifestationTolerant.
        /// </summary>
        protected abstract void BusReset();
        #endregion

        #region DFU class requests
        public Status GetStatus()
        {
            byte[] indata = new byte[Status.Size];
            ControlTransfer(Request.GetStatus, 0, ref indata);
            return indata.ToStruct<Status>();
        }

        public void ClrStatus()
        {
            ControlTransfer(Request.ClrStatus);
        }

        public void Detach()
        {
            ControlTransfer(Request.Detach);
        }

        public void Dnload(ushort blockNumber, byte[] block)
        {
            ControlTransfer(Request.Dnload, blockNumber, block);
        }
        public void Dnload(ushort blockNumber, byte[] data, int startIndex, int length)
        {
            byte[] block = new byte[length];
            Array.Copy(data, startIndex, block, 0, length);
            ControlTransfer(Request.Dnload, blockNumber, block);
        }

        public byte[] Upload(ushort blockNumber, uint length)
        {
            var block = new byte[length];
            ControlTransfer(Request.Upload, blockNumber, ref block);
            return block;
        }

        public State GetState()
        {
            byte[] indata = new byte[1];
            ControlTransfer(Request.GetState, 0, ref indata);
            return (State)indata[0];
        }

        public void Abort()
        {
            ControlTransfer(Request.Abort);
        }
        #endregion

        /// <summary>
        /// Determines if the device is in an Application state, i.e. the device needs reconfiguration
        /// before any other DFU operation can be performed.
        /// </summary>
        /// <returns>True if DFU interface is in run-time application mode</returns>
        public bool InAppMode()
        {
            return GetState().IsAppState();
        }

        /// <summary>
        /// Reconfigures the device application to execute DFU operations.
        /// </summary>
        public void Reconfigure()
        {
            State state = GetState();

            if (!state.IsAppState())
                return;

            if (state == State.AppIdle)
            {
                try
                {
                    Detach();
                }
                catch (Exception)
                {
                    // exceptions due to broken pipe are allowed if the USB device detaches itself
                    if (DfuDescriptor.WillDetach)
                    {
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // When bit 3 in bmAttributes (bitWillDetach) is set the device will generate
            // a detach-attach sequence on the bus when it sees this request.
            if (DfuDescriptor.WillDetach)
            {
                Close();
            }
            // Otherwise, the device starts a timer counting the amount of time
            // specified, in milliseconds, in the wDetachTimeout field.
            // If the device detects a USB reset while this timer is running,
            // then DFU operating mode is enabled by the device
            else
            {
                BusReset();
            }

            // some additional sleep is introduced to amount to the OS detection and driver mounting delays
            Thread.Sleep(DfuDescriptor.DetachTimeout + 500);
        }

        /// <summary>
        /// Check for error status, and perform ClrStatus when in error state.
        /// DeviceError event provides information about the error.
        /// </summary>
        /// <returns>The device status after the error has been cleared.</returns>
        public Status ClearErrors()
        {
            Status status = GetStatus();

            // Any time the device detects an error and reports an error indication status to the host in the response
            // to a DFU_GETSTATUS request, it enters the dfuERROR state. The device cannot transition from the
            // dfuERROR state, after reporting any error status, until after it has received a DFU_CLRSTATUS
            // request. Upon receipt of DFU_CLRSTATUS, the device sets a status of OK and transitions to the
            // dfuIDLE state. Only then is it able to transition to other states.
            if (status.State == State.Error)
            {
                DeviceError(this, new ErrorEventArgs(new Exception(GetErrorString(status))));

                ClrStatus();

                status = GetStatus();
            }

            return status;
        }

        /// <summary>
        /// Reset the DFU interface to initial idle status.
        /// </summary>
        public void ResetToIdle()
        {
            var status = ClearErrors();

            // The DFU_ABORT request enables the host to exit from certain states and return to the DFU_IDLE
            // state. The device sets the OK status on receipt of this request.
            if (status.State.Abortable())
            {
                Abort();
                status = GetStatus();
            }

            VerifyState(status, State.Idle);
        }

        /// <summary>
        /// Manifest the downloaded application image and boot into it.
        /// </summary>
        public void Manifest()
        {
            // After the zero length DFU_DNLOAD request terminates the Transfer phase,
            // the device is ready to manifest the new firmware.
            Dnload(0, new byte[0]);

            try
            {
                // wait until manifesting completes
                Status status;
                for (status = GetStatus(); status.State == State.Manifest; status = GetStatus())
                {
                    Thread.Sleep(status.PollTimeout);
                }

                if (DfuDescriptor.ManifestationTolerant)
                {
                    // If the device enters dfuMANIFEST-SYNC (bitMainfestationTolerant = 1),
                    // then the host issues the DFU_GETSTATUS request, and the device enters the dfuIDLE state.
                    VerifyState(status, State.Idle);

                    // At that point, the host can perform another download, solicit an upload, or issue a USB reset
                    // to return the device to application run-time mode.
                    BusReset();
                }
                else
                {
                    // If, however, the device enters the dfuMANIFEST-WAIT-RESET state (bitManifestationTolerant = 0),
                    // then if bitWillDetach = 1 the device generates a detach-attach sequence on the bus,
                    VerifyState(status, State.ManifestWaitReset);

                    // otherwise (bitWillDetach = 0) the host must issue a USB reset to the device.
                    // After the bus reset the device will evaluate the firmware status and enter the appropriate mode.
                    if (!DfuDescriptor.WillDetach)
                    {
                        BusReset();
                    }
                }
            }
            catch (Exception)
            {
                // exceptions due to broken pipe are allowed if the USB device detaches itself
                if (!DfuDescriptor.ManifestationTolerant && DfuDescriptor.WillDetach)
                {
                }
                else
                {
                    throw;
                }
            }
            Close();
        }

        /// <summary>
        /// Download the firmware from a DFU file to the reconfigured DFU device.
        /// </summary>
        /// <param name="dfuFile">Parsed DFU file contents to download</param>
        public void DownloadFirmware(FileFormat.Dfu.FileContent dfuFile)
        {
            // verify protocol version
            if (dfuFile.DeviceInfo.DfuVersion != DfuDescriptor.DfuVersion)
            {
                throw new InvalidOperationException(String.Format("DFU file version {0} doesn't match device DFU version {1}",
                    dfuFile.DeviceInfo.DfuVersion,
                    DfuDescriptor.DfuVersion));
            }

            ResetToIdle();

            if (DfuDescriptor.DfuVersion <= Protocol.LatestVersion)
            {
                // only raw data can be downloaded
                Download(dfuFile.ImagesByAltSetting[0].Segments[0].Data);
            }
            else if (DfuDescriptor.DfuVersion == Protocol.SeVersion)
            {
                Download(dfuFile.ImagesByAltSetting);
            }
            else
            {
                throw new ArgumentException("The selected dfu file has unsupported DFU specification version.");
            }
        }

        /// <summary>
        /// Download the firmware image to the reconfigured DFU device.
        /// </summary>
        /// <param name="memory">Raw memory image to download</param>
        public void DownloadFirmware(RawMemory memory)
        {
            ResetToIdle();

            if (DfuDescriptor.DfuVersion <= Protocol.LatestVersion)
            {
                // only raw data can be downloaded
                Download(memory.Segments[0].Data);
            }
            else if (DfuDescriptor.DfuVersion == Protocol.SeVersion)
            {
                // parse memory layout of device and sort memory
                Download(SortMemoryByAltSetting(memory));
            }
            else
            {
                throw new ArgumentException("The selected dfu file has unsupported DFU specification version.");
            }
        }

        private void UpdateDownloadProgress(int totalLength, int transferredLength)
        {
            int percentage = 100 * transferredLength / totalLength;
            DownloadProgressChanged(this, new System.ComponentModel.ProgressChangedEventArgs(percentage, transferredLength));
        }

        private string GetErrorString(Status status)
        {
            string error;
            if (status.Error == Error.Vendor)
            {
                error = GetString(status.iString);
            }
            else
            {
                error = status.Error.ToString();
            }
            return error;
        }

        private void VerifyState(Status status, State expectedState)
        {
            if (status.State != expectedState)
            {
                if (status.State == State.Error)
                {
                    var e = new InvalidStateException(expectedState, status.State, GetErrorString(status));
                    DeviceError(this, new ErrorEventArgs(e));
                    throw e;
                }
                else
                {
                    throw new InvalidStateException(expectedState, status.State);
                }
            }
        }

        /// <summary>
        /// Upload a block of firmware from the reconfigured DFU device.
        /// </summary>
        /// <param name="length">The (maximum) size of memory to upload from the device.</param>
        /// <param name="blockNr">The starting block number of memory reading.</param>
        /// <returns>The memory dump from the device</returns>
        public byte[] UploadBlock(uint length, ushort blockNr = 0)
        {
            if (!DfuDescriptor.CanUpload)
            {
                throw new InvalidOperationException("The device doesn't support the upload operation.");
            }
            ResetToIdle();
            byte[] block = new byte[length];
            uint transferred = 0;
            while (transferred < length)
            {
                uint transferLen;
                if ((transferred + DfuDescriptor.TransferSize) > length)
                {
                    transferLen = length - transferred;
                }
                else
                {
                    transferLen = (uint)DfuDescriptor.TransferSize;
                }

                var b = Upload(blockNr, transferLen);
                Array.Copy(b, 0, block, transferred, transferLen);
                transferred += (uint)b.Length;
                blockNr++;

                if (b.Length < transferLen)
                {
                    Array.Resize(ref block, (int)transferred);
                    break;
                }
            }
            // if the last transfer was also TransferSize, add a 0 length transfer to terminate the upload
            if (GetState() == State.UploadIdle)
            {
                Upload(blockNr, 0);
            }
            return block;
        }

        /// <summary>
        /// Download a contiguous memory segment to the idling DFU device. (DFU spec 1.1)
        /// </summary>
        /// <param name="segment"></param>
        private void Download(byte[] segment)
        {
            Status status;
            ushort blockNr = 0;
            int transferLen, transferred = 0;

            // start in idle
            status = ClearErrors();

            try
            {
                VerifyState(status, State.Idle);

                // download phase
                UpdateDownloadProgress(segment.Length, transferred);

                while (transferred < segment.Length)
                {
                    // send data block for flashing
                    if ((transferred + DfuDescriptor.TransferSize) > segment.Length)
                    {
                        transferLen = segment.Length - transferred;
                    }
                    else
                    {
                        transferLen = DfuDescriptor.TransferSize;
                    }

                    Dnload(blockNr, segment, transferred, transferLen);

                    // wait until block is processed by device
                    for (status = GetStatus(); status.State == State.DnloadBusy; status = GetStatus())
                    {
                        Thread.Sleep(status.PollTimeout);
                    }

                    VerifyState(status, State.DnloadIdle);

                    blockNr++;
                    transferred += transferLen;
                    UpdateDownloadProgress(segment.Length, transferred);
                }
            }
            finally
            {
                // abort any stuck operation
                if ((transferred != segment.Length) && IsOpen() && status.State.Abortable())
                {
                    Abort();
                }
            }
        }

        #region ST Microelectronics Extension protocol
        /// <summary>
        /// Special commands can be executed by abusing Dnload request with 0 block number.
        /// </summary>
        private enum SeCommands : byte
        {
            GetCommands = 0x00,
            SetAddress = 0x21,
            Erase = 0x41,
            ReadUnprotect = 0x92,
        }

        /// <summary>
        /// Sets the initial address pointer for the upcoming Download/Upload transfers.
        /// </summary>
        /// <param name="address">Start address for the following transfer</param>
        /// <returns>The DFU status after the operation</returns>
        private Status SeSetAddress(uint address)
        {
            byte[] data = new byte[5];
            data[0] = (byte)SeCommands.SetAddress;
            data[1] = (byte)address;
            data[2] = (byte)(address >> 8);
            data[3] = (byte)(address >> 16);
            data[4] = (byte)(address >> 24);
            Dnload(0, data);

            Status status;
            for (status = GetStatus(); status.State == State.DnloadBusy; status = GetStatus())
            {
                Thread.Sleep(status.PollTimeout);
            }
            return status;
        }

        /// <summary>
        /// Erases the memory block at the specified address.
        /// </summary>
        /// <param name="address">Start address of the block to erase</param>
        /// <returns>The DFU status after the operation</returns>
        private Status SeErase(uint address)
        {
            byte[] data = new byte[5];
            data[0] = (byte)SeCommands.Erase;
            data[1] = (byte)address;
            data[2] = (byte)(address >> 8);
            data[3] = (byte)(address >> 16);
            data[4] = (byte)(address >> 24);
            Dnload(0, data);

            Status status;
            for (status = GetStatus(); status.State == State.DnloadBusy; status = GetStatus())
            {
                Thread.Sleep(status.PollTimeout);
            }
            return status;
        }

        private static readonly Regex SeMemoryLayoutRegex = new Regex(
                @"^@(?<name>[\w\s]*[\w]+)\s*
                 /0x(?<address>[A-F0-9]{1,8})
                 /(\d+)\*(\d+)([\x20KM]{1})([a-g]{1})(?:,(\d+)\*(\d+)([\x20KM]{1})([a-g]{1}))*$",
                RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        /// <summary>
        /// Parses the string description of a DFU alternate setting into a memory layout.
        /// </summary>
        /// <param name="altSel">DFU alternate setting index</param>
        /// <returns>The parsed memory layout</returns>
        private NamedLayout ParseLayout(byte altSel)
        {
            return ParseLayout(GetString(iAlternateSetting(altSel)));
        }

        /// <summary>
        /// Parses the string description of a DFU alternate setting into a memory layout.
        /// </summary>
        /// <param name="dfuSeFormat">DFU alternate setting string description</param>
        /// <returns>The parsed memory layout</returns>
        private NamedLayout ParseLayout(string dfuSeFormat)
        {
            var result = SeMemoryLayoutRegex.Match(dfuSeFormat);
            if (!result.Success)
            {
                throw new ArgumentException(String.Format("The DFU device has invalid DFUSE memory layout description ({0}).", dfuSeFormat));
            }

            var layout = new NamedLayout(result.Groups["name"].Value);
            uint address = UInt32.Parse(result.Groups["address"].Value, NumberStyles.HexNumber);

            int groupoff = 0;
            int i = 0;
            do
            {
                uint blockNo = UInt32.Parse(result.Groups[groupoff + 1].Captures[i].Value);
                uint blockSize = UInt32.Parse(result.Groups[groupoff + 2].Captures[i].Value);

                // size modifiers (K for kilo-, M for Mega)
                switch (result.Groups[groupoff + 3].Captures[i].Value[0])
                {
                    case 'K':
                        blockSize <<= 10;
                        break;
                    case 'M':
                        blockSize <<= 20;
                        break;
                    default:
                        break;
                }

                // the permissions are encoded in the lowest 3 bits of the ASCII character...
                var perm = (DeviceProgramming.Memory.Permissions)(result.Groups[groupoff + 4].Captures[i].Value[0] & 0x7);

                for (int b = 0; b < blockNo; b++)
                {
                    layout.AppendBlock(new Block(address, blockSize, perm));
                    address += blockSize;
                }

                if (groupoff == 0)
                {
                    groupoff = 4;
                }
                else
                {
                    i++;
                }
            } while (i < result.Groups[groupoff + 1].Captures.Count);

            return layout;
        }

        /// <summary>
        /// Download a memory to the idling DFU device. (DFUSE spec 1.1a)
        /// </summary>
        /// <param name="sortedMemory">A list of memory images, mapped to alternate settings</param>
        private void Download(Dictionary<byte, NamedMemory> sortedMemory)
        {
            foreach (var memoryWithAltSel in sortedMemory)
            {
                byte altSel = memoryWithAltSel.Key;
                var memory = memoryWithAltSel.Value;
                var layout = ParseLayout(altSel);
                var startAddr = memory.Segments.First().StartAddress;
                var endAddr = memory.Segments.Last().EndAddress;

                // verify memory with layout
                if ((startAddr < layout.StartAddress) || (endAddr > layout.EndAddress))
                {
                    throw new ArgumentOutOfRangeException("sortedMemory", String.Format("Memory is out of bounds at alternate setting {0}.", altSel));
                }

                int totalSize = memory.Segments.Sum((x) => x.Length);
                int totalTransferred = 0;

                // select this memory layout for download
                AlternateSetting = altSel;

                // ensure idle mode
                Status status = ClearErrors();

                try
                {
                    VerifyState(status, State.Idle);

                    int firstBlock = 0;
                    while ((layout.Blocks[firstBlock].StartAddress + layout.Blocks[firstBlock].Size) <= startAddr)
                    {
                        firstBlock++;
                    }
                    int lastBlock = firstBlock;
                    while ((layout.Blocks[lastBlock].StartAddress + layout.Blocks[lastBlock].Size) <= endAddr)
                    {
                        lastBlock++;
                    }

                    // erase blocks when necessary
                    for (int block = firstBlock; block <= lastBlock; block++)
                    {
                        if (!layout.Blocks[block].Permissions.IsWriteable())
                        {
                            throw new InvalidOperationException("Cannot download to readonly memory block.");
                        }
                        if (layout.Blocks[block].Permissions.IsEraseable())
                        {
                            status = SeErase((uint)layout.Blocks[block].StartAddress);
                            VerifyState(status, State.DnloadIdle);
                        }
                    }

                    UpdateDownloadProgress(totalSize, totalTransferred);

                    // download the segments to the target
                    for (int segNo = 0; segNo < memory.Segments.Count; segNo++)
                    {
                        ushort blockNr = 2;
                        int transferLen, transferred = 0;
                        var segment = memory.Segments[segNo];

                        status = SeSetAddress((uint)segment.StartAddress);
                        VerifyState(status, State.DnloadIdle);

                        while (transferred < segment.Length)
                        {
                            // send data block for flashing
                            if ((transferred + DfuDescriptor.TransferSize) > segment.Length)
                            {
                                transferLen = segment.Length - transferred;
                            }
                            else
                            {
                                transferLen = DfuDescriptor.TransferSize;
                            }

                            Dnload(blockNr, segment.Data, transferred, transferLen);

                            // wait until block is processed by device
                            for (status = GetStatus(); status.State == State.DnloadBusy; status = GetStatus())
                            {
                                Thread.Sleep(status.PollTimeout);
                            }

                            VerifyState(status, State.DnloadIdle);

                            blockNr++;
                            transferred += transferLen;
                            totalTransferred += transferLen;
                            UpdateDownloadProgress(totalSize, totalTransferred);

                            // block number 0 and 1 are reserved, on overflow:
                            if (blockNr == 0)
                            {
                                // set new address, so block number based address calculation will be valid
                                status = SeSetAddress((uint)segment.StartAddress + (uint)transferred);
                                VerifyState(status, State.DnloadIdle);
                                blockNr = 2;
                            }
                        }
                    }
                }
                finally
                {
                    // abort any stuck operation
                    if ((totalTransferred != totalSize) && IsOpen() && status.State.Abortable())
                    {
                        Abort();
                    }
                }
            }
        }

        /// <summary>
        /// Map the raw memory to alternate settings of the DFUSE device.
        /// </summary>
        /// <param name="memory">Raw memory image</param>
        /// <returns>Memory mapped to alternate settings</returns>
        private Dictionary<byte, NamedMemory> SortMemoryByAltSetting(RawMemory memory)
        {
            Dictionary<byte, NamedMemory> sortedMemory = new Dictionary<byte, NamedMemory>();
            int segNo = 0;

            while (segNo < memory.Segments.Count)
            {
                byte altSel;
                // go through the available memories (each of which is an alternate setting)
                for (altSel = 0; altSel < NumberOfAlternateSettings; altSel++)
                {
                    var layout = ParseLayout(altSel);
                    int segCount = 0;
                    var newMem = new NamedMemory(layout.Name);

                    // add segments which fit into this layout
                    for (int i = segNo;
                         (i < memory.Segments.Count) &&
                         (memory.Segments[i].StartAddress >= layout.StartAddress) &&
                         (memory.Segments[i].EndAddress <= layout.EndAddress);
                         i++)
                    {
                        newMem.TryAddSegment(memory.Segments[i]);
                        segCount++;
                    }

                    // no matching segments, continue searching fitting alternate setting
                    if (segCount == 0)
                    {
                        continue;
                    }

                    sortedMemory.Add(altSel, newMem);

                    // advance with the segments
                    segNo += segCount;
                    break;
                }

                // loop terminated without break
                if (altSel == NumberOfAlternateSettings)
                {
                    throw new ArgumentOutOfRangeException("memory", memory, "The provided memory has segments that cannot be found on the target device.");
                }
            }

            return sortedMemory;
        }

        /// <summary>
        /// Upload part of the firmware image from the reconfigured DFU device.
        /// </summary>
        /// <param name="mblock">Memory block to read from</param>
        /// <returns>The memory segment provided by the device</returns>
        public Segment UploadBlock(Block mblock)
        {
            // perform a bunch of tests first
            if (DfuDescriptor.DfuVersion != Protocol.SeVersion)
            {
                throw new InvalidOperationException("The device doesn't support the DFUSE protocol.");
            }

            byte altSel = 0;
            for (; altSel < NumberOfAlternateSettings; altSel++)
            {
                var layout = ParseLayout(altSel);
                if ((mblock.StartAddress >= layout.StartAddress) && (mblock.EndAddress <= layout.EndAddress))
                {
                    foreach (var block in layout.Blocks)
                    {
                        if (block.Overlaps(mblock) && !block.Permissions.IsReadable())
                        {
                            throw new ArgumentOutOfRangeException("mblock", mblock, "The provided memory block is not readable on the target device.");
                        }
                    }
                    break;
                }
            }
            if (altSel == NumberOfAlternateSettings)
            {
                throw new ArgumentOutOfRangeException("mblock", mblock, "The provided memory block is not available on the target device.");
            }

            // select this memory layout for upload
            AlternateSetting = altSel;

            // select start address
            ResetToIdle();

            Status status = SeSetAddress((uint)mblock.StartAddress);
            VerifyState(status, State.DnloadIdle);

            // perform memory dump
            // TODO: ensure block number doesn't overflow
            byte[] memory = UploadBlock((uint)mblock.Size, 2);

            return new Segment(mblock.StartAddress, memory);
        }
        #endregion
    }
}
