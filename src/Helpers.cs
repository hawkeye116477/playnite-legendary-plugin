using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryLibraryNS
{
    public class Helpers
    {
        static readonly string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        public static string FormatSize(double size)
        {
            int i = 0;
            decimal number = (decimal)size;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                i++;
            }
            return string.Format("{0:n2} {1}", number, suffixes[i]);
        }

        public static int TotalRAM
        {
            get
            {
                ObjectQuery wql = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(wql);
                ManagementObjectCollection results = searcher.Get();
                double ram = 0.0;
                foreach (ManagementObject result in results)
                {
                    ram = Convert.ToDouble(result["TotalVisibleMemorySize"].ToString().Replace("KB", ""));
                }
                ram = Math.Round(ram / 1024);
                return Convert.ToInt32(ram);
            }
        }
    }
}
