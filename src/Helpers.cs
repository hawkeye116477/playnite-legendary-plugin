using System;
using System.IO;
using System.Management;
using ByteSizeLib;
using Playnite.SDK.Data;

namespace LegendaryLibraryNS
{
    public class Helpers
    {
        public static string FormatSize(double size, string unit = "B", bool toBits = false)
        {
            if (toBits)
            {
                size *= 8;
            }
            var finalSize = ByteSize.Parse($"{size} {unit}").ToBinaryString();
            if (toBits)
            {
                finalSize = finalSize.Replace("B", "b");
            }
            return finalSize.Replace("i", "");
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

        /// <summary>
        /// Returns a relative path string from a full path based on a base path
        /// provided.
        /// Found on https://weblog.west-wind.com/posts/2010/Dec/20/Finding-a-Relative-Path-in-NET and improved a little
        /// </summary>
        /// <param name="basePath">The base path on which relative processing is based. Should be a directory.</param>
        /// <param name="fullPath">The path to convert. Can be either a file or a directory</param>
        /// <returns>
        /// String of the relative path.
        /// 
        /// Examples of returned values:
        ///  test.txt, ..\test.txt, ..\..\..\test.txt, ., .., subdir\test.txt
        /// </returns>
        public static string GetRelativePath(string basePath, string fullPath)
        {
            // Require trailing backslash for path
            if (!basePath.EndsWith("\\"))
                basePath += "\\";

            Uri baseUri = new Uri(basePath);
            Uri fullUri = new Uri(fullPath);

            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);

            // Uri's use forward slashes so convert back to backward slashes
            // Uri's also escape some chars, co convert back to unescaped format
            return Uri.UnescapeDataString(relativeUri.ToString().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        }

        public static void SaveJsonSettingsToFile(object jsonSettings, string fileName)
        {
            var strConf = Serialization.ToJson(jsonSettings, true);
            if (!strConf.IsNullOrEmpty())
            {
                var dataDir = LegendaryLibrary.Instance.GetPluginUserDataPath();
                if (!Directory.Exists(dataDir))
                {
                    Directory.CreateDirectory(dataDir);
                }
                var dataFile = Path.Combine(dataDir, $"{fileName}.json");
                File.WriteAllText(dataFile, strConf);
            }
        }
    }
}
