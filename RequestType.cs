using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibUsbDfu
{
    public struct RequestType
    {
        public enum Direction : byte
        {
            Out = 0 << 7,
            In = 1 << 7,
        }
        public enum Type : byte
        {
            Standard = 0 << 5,
            Class = 1 << 5,
            Vendor = 2 << 5,
        }
        public enum Recipient : byte
        {
            Device = 0 << 0,
            Interface = 1 << 0,
            Endpoint = 2 << 0,
            Other = 3 << 0,
        }
        private readonly byte value;

        public RequestType(Direction dir, Type type, Recipient rec)
        {
            value = (byte)((byte)dir | (byte)type | (byte)rec);
        }
        private RequestType(byte val)
        {
            value = val;
        }
        public static implicit operator byte(RequestType r)
        {
            return r.value;
        }
        public static explicit operator RequestType(byte val)
        {
            return new RequestType(val);
        }
        public override string ToString()
        {
            return value.ToString();
        }
    }
}
