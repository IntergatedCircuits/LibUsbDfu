using System;

namespace DeviceProgramming.Memory
{
    [Flags]
    public enum Permissions
    {
        Inaccessible = 0,
        Readable = 1,
        Writeable = 2,
        Eraseable = 4,
    }

    public static class PermissionMethods
    {
        public static bool IsReadable(this Permissions type)
        {
            return (type & Permissions.Readable) != 0;
        }
        public static bool IsWriteable(this Permissions type)
        {
            return (type & Permissions.Writeable) != 0;
        }
        public static bool IsEraseable(this Permissions type)
        {
            return (type & Permissions.Eraseable) != 0;
        }
    }
}
