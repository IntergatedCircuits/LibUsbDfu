using DeviceProgramming.FileFormat;
using System;
using System.IO;
using Xunit;

namespace DeviceProgramming.Tests
{
    // Helper base providing temp-file management for file format tests.
    public abstract class FileFormatTestBase
    {
        /// <summary>
        /// Writes <paramref name="lines"/> to a new temporary file and returns its path.
        /// The caller is responsible for deleting the file (use try/finally).
        /// </summary>
        protected static string CreateTempFile(params string[] lines)
        {
            string path = Path.GetTempFileName();
            File.WriteAllLines(path, lines);
            return path;
        }
    }

    /// <summary>
    /// Tests for <see cref="IntelHex.ParseFile"/>.
    ///
    /// Intel HEX record types covered:
    ///   00 - Data
    ///   01 - End Of File
    ///   02 - Extended Segment Address  (20-bit, segment * 16)
    ///   03 - Start Segment Address     (CS:IP - ignored by parser)
    ///   04 - Extended Linear Address   (upper 16 bits of 32-bit address)
    ///   05 - Start Linear Address      (EIP - ignored by parser)
    ///
    /// Checksum rule: sum of all record bytes (including checksum byte) == 0 (mod 256).
    /// </summary>
    public class IntelHexTests : FileFormatTestBase
    {
        // ---------------------------------------------------------------
        // Valid – single contiguous segment
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_BasicData_SingleSegment()
        {
            // [0x01, 0x02, 0x03] at 0x0100, followed by EOF
            // :03 0100 00 010203 F6  (sum=0x0A, chk=0xF6)
            // :00 0000 01 FF         (sum=0x01, chk=0xFF)
            string path = CreateTempFile(
                ":03010000010203F6",
                ":00000001FF");
            try
            {
                var mem = IntelHex.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal((ulong)0x0100, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_ExtendedLinearAddress_HighAddressSegment()
        {
            // ELA 0x0001  -> address base 0x00010000
            // data [0x01, 0x02] at local 0x0000  -> absolute 0x00010000
            // :02 0000 04 0001 F9  (sum=0x07, chk=0xF9)
            // :02 0000 00 0102 FB  (sum=0x05, chk=0xFB)
            // :00 0000 01 FF
            string path = CreateTempFile(
                ":020000040001F9",
                ":020000000102FB",
                ":00000001FF");
            try
            {
                var mem = IntelHex.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal((ulong)0x00010000, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x01, 0x02 }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_ExtendedSegmentAddress_HighAddressSegment()
        {
            // ESA 0x1000  -> address base = 0x1000 * 16 = 0x00010000
            // data [0x05, 0x06] at local 0x0000  -> absolute 0x00010000
            // :02 0000 02 1000 EC  (sum=0x14, chk=0xEC)
            // :02 0000 00 0506 F3  (sum=0x0D, chk=0xF3)
            // :00 0000 01 FF
            string path = CreateTempFile(
                ":020000021000EC",
                ":020000000506F3",
                ":00000001FF");
            try
            {
                var mem = IntelHex.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal((ulong)0x00010000, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x05, 0x06 }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_StartLinearAddress_RecordIsAccepted()
        {
            // SLA (type 05) carries a 4-byte EIP entry point; the parser ignores it
            // :03 0100 00 010203 F6
            // :04 0000 05 00000000 F7  (sum=0x09, chk=0xF7)
            // :00 0000 01 FF
            string path = CreateTempFile(
                ":03010000010203F6",
                ":0400000500000000F7",
                ":00000001FF");
            try
            {
                var mem = IntelHex.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_StartSegmentAddress_RecordIsAccepted()
        {
            // SSA (type 03) carries a 4-byte CS:IP entry point; the parser ignores it
            // :03 0100 00 010203 F6
            // :04 0000 03 00000000 F9  (sum=0x07, chk=0xF9)
            // :00 0000 01 FF
            string path = CreateTempFile(
                ":03010000010203F6",
                ":0400000300000000F9",
                ":00000001FF");
            try
            {
                var mem = IntelHex.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        // ---------------------------------------------------------------
        // Valid – multiple disjunct segments
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_TwoDisjunctDataRanges_ProduceTwoSegments()
        {
            // [0x01, 0x02] at 0x0100 and [0x0A, 0x0B] at 0x0200 - gap in between
            // :02 0100 00 0102 FA  (sum=0x06, chk=0xFA)
            // :02 0200 00 0A0B E7  (sum=0x19, chk=0xE7)
            // :00 0000 01 FF
            string path = CreateTempFile(
                ":020100000102FA",
                ":020200000A0BE7",
                ":00000001FF");
            try
            {
                var mem = IntelHex.ParseFile(path);
                Assert.Equal(2, mem.Segments.Count);
                Assert.Equal((ulong)0x0100, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x01, 0x02 }, mem.Segments[0].Data);
                Assert.Equal((ulong)0x0200, mem.Segments[1].StartAddress);
                Assert.Equal(new byte[] { 0x0A, 0x0B }, mem.Segments[1].Data);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_TwoDisjunctSegments_ViaExtendedLinearAddress()
        {
            // Segment 1: [0x01, 0x02] at 0x0100 (no ELA -> base 0)
            // Segment 2: [0x0A, 0x0B] at 0x00010000 (ELA 0x0001 -> base 0x00010000)
            // :02 0100 00 0102 FA  (sum=0x06, chk=0xFA)
            // :02 0000 04 0001 F9
            // :02 0000 00 0A0B E9  (sum=0x17, chk=0xE9)
            // :00 0000 01 FF
            string path = CreateTempFile(
                ":020100000102FA",
                ":020000040001F9",
                ":020000000A0BE9",
                ":00000001FF");
            try
            {
                var mem = IntelHex.ParseFile(path);
                Assert.Equal(2, mem.Segments.Count);
                Assert.Equal((ulong)0x0100, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x01, 0x02 }, mem.Segments[0].Data);
                Assert.Equal((ulong)0x00010000, mem.Segments[1].StartAddress);
                Assert.Equal(new byte[] { 0x0A, 0x0B }, mem.Segments[1].Data);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_TwoDisjunctSegments_ViaExtendedSegmentAddress()
        {
            // Segment 1: [0x05, 0x06] at 0x0000 (no ESA)
            // Segment 2: [0x05, 0x06] at 0x00010000 (ESA 0x1000)
            // :02 0000 00 0506 F3
            // :02 0000 02 1000 EC
            // :02 0000 00 0506 F3
            // :00 0000 01 FF
            string path = CreateTempFile(
                ":020000000506F3",
                ":020000021000EC",
                ":020000000506F3",
                ":00000001FF");
            try
            {
                var mem = IntelHex.ParseFile(path);
                Assert.Equal(2, mem.Segments.Count);
                Assert.Equal((ulong)0x00000000, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x05, 0x06 }, mem.Segments[0].Data);
                Assert.Equal((ulong)0x00010000, mem.Segments[1].StartAddress);
                Assert.Equal(new byte[] { 0x05, 0x06 }, mem.Segments[1].Data);
            }
            finally { File.Delete(path); }
        }

        // ---------------------------------------------------------------
        // Invalid – structural errors
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_EmptyFile_Throws()
        {
            string path = CreateTempFile();
            try { Assert.Throws<ArgumentException>(() => IntelHex.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_MissingEofRecord_Throws()
        {
            // File has a valid data record but no EOF – the parser must detect this
            string path = CreateTempFile(":03010000010203F6");
            try { Assert.Throws<ArgumentException>(() => IntelHex.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_DataAfterEofRecord_TrailingRecordsIgnored()
        {
            // The parser loop is guarded by !fileended, so it stops as soon as the
            // EOF record (type 01) is consumed.  Any records appearing after the EOF
            // line are never read; only the data recorded before it must be returned.
            // :02 0100 00 0102 FA  (sum=0x06, chk=0xFA)
            // :00 0000 01 FF       <- EOF - parser stops here
            // :03 0200 00 010203 F5  <- never read
            string path = CreateTempFile(
                ":020100000102FA",
                ":00000001FF",
                ":030200000102 03F5");
            try
            {
                var mem = IntelHex.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal((ulong)0x0100, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x01, 0x02 }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        // ---------------------------------------------------------------
        // Invalid – malformed record content
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_InvalidLineFormat_Throws()
        {
            // Line does not match the record regex at all
            string path = CreateTempFile("GARBAGE");
            try { Assert.Throws<ArgumentException>(() => IntelHex.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_BadChecksum_Throws()
        {
            // Correct checksum is F6 but FF is supplied instead
            string path = CreateTempFile(
                ":03010000010203FF",
                ":00000001FF");
            try { Assert.Throws<ArgumentException>(() => IntelHex.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_WrongDataLength_Throws()
        {
            // byteCount says 3 bytes of data but only 2 are present; the length
            // check in the parser must catch this before the checksum check
            string path = CreateTempFile(
                ":030100000102FA",   // byteCount=3, data=[01,02] (only 2 bytes) - length mismatch
                ":00000001FF");
            try { Assert.Throws<ArgumentException>(() => IntelHex.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        // ---------------------------------------------------------------
        // Invalid – wrong byte count in address-extension records
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_ExtendedLinearAddress_InvalidByteCount_Throws()
        {
            // ELA must have byteCount == 2; here byteCount == 1
            // :01 0000 04 00 FB  (sum=0x05, chk=0xFB) - valid checksum, invalid byteCount
            string path = CreateTempFile(
                ":0100000400FB",
                ":00000001FF");
            try { Assert.Throws<ArgumentException>(() => IntelHex.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_ExtendedSegmentAddress_InvalidByteCount_Throws()
        {
            // ESA must have byteCount == 2; here byteCount == 1
            // :01 0000 02 10 ED  (sum=0x13, chk=0xED)
            string path = CreateTempFile(
                ":0100000210ED",
                ":00000001FF");
            try { Assert.Throws<ArgumentException>(() => IntelHex.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_StartLinearAddress_InvalidByteCount_Throws()
        {
            // SLA must have byteCount == 4; here byteCount == 2
            // :02 0000 05 0000 F9  (sum=0x07, chk=0xF9)
            string path = CreateTempFile(
                ":020000050000F9",
                ":00000001FF");
            try { Assert.Throws<ArgumentException>(() => IntelHex.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_StartSegmentAddress_InvalidByteCount_Throws()
        {
            // SSA must have byteCount == 4; here byteCount == 2
            // :02 0000 03 0000 FB  (sum=0x05, chk=0xFB)
            string path = CreateTempFile(
                ":020000030000FB",
                ":00000001FF");
            try { Assert.Throws<ArgumentException>(() => IntelHex.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        // ---------------------------------------------------------------
        // Invalid – overlapping memory areas
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_OverlappingDataRecords_Throws()
        {
            // Record 1: [0x01, 0x02, 0x03] at 0x0100..0x0102
            // Record 2: [0x04]             at 0x0101       <- overlaps
            // :03 0100 00 010203 F6
            // :01 0101 00 04 F9  (sum=0x01+0x01+0x01+0x00+0x04=0x07, chk=0xF9)
            // :00 0000 01 FF
            string path = CreateTempFile(
                ":03010000010203F6",
                ":0101010004F9",
                ":00000001FF");
            try { Assert.Throws<ArgumentException>(() => IntelHex.ParseFile(path)); }
            finally { File.Delete(path); }
        }
    }

    /// <summary>
    /// Tests for <see cref="SRecord.ParseFile"/>.
    ///
    /// Motorola S-record types covered:
    ///   S0 - Header
    ///   S1 - Data  with 16-bit address
    ///   S2 - Data  with 24-bit address
    ///   S3 - Data  with 32-bit address
    ///   S5 - Record count (16-bit)
    ///   S6 - Record count (24-bit)
    ///   S7 - End / start address (32-bit)
    ///   S8 - End / start address (24-bit)
    ///   S9 - End / start address (16-bit)
    ///
    /// Checksum rule: (sum_of_record_bytes XOR checksum) == 0xFF,
    /// i.e. checksum == one's-complement of the least-significant byte of the sum.
    /// "Record bytes" = byteCount field + all address bytes + all data bytes.
    /// </summary>
    public class SRecordTests : FileFormatTestBase
    {
        // ---------------------------------------------------------------
        // Valid – S1 (16-bit address)
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_S1Data_WithHeaderAndTerminator_SingleSegment()
        {
            // S0 07 0000 54455354 B8  header "TEST"  (sum=0x47, chk=0xB8)
            // S1 06 0100 010203 F2    data [1,2,3]    (sum=0x0D, chk=0xF2)
            // S9 03 0000 FC           terminator      (sum=0x03, chk=0xFC)
            string path = CreateTempFile(
                "S007000054455354B8",
                "S1060100010203F2",
                "S9030000FC");
            try
            {
                var mem = SRecord.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal((ulong)0x0100, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_S1Data_TwoDisjunctSegments()
        {
            // Segment 1: [0x01, 0x02] at 0x0100
            // S1 05 0100 0102 F6  (sum=0x09, chk=0xF6)
            // Segment 2: [0x0A, 0x0B] at 0x0200
            // S1 05 0200 0A0B E3  (sum=0x1C, chk=0xE3)
            // S9 03 0000 FC
            string path = CreateTempFile(
                "S10501000102F6",
                "S10502000A0BE3",
                "S9030000FC");
            try
            {
                var mem = SRecord.ParseFile(path);
                Assert.Equal(2, mem.Segments.Count);
                Assert.Equal((ulong)0x0100, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x01, 0x02 }, mem.Segments[0].Data);
                Assert.Equal((ulong)0x0200, mem.Segments[1].StartAddress);
                Assert.Equal(new byte[] { 0x0A, 0x0B }, mem.Segments[1].Data);
            }
            finally { File.Delete(path); }
        }

        // ---------------------------------------------------------------
        // Valid – S2 (24-bit address)
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_S2Data_WithS8Terminator_SingleSegment()
        {
            // S2 06 010000 AABB 93  data [0xAA,0xBB] at 0x010000  (sum=0x6C, chk=0x93)
            // S8 04 000000 FB        terminator                    (sum=0x04, chk=0xFB)
            string path = CreateTempFile(
                "S206010000AABB93",
                "S804000000FB");
            try
            {
                var mem = SRecord.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal((ulong)0x010000, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0xAA, 0xBB }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_S2Data_TwoDisjunctSegments()
        {
            // Segment 1: [0xAA, 0xBB] at 0x010000
            // Segment 2: [0x11, 0x22] at 0x020000
            // S2 06 020000 1122 ??
            // sum = 06+02+00+00+11+22 = 0x3B, chk = ~0x3B = 0xC4
            string path = CreateTempFile(
                "S206010000AABB93",
                "S2060200001122C4",
                "S804000000FB");
            try
            {
                var mem = SRecord.ParseFile(path);
                Assert.Equal(2, mem.Segments.Count);
                Assert.Equal((ulong)0x010000, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0xAA, 0xBB }, mem.Segments[0].Data);
                Assert.Equal((ulong)0x020000, mem.Segments[1].StartAddress);
                Assert.Equal(new byte[] { 0x11, 0x22 }, mem.Segments[1].Data);
            }
            finally { File.Delete(path); }
        }

        // ---------------------------------------------------------------
        // Valid – S3 (32-bit address)
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_S3Data_WithS7Terminator_SingleSegment()
        {
            // S3 07 00010000 1122 C4  data [0x11,0x22] at 0x00010000  (sum=0x3B, chk=0xC4)
            // S7 05 00000000 FA        terminator                      (sum=0x05, chk=0xFA)
            string path = CreateTempFile(
                "S307000100001122C4",
                "S70500000000FA");
            try
            {
                var mem = SRecord.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal((ulong)0x00010000, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x11, 0x22 }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_S3Data_TwoDisjunctSegments()
        {
            // Segment 1: [0x11, 0x22] at 0x00010000
            // Segment 2: [0x33, 0x44] at 0x00020000
            // S3 07 00020000 3344 ??
            // sum = 07+00+02+00+00+33+44 = 0x80, chk = ~0x80 & 0xFF = 0x7F
            string path = CreateTempFile(
                "S307000100001122C4",
                "S3070002000033447F",
                "S70500000000FA");
            try
            {
                var mem = SRecord.ParseFile(path);
                Assert.Equal(2, mem.Segments.Count);
                Assert.Equal((ulong)0x00010000, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x11, 0x22 }, mem.Segments[0].Data);
                Assert.Equal((ulong)0x00020000, mem.Segments[1].StartAddress);
                Assert.Equal(new byte[] { 0x33, 0x44 }, mem.Segments[1].Data);
            }
            finally { File.Delete(path); }
        }

        // ---------------------------------------------------------------
        // Valid – S5 / S6 record-count records are accepted
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_S5RecordCount_IsAccepted()
        {
            // S5 carries a 16-bit record count; the parser ignores its value
            // S0 07 0000 54455354 B8
            // S1 05 0000 DEAD 6F  (sum=0x90, chk=0x6F)
            // S5 03 0001 FB        count=1  (sum=0x04, chk=0xFB)
            // S9 03 0000 FC
            string path = CreateTempFile(
                "S007000054455354B8",
                "S1050000DEAD6F",
                "S5030001FB",
                "S9030000FC");
            try
            {
                var mem = SRecord.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal((ulong)0x0000, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0xDE, 0xAD }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_S6RecordCount_IsAccepted()
        {
            // S6 carries a 24-bit record count; the parser ignores its value
            // S2 06 010000 AABB 93
            // S6 04 000001 FA  count=1  (sum=0x05, chk=0xFA)
            // S8 04 000000 FB
            string path = CreateTempFile(
                "S206010000AABB93",
                "S604000001FA",
                "S804000000FB");
            try
            {
                var mem = SRecord.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal((ulong)0x010000, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0xAA, 0xBB }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        // ---------------------------------------------------------------
        // Valid – data without an explicit terminator record
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_S1Data_WithoutTerminationRecord_SegmentIsFlushed()
        {
            // A file with only a header and data records but no S9 is legal in some
            // toolchains; all accumulated data should still be returned.
            string path = CreateTempFile(
                "S007000054455354B8",
                "S1060100010203F2");
            try
            {
                var mem = SRecord.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal((ulong)0x0100, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_S3DataWithS5Count_NoTerminationRecord_SegmentIsFlushed()
        {
            // Regression test for issue #18: bincopy and similar tools emit SREC files
            // that contain only S3 data records and an S5 record-count record, with no
            // S7/S8/S9 terminator.  The parser must flush buffered data at EOF.
            // S3 07 00010000 1122 C4  data [0x11,0x22] at 0x00010000  (sum=0x3B, chk=0xC4)
            // S5 03 0001 FB           count=1                         (sum=0x04, chk=0xFB)
            string path = CreateTempFile(
                "S307000100001122C4",
                "S5030001FB");
            try
            {
                var mem = SRecord.ParseFile(path);
                Assert.Single(mem.Segments);
                Assert.Equal((ulong)0x00010000, mem.Segments[0].StartAddress);
                Assert.Equal(new byte[] { 0x11, 0x22 }, mem.Segments[0].Data);
            }
            finally { File.Delete(path); }
        }

        // ---------------------------------------------------------------
        // Invalid – structural errors
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_EmptyFile_Throws()
        {
            string path = CreateTempFile();
            try { Assert.Throws<ArgumentException>(() => SRecord.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        // ---------------------------------------------------------------
        // Invalid – malformed record content
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_InvalidLineFormat_Throws()
        {
            string path = CreateTempFile("GARBAGE");
            try { Assert.Throws<ArgumentException>(() => SRecord.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_BadChecksum_Throws()
        {
            // Correct checksum for S1060100010203 is F2; supply 00 instead
            string path = CreateTempFile(
                "S106010001020300",
                "S9030000FC");
            try { Assert.Throws<ArgumentException>(() => SRecord.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_WrongDataLength_Throws()
        {
            // byteCount=6 claims 3 addr+data bytes, but only 2 data bytes are
            // encoded in the line: "S1" + "06" + "0100" + "0102" + "F5"
            // line length = 14, expected (6*2)+4 = 16 -> parser throws.
            string path = CreateTempFile(
                "S10601000102F5",
                "S9030000FC");
            try { Assert.Throws<ArgumentException>(() => SRecord.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        // ---------------------------------------------------------------
        // Invalid – overlapping memory areas
        // ---------------------------------------------------------------

        [Fact]
        public void ParseFile_OverlappingS1Records_Throws()
        {
            // Record 1: [0x01, 0x02, 0x03] at 0x0100..0x0102
            // Record 2: [0xFF]             at 0x0101       <- overlaps
            // S1 06 0100 010203 F2
            // S1 04 0101 FF FA   (sum=0x05, chk=0xFA)
            // S9 03 0000 FC
            string path = CreateTempFile(
                "S1060100010203F2",
                "S1040101FFFA",
                "S9030000FC");
            try { Assert.Throws<ArgumentException>(() => SRecord.ParseFile(path)); }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ParseFile_OverlappingS2Records_Throws()
        {
            // Record 1: [0xAA, 0xBB] at 0x010000..0x010001
            // Record 2: [0xCC]       at 0x010001              <- overlaps
            // S2 05 010001 CC ??
            // sum = 05+01+00+01+CC = 0xD3, chk = ~0xD3 = 0x2C
            string path = CreateTempFile(
                "S206010000AABB93",
                "S205010001CC2C",
                "S804000000FB");
            try { Assert.Throws<ArgumentException>(() => SRecord.ParseFile(path)); }
            finally { File.Delete(path); }
        }
    }
}
