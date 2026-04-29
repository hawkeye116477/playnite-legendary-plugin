using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LegendaryLibraryNS
{
    public static partial class Helpers
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private readonly struct MemoryStatusEx()
        {
            public readonly uint dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();
            public readonly uint dwMemoryLoad;
            public readonly ulong ullTotalPhys;
            public readonly ulong ullAvailPhys;
            public readonly ulong ullTotalPageFile;
            public readonly ulong ullAvailPageFile;
            public readonly ulong ullTotalVirtual;
            public readonly ulong ullAvailVirtual;
            public readonly ulong ullAvailExtendedVirtual;
        }

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

        public static int TotalRam
        {
            get
            {
                MemoryStatusEx memStatus = new();
                if (!GlobalMemoryStatusEx(ref memStatus))
                {
                    return 0;
                }

                ulong ram = memStatus.ullTotalPhys / (1024 * 1024);
                return Convert.ToInt32(ram);
            }
        }

        public static bool IsFileLocked(string filePath)
        {
            try
            {
                using FileStream inputStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                inputStream.Close();
            }
            catch (Exception)
            {
                return true;
            }

            return false;
        }
    }
}