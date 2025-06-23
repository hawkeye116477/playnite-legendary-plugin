using ByteSizeLib;
using System;
using System.IO;
using Playnite.SDK.Data;
using System.Globalization;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using System.Windows;
using System.Reflection;
using Playnite.Common;

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

        public static string NormalizePath(string path) => Path.GetFullPath(new Uri(path).LocalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        public void LoadNeededResources(bool icons = true, bool styles = true)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            if (icons)
            {
                ResourceDictionary iconsDict = new ResourceDictionary
                {
                    Source = new Uri($"/{GetType().Assembly.GetName().Name};component/Shared/Resources/Icons.xaml", UriKind.RelativeOrAbsolute)
                };
                dictionaries.Add(iconsDict);
            }
            if (styles)
            {
                var resDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");
                var stylesName = "NormalStyles.xaml";
                if (plugin.PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
                {
                    stylesName = "FullScreenStyles.xaml";
                }
                ResourceDictionary res = Xaml.FromFile<ResourceDictionary>(Path.Combine(resDir, stylesName));
                dictionaries.Add(res);
            }
        }

        public static void SetControlBackground(DependencyObject windowDependency)
        {
            var playniteAPI = API.Instance;
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                var thisWindow = Window.GetWindow(windowDependency);
                thisWindow.Background = (System.Windows.Media.Brush)ResourceProvider.GetResource("ControlBackgroundBrush");
            }
        }
    }
}
