using Microsoft.Win32;
using System;
using System.IO;
using System.Management;

namespace LegendaryLibraryNS
{
    public class Helpers
    {
        public static int TotalRAM
        {
            get
            {
                var wql = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                var searcher = new ManagementObjectSearcher(wql);
                var results = searcher.Get();
                var ram = 0.0;
                foreach (ManagementObject result in results)
                {
                    ram = Convert.ToDouble(result["TotalVisibleMemorySize"].ToString().Replace("KB", ""));
                }

                ram = Math.Round(ram / 1024);
                return Convert.ToInt32(ram);
            }
        }

        public static bool IsFileLocked(string filePath)
        {
            try
            {
                using var inputStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                inputStream.Close();
            }
            catch (Exception)
            {
                return true;
            }

            return false;
        }

        public static string GetMachineGuid()
        {
            var machineGuid = "";
            var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);

            try
            {
                using var cryptography = root.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                if (cryptography != null)
                {
                    machineGuid = Guid.Parse(cryptography.GetValue("MachineGuid")?.ToString() ?? "").ToString();
                }
            }
            finally
            {
                root.Dispose();
            }

            if (machineGuid.IsNullOrEmpty())
            {
                machineGuid = LegendaryLibrary.GetSettings().SyncPlaytimeMachineId;
            }

            return machineGuid;
        }
    }
}