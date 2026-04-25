using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LegendaryLibraryNS
{
    public class Helpers
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MemoryStatusEx
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }
        
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);
        
        public static int TotalRam
        {
            get
            {
                MemoryStatusEx memStatus = new();
                if (!GlobalMemoryStatusEx(memStatus))
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
