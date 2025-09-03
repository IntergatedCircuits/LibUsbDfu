namespace DeviceProgramming.Dfu
{
    public enum State : byte
    {
        AppIdle = 0,            /// Device is running its normal application.
        AppDetach = 1,          /// Device is running its normal application, has received the DFU_DETACH request,
                                /// and is waiting for a USB reset.
        Idle = 2,               /// Device is waiting for requests in DFU mode.
        DnloadSync = 3,         /// Device has received a block and is waiting for the host to solicit the status via DFU_GETSTATUS.
        DnloadBusy = 4,         /// Device is programming a control-write block into its non-volatile memories.
        DnloadIdle = 5,         /// Device is processing a download operation.
        ManifestSync = 6,       /// Device has received the final block of firmware from the host
                                /// and is waiting for receipt of DFU_GETSTATUS to begin the Manifestation phase;
                                /// or device has completed the Manifestation phase and is waiting for receipt of DFU_GETSTATUS.
        Manifest = 7,           /// Device is in the Manifestation phase.
        ManifestWaitReset = 8,  /// Device has programmed its memories and is waiting for a USB reset or a power on reset.
        UploadIdle = 9,         /// The device is processing an upload operation.
        Error = 10,             /// An error has occurred.
    }

    public static class StateMethods
    {
        /// <summary>
        /// Determines if the state permits executing an Abort command.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static bool Abortable(this State state)
        {
            // nothing is aborted when already in idle
            return //(state == State.Idle) ||
                (state == State.DnloadSync) ||
                (state == State.DnloadIdle) ||
                (state == State.ManifestSync) ||
                (state == State.UploadIdle);
        }

        /// <summary>
        /// Determines if the state is an Application state, i.e. the device needs reconfiguration
        /// before any other DFU operation can be performed.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static bool IsAppState(this State state)
        {
            return (state < State.Idle);
        }
    }
}
