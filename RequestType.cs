using LibUsbDotNet.Main;

namespace LibUsbDfu
{
    public struct RequestType
    {
        private readonly byte value;

        public RequestType(UsbEndpointDirection dir, UsbRequestType type, UsbRequestRecipient rec)
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
