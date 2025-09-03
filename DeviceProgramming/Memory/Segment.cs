using System;
using System.Collections.Generic;
using System.Linq;

namespace DeviceProgramming.Memory
{
    /// <summary>
    /// This class represents a contiguous memory content starting at an absolute address.
    /// </summary>
    public class Segment : IEquatable<Segment>, IComparable<Segment>
    {
        public UInt64 StartAddress { get; protected set; }
        public UInt64 EndAddress { get { return StartAddress + (ulong)Data.LongLength - 1; } }
        private byte[] data;

        public byte[] Data
        {
            get { return data; }
        }

        public Segment(ulong startAddress, byte[] data)
        {
            this.StartAddress = startAddress;
            this.data = data;
        }

        public int Length
        {
            get { return Data.Length; }
        }

        public bool Equals(Segment other)
        {
            return this.StartAddress == other.StartAddress && this.Data.SequenceEqual(other.Data);
        }
        public override bool Equals(object obj)
        {
            if (obj is Segment)
                return Equals((Segment)obj);
            return base.Equals(obj);
        }
        public static bool operator ==(Segment a, Segment b)
        {
            return a.Equals(b);
        }
        public static bool operator !=(Segment a, Segment b)
        {
            return !(a == b);
        }

        public bool Overlaps(Segment other)
        {
            if (this.StartAddress < other.StartAddress)
            {
                return other.StartAddress <= this.EndAddress;
            }
            else
            {
                return this.StartAddress <= other.EndAddress;
            }
        }

        public bool Extends(Segment other)
        {
            return this.StartAddress == (other.EndAddress + 1);
        }

        public bool TryAppend(Segment other)
        {
            if (!other.Extends(this))
            {
                return false;
            }
            else
            {
                int dlen = data.Length;
                Array.Resize<byte>(ref data, data.Length + other.Data.Length);
                Array.Copy(other.Data, 0, data, dlen, other.Data.Length);
                return true;
            }
        }

        public bool ContainsKey(UInt64 key)
        {
            return (key >= StartAddress) && (key <= EndAddress);
        }

        public bool TryGetValue(UInt64 key, out byte value)
        {
            if (!ContainsKey(key))
            {
                value = 0;
                return false;
            }
            else
            {
                value = this[key];
                return true;
            }
        }

        public ICollection<byte> Values
        {
            get { return Data; }
        }

        public byte this[UInt64 key]
        {
            get
            {
                return Data[key - StartAddress];
            }
            set
            {
                Data[key - StartAddress] = value;
            }
        }

        public bool Contains(KeyValuePair<UInt64, byte> item)
        {
            byte v;
            return TryGetValue(item.Key, out v) && (v == item.Value);
        }

        public int Count
        {
            get { return Values.Count; }
        }

        public int CompareTo(Segment other)
        {
            return this.StartAddress.CompareTo(other.StartAddress);
        }
    }
}
