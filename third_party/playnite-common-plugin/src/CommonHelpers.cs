using ByteSizeLib;
using System;
using System.IO;
using Playnite.SDK.Data;
using System.Globalization;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace CommonPlugin
{
    public class CommonHelpers
    {
        public Plugin plugin { get; set; }

        public CommonHelpers(Plugin plugin)
        {
            this.plugin = plugin;
        }

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

        public static double ToBytes(double size, string unit)
        {
            return ByteSize.Parse($"{size} {unit}").Bytes;
        }

        public void SaveJsonSettingsToFile(object jsonSettings, string path, string fileName, bool insidePluginUserData = false)
        {
            var strConf = Serialization.ToJson(jsonSettings, true);
            if (!strConf.IsNullOrEmpty())
            {
                if (insidePluginUserData)
                {
                    path = Path.Combine(plugin.GetPluginUserDataPath(), path);
                }
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                var dataFile = Path.Combine(path, $"{fileName}.json");
                File.WriteAllText(dataFile, strConf);
            }
        }

        public static bool IsDirectoryWritable(string folderPath, string permissionErrorString)
        {
            try
            {
                using (FileStream fs = File.Create(Path.Combine(folderPath, Path.GetRandomFileName()),
                                                   1,
                                                   FileOptions.DeleteOnClose)
                )
                { }
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                var playniteAPI = API.Instance;
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(permissionErrorString));
                return false;
            }
            catch (Exception ex)
            {
                var logger = LogManager.GetLogger();
                logger.Error($"An error occured during checking if directory {folderPath} is writable: {ex.Message}");
                return true;
            }
        }

        public static double ToDouble(string value)
        {
            double result;

            // Try parsing in the current culture
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result) &&
                // Then try in US english
                !double.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("en-US"), out result) &&
                // Then in neutral language
                !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                result = 0;
            }

            return result;
        }

        public static int CpuThreadsNumber
        {
            get
            {
                return Environment.ProcessorCount;
            }
        }
    }
}
