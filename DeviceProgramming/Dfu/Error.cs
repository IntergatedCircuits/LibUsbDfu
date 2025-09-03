namespace DeviceProgramming.Dfu
{
    public enum Error : byte
    {
        Ok = 0x00,          /// No error condition is present.
        Target = 0x01,      /// File is not targeted for use by this device.
        File = 0x02,        /// File is for this device but fails some vendor-specific verification test.
        Write = 0x03,       /// Device is unable to write memory.
        Erase = 0x04,       /// Memory erase function failed.
        CheckErased = 0x05, /// Memory erase check failed.
        Prog = 0x06,        /// Program memory function failed.
        Verify = 0x07,      /// Programmed memory failed verification.
        Address = 0x08,     /// Cannot program memory due to received address that is out of range.
        NotDone = 0x09,     /// Received DFU_DNLOAD with wLength = 0, but device does not think it has all of the data yet.
        Firmware = 0x0A,    /// Device's firmware is corrupt. It cannot return to run-time (non-DFU) operations.
        Vendor = 0x0B,      /// iString indicates a vendor-specific error.
        USB = 0x0C,         /// Device detected unexpected USB reset signaling.
        POR = 0x0D,         /// Device detected unexpected power on reset.
        Unknown = 0x0E,     /// Something went wrong.
        StalledPkt = 0x0F,  /// Device stalled an unexpected request.
    }
}
