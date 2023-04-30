using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryLibraryNS
{
    class Helpers
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
    }
}
