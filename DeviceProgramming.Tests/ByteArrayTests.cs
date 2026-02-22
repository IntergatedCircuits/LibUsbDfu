using System;
using System.Runtime.InteropServices;
using DeviceProgramming;
using Xunit;

namespace DeviceProgramming.Tests
{
    public class ByteArrayTests
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TestStruct
        {
            public byte A;
            public short B;
            public int C;
        }

        [Fact]
        public void ToBytes_And_ToStruct_Roundtrip()
        {
            var s = new TestStruct { A = 0x12, B = 0x3456, C = 0x789ABCDE };

            byte[] bytes = s.ToBytes();
            Assert.Equal(Marshal.SizeOf<TestStruct>(), bytes.Length);

            var s2 = bytes.ToStruct<TestStruct>();
            Assert.Equal(s.A, s2.A);
            Assert.Equal(s.B, s2.B);
            Assert.Equal(s.C, s2.C);
        }

        [Fact]
        public void ToStruct_Throws_On_ShortBuffer()
        {
            byte[] small = new byte[2];
            Assert.Throws<ArgumentException>(() => small.ToStruct<TestStruct>());
        }
    }
}
