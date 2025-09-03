using DeviceProgramming.Memory;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace DeviceProgramming.FileFormat
{
    /// <summary>
    /// Motorola S-record is a file format that conveys binary information as hex values in ASCII text form.
    /// https://en.wikipedia.org/wiki/SREC_(file_format)
    /// </summary>
    public static class SRecord
    {
        /// <summary>
        /// Tests if the file extension is associated with this file format.
        /// </summary>
        /// <param name="ext">The file extension, starting with the dot</param>
        /// <returns>True if the file extension is supported, false otherwise</returns>
        public static bool IsExtensionSupported(string ext)
        {
            return ext.Equals(".SREC", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".SRECORD", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".S19", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".S28", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".S37", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses the contents of a file into a memory image.
        /// </summary>
        /// <param name="filepath">Path to the SREC file</param>
        /// <returns>The parsed memory image</returns>
        public static RawMemory ParseFile(string filepath)
        {
            using (StreamReader file = new StreamReader(filepath))
            {
                var parser = new TextRecordParser();

                // parse each line as a record
                while (parser.ParseNextRecord(file, RecordRegex, CountCharOffset))
                {
                    // check if checksum is correct
                    var chksum = (byte)(parser.ByteCount + (parser.Address >> 24) + (parser.Address >> 16) + (parser.Address >> 8) + parser.Address + parser.DataChecksum);
                    chksum ^= parser.Checksum;
                    if (chksum != 0xFF)
                    {
                        throw new ArgumentException(String.Format("The selected srec file has incorrect checksum (line {0}).", parser.CurrentLine));
                    }

                    switch ((RecordType)parser.RecordType)
                    {
                        case RecordType.Data16b:
                        case RecordType.Data24b:
                        case RecordType.Data32b:
                            parser.SaveRecordData();
                            break;

                        case RecordType.StartAddress16b:
                        case RecordType.StartAddress24b:
                        case RecordType.StartAddress32b:
                            parser.FlushSegment();
                            break;

                        #region not used
                        case RecordType.Header:
                            break;
                        case RecordType.Count16b:
                        case RecordType.Count24b:
                            break;
                        #endregion
                    }
                }

                if (parser.CurrentLine == 0)
                {
                    throw new ArgumentException("The selected file is empty or has invalid format.");
                }
                return parser.Memory;
            }
        }

        private enum RecordType : byte
        {
            Header = 0,
            Data16b = 1,
            Data24b = 2,
            Data32b = 3,
            Count16b = 5,
            Count24b = 6,
            StartAddress32b = 7,
            StartAddress24b = 8,
            StartAddress16b = 9,
        }

        private static readonly byte[] AddressSizePerType = { 2, 2, 3, 4, 0, 2, 3, 4, 3, 2 };

        private static readonly int CountCharOffset = 4;

        private static readonly Regex RecordRegex = new Regex
            (@"^S(?:(?:(?<recordtype>[0,1,5,9]{1})(?<bytecount>[A-F0-9]{2})(?<address>(?:[A-F0-9]{2}){2})) |
                    (?:(?<recordtype>[2,6,8]{1})(?<bytecount>[A-F0-9]{2})(?<address>(?:[A-F0-9]{2}){3})) |
                    (?:(?<recordtype>[3,7]{1})(?<bytecount>[A-F0-9]{2})(?<address>(?:[A-F0-9]{2}){4})))(?<data>(?:[A-F0-9]{2})*)(?<checksum>[A-F0-9]{2})$",
            RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
    }
}
