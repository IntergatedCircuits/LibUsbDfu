using DeviceProgramming.Memory;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace DeviceProgramming.FileFormat
{
    /// <summary>
    /// Helper class for parsing ASCII text record based image file formats.
    /// </summary>
    internal class TextRecordParser
    {
        /// <summary>
        /// The last processed line number of the file.
        /// </summary>
        public uint CurrentLine { get; private set; }

        public byte ByteCount { get; private set; }
        public uint Address { get; private set; }
        public byte RecordType { get; private set; }
        public byte Checksum { get; private set; }
        public byte[] Data { get; private set; }
        public byte DataChecksum { get; private set; }

        public uint AddressOffset { get; set; }

        private uint SegmentStartAddress { get; set; }
        private uint SegmentEndAddress { get; set; }
        private List<byte[]> MemoryBlocks { get; set; }

        public RawMemory Memory { get; private set; }

        /// <summary>
        /// Creates an empty text record parser context.
        /// </summary>
        public TextRecordParser()
        {
            CurrentLine = 0;
            AddressOffset = 0;
            SegmentStartAddress = (~0u) - 1u;
            SegmentEndAddress = (~0u) - 1u;
            MemoryBlocks = new List<byte[]>();
            Memory = new RawMemory();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file">File reader stream is parsed one line at a time</param>
        /// <param name="regex">
        /// The regular expression to use with mandatory <c>"bytecount"</c>, <c>"address"</c>,
        /// <c>"recordtype"</c>, <c>"checksum"</c> and <c>"data"</c> groups
        /// </param>
        /// <param name="countCharOffset">
        /// Number of header characters that aren't included in the ByteCount of the format
        /// </param>
        /// <returns>
        /// True if the line was parsed, false if the end of file is reached
        /// </returns>
        public bool ParseNextRecord(StreamReader file, Regex regex, int countCharOffset)
        {
            // advance to the next line
            string line = file.ReadLine();
            if (line == null)
            {
                return false;
            }
            CurrentLine++;

            // parse the record information using the provided regex
            var result = regex.Match(line);
            if (!result.Success)
            {
                throw new ArgumentException(String.Format("The selected record file has invalid format (line {0}).", CurrentLine));
            }
            ByteCount = Byte.Parse(result.Groups["bytecount"].Value, NumberStyles.HexNumber);
            Address = UInt32.Parse(result.Groups["address"].Value, NumberStyles.HexNumber);
            RecordType = Byte.Parse(result.Groups["recordtype"].Value, NumberStyles.HexNumber);
            Checksum = Byte.Parse(result.Groups["checksum"].Value, NumberStyles.HexNumber);
            var sdata = result.Groups["data"].Value;

            // check if length is correct
            if (((ByteCount * 2) + countCharOffset) != result.Length)
            {
                throw new ArgumentException(String.Format("The selected record file has incorrect data length (line {0}).", CurrentLine));
            }

            // parse the data into a byte array, sum them up for checksum calculation
            int dlen = sdata.Length / 2;
            Data = new byte[dlen];
            DataChecksum = 0;

            for (int i = 0; i < dlen; i++)
            {
                Data[i] = Byte.Parse(sdata.Substring(i * 2, 2), NumberStyles.HexNumber);
                DataChecksum += Data[i];
            }

            return true;
        }

        /// <summary>
        /// This function adds the current record's data to the memory image.
        /// </summary>
        public void SaveRecordData()
        {
            var memaddress = AddressOffset + Address;

            // if new address segment starts, save the previous one first
            if (memaddress != (SegmentEndAddress + 1u))
            {
                FlushSegment();
                SegmentStartAddress = memaddress;
            }

            MemoryBlocks.Add(Data);
            SegmentEndAddress = memaddress + (uint)Data.Length - 1u;
        }

        /// <summary>
        /// Saves the current work segment into the memory image.
        /// </summary>
        public void FlushSegment()
        {
            if (MemoryBlocks.Count > 0)
            {
                // unify the record data blocks into a single segment
                var memseg = new byte[SegmentEndAddress + 1 - SegmentStartAddress];
                uint reladdr = 0;
                for (int i = 0; i < MemoryBlocks.Count; i++)
                {
                    MemoryBlocks[i].CopyTo(memseg, reladdr);
                    reladdr += (uint)MemoryBlocks[i].Length;
                }
                // add it to the image, and empty the context
                Memory.TryAddSegment(new Segment(SegmentStartAddress, memseg));
                MemoryBlocks.Clear();
            }
        }
    }
}
