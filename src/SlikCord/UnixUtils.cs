using System.Runtime.InteropServices;

namespace Slik.Cord
{
    public static class UnixUtils
    {
        public static bool IsUnixFamily() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD);
    }
}
