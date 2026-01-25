using System;
using System.Collections.Generic;
using System.Linq;

namespace DeviceProgramming.Memory
{
    /// <summary>
    /// This class represents a raw memory image with data segments at absolute addresses.
    /// </summary>
    public class RawMemory : IEquatable<RawMemory>
    {
        public List<Segment> Segments { get; protected set; }

        public RawMemory()
        {
            Segments = new List<Segment>();
        }

        public bool TryAddSegment(Segment newSegment)
        {
            if (Segments.Any((s) => s.Overlaps(newSegment)))
            {
                return false;
            }
            for (int i = 0; i < Segments.Count; i++)
            {
                var segment = Segments[i];
                if (newSegment.TryAppend(segment))
                {
                    // the new segment now includes the old one
                    Segments[i] = newSegment;
                    return true;
                }
                if (segment.TryAppend(newSegment))
                {
                    // the old segment now includes the new one
                    return true;
                }
            }
            // no overlaps or extensions, just add it
            Segments.Add(newSegment);
            Segments.Sort();
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is RawMemory)
                return Equals((RawMemory)obj);
            return base.Equals(obj);
        }
        public bool Equals(RawMemory other)
        {
            return ReferenceEquals(this, other) || this.Segments.SequenceEqual(other.Segments);
        }
        public static bool operator ==(RawMemory a, RawMemory b)
        {
            return a.Equals(b);
        }
        public static bool operator !=(RawMemory a, RawMemory b)
        {
            return !(a == b);
        }
    }
}
