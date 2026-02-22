using System;
using System.Runtime.InteropServices;

namespace DeviceProgramming
{
    public static class ByteArray
    {
        /// <summary>
        /// Converts a byte array to a fixed size managed struct.
        /// </summary>
        /// <typeparam name="T">Type of the resulting struct</typeparam>
        /// <param name="bytes">The struct data in raw bytes</param>
        /// <returns>The struct created by the byte array input</returns>
        public static T ToStruct<T>(this byte[] bytes) where T : struct
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            int size = Marshal.SizeOf(typeof(T));
            if (bytes.Length < size)
                throw new ArgumentException("Byte array is too small for the requested structure.", nameof(bytes));

            T s;
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                s = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
            return s;
        }

        /// <summary>
        /// Converts a fixed size struct to a byte array.
        /// </summary>
        /// <param name="obj">The structure to convert</param>
        /// <returns>The struct data in raw bytes</returns>
        public static byte[] ToBytes(this object obj)
        {
            int size = Marshal.SizeOf(obj);
            byte[] arr = new byte[size];

            GCHandle handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(handle.AddrOfPinnedObject(), arr, 0, size);
            }
            finally
            {
                handle.Free();
            }
            return arr;
        }
    }
}
