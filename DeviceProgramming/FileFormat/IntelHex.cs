using System;
using System.IO;
using System.Text.RegularExpressions;
using DeviceProgramming.Memory;

namespace DeviceProgramming.FileFormat
{
    /// <summary>
    /// Intel HEX is a file format that conveys binary information as hex values in ASCII text form.
    /// https://en.wikipedia.org/wiki/Intel_HEX
    /// </summary>
    public static class IntelHex
    {
        /// <summary>
        /// Tests if the file extension is associated with this file format.
        /// </summary>
        /// <param name="ext">The file extension, starting with the dot</param>
        /// <returns>True if the file extension is supported, false otherwise</returns>
        public static bool IsExtensionSupported(string ext)
        {
            return ext.Equals(".hex", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".H86", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses the contents of a file into a memory image.
        /// </summary>
        /// <param name="filepath">Path to the HEX file</param>
        /// <returns>The parsed memory image</returns>
        public static RawMemory ParseFile(string filepath)
        {
            using (StreamReader file = new StreamReader(filepath))
            {
                bool fileended = false;
                var parser = new TextRecordParser();

                // parse each line as a record
                while (!fileended && parser.ParseNextRecord(file, RecordRegex, CountCharOffset))
                {
                    // check if checksum is correct
                    var chksum = (byte)(parser.ByteCount + (parser.Address >> 8) + parser.Address + parser.RecordType + parser.DataChecksum);
                    
                    // this is equal to taking 2's complement
                    chksum += parser.Checksum;
                    if (chksum != 0)
                    {
                        throw new ArgumentException(String.Format("The selected hex file has incorrect checksum (line {0}).", parser.CurrentLine));
                    }

                    switch ((RecordType)parser.RecordType)
                    {
                        case RecordType.Data:
                            parser.SaveRecordData();
                            break;

                        case RecordType.EndOfFile:
                            // flush the last segment
                            parser.FlushSegment();
                            fileended = true;
                            break;

                        case RecordType.ExtendedLinearAddress:
                            // check the byte count here instead of coding it into the regex
                            if (parser.ByteCount != 2)
                            {
                                throw new ArgumentException(String.Format("The selected hex file has invalid record format (line {0}).", parser.CurrentLine));
                            }
                            parser.AddressOffset = ((uint)parser.Data[0] << 24) | ((uint)parser.Data[1] << 16);
                            break;

                        case RecordType.ExtendedSegmentAddress:
                            // check the byte count here instead of coding it into the regex
                            if (parser.ByteCount != 2)
                            {
                                throw new ArgumentException(String.Format("The selected hex file has invalid record format (line {0}).", parser.CurrentLine));
                            }
                            parser.AddressOffset = ((uint)parser.Data[0] << 12) | ((uint)parser.Data[1] << 4);
                            break;

                        #region not used
                        case RecordType.StartLinearAddress:
                            // check the byte count here instead of coding it into the regex
                            if (parser.ByteCount != 4)
                            {
                                throw new ArgumentException(String.Format("The selected hex file has invalid record format (line {0}).", parser.CurrentLine));
                            }
                            break;

                        case RecordType.StartSegmentAddress:
                            // check the byte count here instead of coding it into the regex
                            if (parser.ByteCount != 4)
                            {
                                throw new ArgumentException(String.Format("The selected hex file has invalid record format (line {0}).", parser.CurrentLine));
                            }
                            break;
                        #endregion
                    }
                }

                if (parser.CurrentLine == 0)
                { 
                    throw new ArgumentException("The selected file is empty or has invalid format.");
                }

                if (!fileended)
                {
                    throw new ArgumentException("The selected hex file is missing hex format End Of File line.");
                }
                return parser.Memory;
            }
        }

        private enum RecordType : byte
        {
            Data = 0x00,
            EndOfFile = 0x01,
            ExtendedSegmentAddress = 0x02,
            StartSegmentAddress = 0x03,
            ExtendedLinearAddress = 0x04,
            StartLinearAddress = 0x05,
        }

        private static readonly int CountCharOffset = 11;

        private static readonly Regex RecordRegex = new Regex
            (@"^:(?<bytecount>[A-F0-9]{2})(?<address>[A-F0-9]{4})(?<recordtype>0[0-5]{1})(?<data>(?:[A-F0-9]{2})*)(?<checksum>[A-F0-9]{2})$",
            RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
    }
}
