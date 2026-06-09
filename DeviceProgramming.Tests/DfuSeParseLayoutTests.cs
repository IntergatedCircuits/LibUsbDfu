using DeviceProgramming.Dfu;
using DeviceProgramming.Memory;
using System;
using Xunit;

namespace DeviceProgramming.Tests
{
    /// <summary>
    /// Unit tests for <see cref="Device.ParseLayout(string)"/>.
    ///
    /// DfuSe memory layout strings follow the format:
    ///   @&lt;name&gt; /0x&lt;ADDRESS&gt;/&lt;count&gt;*&lt;size&gt;[modifier][perm][,...]
    ///
    /// Where modifier is a space (raw bytes), 'K' (kilobytes), or 'M' (megabytes),
    /// and perm is a letter 'a'–'g' whose lowest 3 ASCII bits encode permissions:
    ///   bit 0 = Readable, bit 1 = Writeable, bit 2 = Eraseable.
    ///
    /// Address must use uppercase hexadecimal digits (A–F).
    /// </summary>
    public class DfuSeParseLayoutTests
    {
        private const ulong KB = 1024UL;

        // ---------------------------------------------------------------
        // Valid – single block specification
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLayout_Stm32F103_SingleBlockType_32PagesOf1K()
        {
            // Typical STM32F103 (64 KB) Flash: 32 pages of 2 KB each, read-only via DFU.
            // 'a' = 0x61 & 0x07 = 1 = Readable
            var layout = Device.ParseLayout("@Internal Flash  /0x08000000/32*2Ka");

            Assert.Equal("Internal Flash", layout.Name);
            Assert.Equal(0x08000000UL, layout.StartAddress);
            Assert.Equal(32, layout.Blocks.Count);
            Assert.All(layout.Blocks, b =>
            {
                Assert.Equal(2UL * KB, b.Size);
                Assert.Equal(Permissions.Readable, b.Permissions);
            });
            Assert.Equal(0x08000000UL, layout.Blocks[0].StartAddress);
            // 31 * 2048 = 0xF800
            Assert.Equal(0x0800F800UL, layout.Blocks[31].StartAddress);
        }

        [Fact]
        public void ParseLayout_Stm32L0_SingleBlockType_1536PagesOf128Bytes()
        {
            // STM32L0 192 KB Flash: 1536 pages of 128 bytes each (no K/M modifier = raw bytes),
            // readable, writeable and eraseable.
            // 'g' = 0x67 & 0x07 = 7 = Readable | Writeable | Eraseable
            var layout = Device.ParseLayout("@Internal Flash  /0x08000000/1536*128g");

            Assert.Equal("Internal Flash", layout.Name);
            Assert.Equal(0x08000000UL, layout.StartAddress);
            Assert.Equal(1536, layout.Blocks.Count);
            Assert.All(layout.Blocks, b =>
            {
                Assert.Equal(128UL, b.Size);
                Assert.Equal(Permissions.Readable | Permissions.Writeable | Permissions.Eraseable, b.Permissions);
            });
            Assert.Equal(0x08000000UL, layout.Blocks[0].StartAddress);
            // 1535 * 128 = 0x2FF80
            Assert.Equal(0x0802FF80UL, layout.Blocks[1535].StartAddress);
            Assert.Equal(192UL * KB, layout.Size);
        }

        [Fact]
        public void ParseLayout_OptionBytes_SmallBlockWithSpaceModifier()
        {
            // STM32F1 Option Bytes: 1 block of 16 raw bytes (space = no multiplier),
            // readable and eraseable.
            // 'e' = 0x65 & 0x07 = 5 = Readable | Eraseable
            var layout = Device.ParseLayout("@Option Bytes  /0x1FFFF800/1*16 e");

            Assert.Equal("Option Bytes", layout.Name);
            Assert.Equal(0x1FFFF800UL, layout.StartAddress);
            Assert.Single(layout.Blocks);
            Assert.Equal(0x1FFFF800UL, layout.Blocks[0].StartAddress);
            Assert.Equal(16UL, layout.Blocks[0].Size);
            Assert.Equal(Permissions.Readable | Permissions.Eraseable, layout.Blocks[0].Permissions);
        }

        [Fact]
        public void ParseLayout_Ram_SingleBlock32K_FullyAccessible()
        {
            // 'g' = 0x67 & 0x07 = 7 = Readable | Writeable | Eraseable
            var layout = Device.ParseLayout("@RAM /0x20000000/1*32Kg");

            Assert.Equal("RAM", layout.Name);
            Assert.Equal(0x20000000UL, layout.StartAddress);
            Assert.Single(layout.Blocks);
            Assert.Equal(0x20000000UL, layout.Blocks[0].StartAddress);
            Assert.Equal(32UL * KB, layout.Blocks[0].Size);
            Assert.Equal(Permissions.Readable | Permissions.Writeable | Permissions.Eraseable, layout.Blocks[0].Permissions);
        }

        [Fact]
        public void ParseLayout_MegabyteModifier_CorrectBlockSize()
        {
            // 'M' modifier multiplies the raw size by 1 048 576.
            var layout = Device.ParseLayout("@RAM /0x20000000/1*1Mg");

            Assert.Single(layout.Blocks);
            Assert.Equal(1024UL * KB, layout.Blocks[0].Size);
        }

        [Fact]
        public void ParseLayout_ReadWritePermission_WriteableOnly()
        {
            // 'b' = 0x62 & 0x07 = 2 = Writeable
            // 'c' = 0x63 & 0x07 = 3 = Readable | Writeable
            var layout = Device.ParseLayout("@Flash /0x08000000/1*4Kb,1*4Kc");

            Assert.Equal(2, layout.Blocks.Count);
            Assert.Equal(Permissions.Writeable, layout.Blocks[0].Permissions);
            Assert.Equal(Permissions.Readable | Permissions.Writeable, layout.Blocks[1].Permissions);
        }

        // ---------------------------------------------------------------
        // Valid – multiple block specifications
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLayout_Stm32F4_ThreeBlockTypes_CorrectCountSizesPermissions()
        {
            // STM32F405/407 1 MB Flash layout:
            //   sector 0–3  : 4 × 16 KB  – Readable | Eraseable ('e')
            //   sector 4    : 1 × 64 KB  – Readable | Writeable | Eraseable ('g')
            //   sectors 5–11: 7 × 128 KB – Readable | Writeable | Eraseable ('g')
            var layout = Device.ParseLayout("@Internal Flash  /0x08000000/4*16Ke,1*64Kg,7*128Kg");

            Assert.Equal("Internal Flash", layout.Name);
            Assert.Equal(0x08000000UL, layout.StartAddress);
            Assert.Equal(12, layout.Blocks.Count);

            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(16UL * KB, layout.Blocks[i].Size);
                Assert.Equal(Permissions.Readable | Permissions.Eraseable, layout.Blocks[i].Permissions);
            }

            Assert.Equal(64UL * KB, layout.Blocks[4].Size);
            Assert.Equal(Permissions.Readable | Permissions.Writeable | Permissions.Eraseable, layout.Blocks[4].Permissions);

            for (int i = 5; i < 12; i++)
            {
                Assert.Equal(128UL * KB, layout.Blocks[i].Size);
                Assert.Equal(Permissions.Readable | Permissions.Writeable | Permissions.Eraseable, layout.Blocks[i].Permissions);
            }
        }

        [Fact]
        public void ParseLayout_Stm32F4_BlockAddressesAreContinuousFromBase()
        {
            var layout = Device.ParseLayout("@Internal Flash  /0x08000000/4*16Ke,1*64Kg,7*128Kg");

            // 16 KB sectors (0x4000 each)
            Assert.Equal(0x08000000UL, layout.Blocks[0].StartAddress);
            Assert.Equal(0x08004000UL, layout.Blocks[1].StartAddress);
            Assert.Equal(0x08008000UL, layout.Blocks[2].StartAddress);
            Assert.Equal(0x0800C000UL, layout.Blocks[3].StartAddress);
            // 64 KB sector (0x10000)
            Assert.Equal(0x08010000UL, layout.Blocks[4].StartAddress);
            // 128 KB sectors (0x20000 each)
            Assert.Equal(0x08020000UL, layout.Blocks[5].StartAddress);
            Assert.Equal(0x08040000UL, layout.Blocks[6].StartAddress);
            Assert.Equal(0x080E0000UL, layout.Blocks[11].StartAddress);
        }

        [Fact]
        public void ParseLayout_Stm32F4_TotalSizeAndEndAddressAreCorrect()
        {
            var layout = Device.ParseLayout("@Internal Flash  /0x08000000/4*16Ke,1*64Kg,7*128Kg");

            // 4×16 K + 1×64 K + 7×128 K = 64 K + 64 K + 896 K = 1024 K = 1 MB
            ulong expectedSize = 4 * 16UL * KB + 1 * 64UL * KB + 7 * 128UL * KB;
            Assert.Equal(expectedSize, layout.Size);
            Assert.Equal(layout.StartAddress + expectedSize - 1, layout.EndAddress);
        }

        [Fact]
        public void ParseLayout_TwoBlockSpecs_AddressesAreContinuous()
        {
            // 2 × 64 KB then 2 × 128 KB
            var layout = Device.ParseLayout("@Flash /0x08000000/2*64Kg,2*128Kg");

            Assert.Equal(4, layout.Blocks.Count);
            Assert.Equal(0x08000000UL, layout.Blocks[0].StartAddress);
            Assert.Equal(0x08010000UL, layout.Blocks[1].StartAddress);  // + 64 K
            Assert.Equal(0x08020000UL, layout.Blocks[2].StartAddress);  // + 64 K
            Assert.Equal(0x08040000UL, layout.Blocks[3].StartAddress);  // + 128 K
        }

        [Fact]
        public void ParseLayout_SystemMemory_SingleBlock_AtHighAddress()
        {
            // STM32F4 System Memory (read-only boot ROM):
            // 1 × 30 KB, Readable only ('a').
            var layout = Device.ParseLayout("@System Memory /0x1FFF0000/1*30Ka");

            Assert.Equal("System Memory", layout.Name);
            Assert.Equal(0x1FFF0000UL, layout.StartAddress);
            Assert.Single(layout.Blocks);
            Assert.Equal(30UL * KB, layout.Blocks[0].Size);
            Assert.Equal(Permissions.Readable, layout.Blocks[0].Permissions);
        }

        // ---------------------------------------------------------------
        // Invalid – null / empty input
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLayout_NullInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Device.ParseLayout(null));
        }

        [Fact]
        public void ParseLayout_EmptyString_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Device.ParseLayout(""));
        }

        // ---------------------------------------------------------------
        // Invalid – structural / format errors
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLayout_MissingAtPrefix_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Device.ParseLayout("Internal Flash /0x08000000/1*64Kg"));
        }

        [Fact]
        public void ParseLayout_NoName_ThrowsArgumentException()
        {
            // '/' immediately after '@' is not a word character, so the name group fails.
            Assert.Throws<ArgumentException>(() => Device.ParseLayout("@/0x08000000/1*64Kg"));
        }

        [Fact]
        public void ParseLayout_MissingAddressPrefix_ThrowsArgumentException()
        {
            // Missing the '/0x…' address segment entirely.
            Assert.Throws<ArgumentException>(() => Device.ParseLayout("@Internal Flash/1*64Kg"));
        }

        [Fact]
        public void ParseLayout_MissingBlockSpec_ThrowsArgumentException()
        {
            // String ends after the address with no block-descriptor segment.
            Assert.Throws<ArgumentException>(() => Device.ParseLayout("@Internal Flash /0x08000000"));
        }

        [Fact]
        public void ParseLayout_LowercaseHexInAddress_ThrowsArgumentException()
        {
            // Address regex [A-F0-9]{1,8} is case-sensitive; lowercase letters are rejected.
            Assert.Throws<ArgumentException>(() => Device.ParseLayout("@Internal Flash /0x0800abcd/1*1Kg"));
        }

        [Fact]
        public void ParseLayout_InvalidPermissionChar_ThrowsArgumentException()
        {
            // 'h' is outside the permitted [a-g] range.
            Assert.Throws<ArgumentException>(() => Device.ParseLayout("@Internal Flash /0x08000000/1*64Kh"));
        }

        [Fact]
        public void ParseLayout_InvalidSizeModifier_ThrowsArgumentException()
        {
            // 'P' is not in the allowed modifier set [space, K, M].
            // With 'P' unmatched as a modifier, 'P' is then tested as the permission
            // character and also rejected (not in [a-g]), causing a match failure.
            Assert.Throws<ArgumentException>(() => Device.ParseLayout("@Internal Flash /0x08000000/1*64Pg"));
        }

        [Fact]
        public void ParseLayout_TrailingCommaAfterLastBlockSpec_ThrowsArgumentException()
        {
            // A trailing comma signals an additional block spec that is absent.
            Assert.Throws<ArgumentException>(() => Device.ParseLayout("@Internal Flash /0x08000000/1*64Kg,"));
        }

        [Fact]
        public void ParseLayout_GarbageInput_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Device.ParseLayout("GARBAGE"));
        }
    }
}
