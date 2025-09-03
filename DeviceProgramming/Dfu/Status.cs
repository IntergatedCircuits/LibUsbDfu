using System;
using System.Runtime.InteropServices;

namespace DeviceProgramming.Dfu
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Status
    {
        /// <summary>
        /// Total size of this structure in bytes.
        /// </summary>
        public static readonly int Size = Marshal.SizeOf(typeof(Status));

        /// <summary>
        /// An indication of the status resulting from the execution of the most recent request.
        /// </summary>
        public readonly Dfu.Error Error;

        private uint pollTimeout24_state8;

        /// <summary>
        /// Minimum time, in milliseconds, that the host should wait before sending a subsequent DFU_GETSTATUS request.
        /// </summary>
        public int PollTimeout { get { return (int)(pollTimeout24_state8 & 0xFFFFFF); } }

        /// <summary>
        /// An indication of the state that the device is going to enter immediately following transmission of this response.
        /// (By the time the host receives this information, this is the current state of the device.)
        /// </summary>
        public State State { get { return (State)(pollTimeout24_state8 >> 24); } }

        /// <summary>
        /// Index of status description in string table.
        /// </summary>
        public readonly byte iString;

        public Status(byte[] data, int offset = 0)
        {
            if ((data.Length - offset) < Size)
            {
                throw new ArgumentException("Invalid Status buffer size", "data");
            }

            this.Error = (Dfu.Error)data[offset];
            pollTimeout24_state8 = (uint)((data[offset + 4] << 24) | (data[offset + 3] << 16) | (data[offset + 2] << 8) | (data[offset + 1]));
            iString = data[offset + 5];
        }

        public Status(State state, int pollTimeout, Error error = Dfu.Error.Ok, byte iString = 0)
        {
            pollTimeout24_state8 = ((uint)pollTimeout & 0xFFFFFF) | ((uint)state << 24);
            this.Error = error;
            this.iString = iString;
        }
    }
}
