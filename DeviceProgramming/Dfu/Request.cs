namespace DeviceProgramming.Dfu
{
    public enum Request : byte
    {
        Detach = 0,     /// Detach the application from the USB device
        Dnload = 1,     /// Download a block to the program memory
        Upload = 2,     /// Read a block of the program memory
        GetStatus = 3,  /// Read the DFU status
        ClrStatus = 4,  /// Clear the error status
        GetState = 5,   /// Read the DFU state
        Abort = 6,      /// Abort the ongoing request
    }
}
