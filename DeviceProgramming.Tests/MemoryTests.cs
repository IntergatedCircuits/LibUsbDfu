using DeviceProgramming.Memory;
using System;
using System.Linq;
using Xunit;

namespace DeviceProgramming.Tests
{
    public class MemoryTests
    {
        [Fact]
        public void Block_Equals_Compare_And_Overlap()
        {
            var a = new Block(0, 10);
            var b = new Block(0, 10);
            var c = new Block(5, 10);
            var d = new Block(20, 5);

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.False(a != b);

            Assert.True(a.Overlaps(c));
            Assert.True(c.Overlaps(a));
            Assert.False(a.Overlaps(d));

            Assert.True(a.CompareTo(b) == 0);
            Assert.True(a.CompareTo(c) < 0);
            Assert.True(d.CompareTo(c) > 0);
        }

        [Fact]
        public void Segment_Indexer_TryGetValue_Contains_And_Merge()
        {
            var seg1 = new Segment(0x1000, new byte[] { 1, 2, 3 });
            var seg2 = new Segment(0x1003, new byte[] { 4, 5 });
            var seg3 = new Segment(0x2000, new byte[] { 9 });

            // ContainsKey / Contains / TryGetValue / indexer
            Assert.True(seg1.ContainsKey(0x1000));
            Assert.True(seg1.ContainsKey(0x1002));
            Assert.False(seg1.ContainsKey(0x0FFF));

            Assert.True(seg1.TryGetValue(0x1001, out byte v));
            Assert.Equal(2, v);

            seg1[0x1002] = 7;
            Assert.Equal(7, seg1[0x1002]);

            // Merge: seg2 extends seg1
            Assert.True(seg2.Extends(seg1));
            Assert.True(seg1.TryMerge(seg2));
            Assert.Equal(5, seg1.Length);
            Assert.Equal(new byte[] { 1, 2, 7, 4, 5 }, seg1.Data);

            // seg3 does not overlap or extend
            Assert.False(seg1.Overlaps(seg3));
            Assert.False(seg1.Extends(seg3));
            Assert.False(seg1.TryMerge(seg3));
        }

        [Fact]
        public void RawMemory_AddSegments_Merge_RejectOverlap_And_Sort()
        {
            var m = new RawMemory();

            // Create a middle segment and then append and prepend to it
            var sMid = new Segment(0x10u, new byte[] { 1, 2 }); // covers 0x10..0x11
            var sAppend = new Segment(0x12u, new byte[] { 3 }); // extends after sMid
            var sPrepend = new Segment(0x0Eu, new byte[] { 9, 8 }); // extends before sMid

            // Add middle
            Assert.True(m.TryAddSegment(sMid));
            Assert.Single(m.Segments);
            Assert.Equal((ulong)0x10, m.Segments[0].StartAddress);
            Assert.Equal(2, m.Segments[0].Length);

            // Append to the right
            Assert.True(m.TryAddSegment(sAppend));
            Assert.Single(m.Segments);
            Assert.Equal(3, m.Segments[0].Length);
            Assert.Equal(new byte[] { 1, 2, 3 }, m.Segments[0].Data);

            // Prepend to the left
            Assert.True(m.TryAddSegment(sPrepend));
            Assert.Single(m.Segments);
            Assert.Equal((ulong)0x0E, m.Segments[0].StartAddress);
            Assert.Equal(5, m.Segments[0].Length);
            Assert.Equal(new byte[] { 9, 8, 1, 2, 3 }, m.Segments[0].Data);

            // Now add a distinct non-overlapping segment and verify it is accepted
            var sOther = new Segment(0x30u, new byte[] { 7 });
            Assert.True(m.TryAddSegment(sOther));
            Assert.Equal(2, m.Segments.Count);

            // Overlapping segment should be rejected
            var sOverlap = new Segment(0x11u, new byte[] { 99 }); // overlaps existing merged segment
            Assert.False(m.TryAddSegment(sOverlap));

            // ensure segments are sorted by start address
            Assert.True(m.Segments.SequenceEqual(m.Segments.OrderBy(s => s.StartAddress)));
        }

        [Fact]
        public void Layout_AppendBlock_Validates_Contiguity()
        {
            var layout = new Layout();
            var b1 = new Block(0x100, 0x10);
            var b2 = new Block(0x110, 0x10);
            var bad = new Block(0x200, 0x10);

            layout.AppendBlock(b1);
            Assert.Equal((ulong)0x100, layout.StartAddress);
            Assert.Equal((ulong)0x10, layout.Size);

            layout.AppendBlock(b2);
            Assert.Equal((ulong)0x100, layout.StartAddress);
            Assert.Equal((ulong)0x20, layout.Size);

            Assert.Throws<ArgumentException>(() => layout.AppendBlock(bad));
        }
    }
}
