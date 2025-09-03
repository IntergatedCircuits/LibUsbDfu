using System;

namespace DeviceProgramming.Memory
{
    /// <summary>
    /// This class represents a memory unit that can be read/written/erased (depending on permissions) as a whole.
    /// </summary>
    public readonly struct Block : IEquatable<Block>, IComparable<Block>
    {
        public readonly UInt64 StartAddress;
        public readonly UInt64 Size;
        public readonly Memory.Permissions Permissions;
        public UInt64 EndAddress { get { return StartAddress + Size - 1; } }

        public Block(ulong startAddress, ulong size, Memory.Permissions permissions = Permissions.Inaccessible)
        {
            StartAddress = startAddress;
            Size = size;
            Permissions = permissions;
        }

        public bool Overlaps(Block other)
        {
            if (this.StartAddress < other.StartAddress)
            {
                return other.StartAddress <= (this.StartAddress + this.Size - 1);
            }
            else
            {
                return this.StartAddress <= (other.StartAddress + other.Size - 1);
            }
        }

        public bool Equals(Block other)
        {
            return this.StartAddress == other.StartAddress && this.Size == other.Size && this.Permissions == other.Permissions;
        }
        public override bool Equals(object obj)
        {
            if (obj is Block)
                return Equals((Block)obj);
            return base.Equals(obj);
        }
        public static bool operator ==(Block a, Block b)
        {
            return a.Equals(b);
        }
        public static bool operator !=(Block a, Block b)
        {
            return !(a == b);
        }
        public int CompareTo(Block other)
        {
            return this.StartAddress.CompareTo(other.StartAddress);
        }
    }
}
