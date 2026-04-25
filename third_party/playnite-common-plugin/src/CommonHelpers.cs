using ByteSizeLib;
using System;
using System.IO;
using System.Globalization;
using Playnite;
using System.Windows;
using System.Reflection;
using System.Threading.Tasks;
using Playnite.Common;

namespace CommonPlugin
{
    public class CommonHelpers
    {
        public IPlayniteApi PlayniteApi { get; set; }

        public CommonHelpers(IPlayniteApi playniteApi)
        {
            this.PlayniteApi = playniteApi;
        }

        public static string FormatSize(double size, string unit = "B", bool toBits = false)
        {
            var logger = LogManager.GetLogger();
            if (toBits)
            {
                size *= 8;
            }
            if (size < 0)
            {
                logger.Warn($"Invalid size: {size}");
                size = 0;
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
            var logger = LogManager.GetLogger();
            if (size < 0)
            {
                logger.Warn($"Invalid size: {size}");
                size = 0;
            }
            return ByteSize.Parse($"{size} {unit}").Bytes;
        }

        public void SaveJsonSettingsToFile(object jsonSettings, string path, string fileName, bool insidePluginUserData = false)
        {
            var strConf = Serialization.ToJson(jsonSettings, true);
            if (!strConf.IsNullOrEmpty())
            {
                if (insidePluginUserData)
                {
                    path = Path.Combine(PlayniteApi.UserDataDir, path);
                }
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                var dataFile = Path.Combine(path, $"{fileName}.json");
                File.WriteAllText(dataFile, strConf);
            }
        }

        public async Task<bool> IsDirectoryWritable(string? folderPath, string permissionErrorString = "")
        {
            try
            {
                Directory.CreateDirectory(folderPath);
                await using (var fs = File.Create(Path.Combine(folderPath, Path.GetRandomFileName()),
                                 1,
                                 FileOptions.DeleteOnClose)
                            )
                { }
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                if (permissionErrorString != "")
                {
                    await PlayniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(permissionErrorString));
                }
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
            // Try parsing in the current culture
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var result) &&
                // Then try in US english
                !double.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("en-US"), out result) &&
                // Then in neutral language
                !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                result = 0;
            }

            return result;
        }

        public static int CpuThreadsNumber => Environment.ProcessorCount;

        public static string NormalizePath(string path) => Path.GetFullPath(new Uri(path).LocalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        public void LoadNeededResources(bool styles = true)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            if (styles)
            {
                var resDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");
                var stylesName = "NormalStyles.xaml";
                if (PlayniteApi.AppInfo.Mode == AppMode.Fullscreen)
                {
                    stylesName = "FullScreenStyles.xaml";
                }
                ResourceDictionary res = Xaml.FromFile<ResourceDictionary>(Path.Combine(resDir, stylesName));
                dictionaries.Add(res);
            }
        }

        public void SetControlBackground(DependencyObject windowDependency)
        {
            if (PlayniteApi.AppInfo.Mode == AppMode.Fullscreen)
            {
                var thisWindow = Window.GetWindow(windowDependency);
                thisWindow?.Background = (System.Windows.Media.Brush)Application.Current?.TryFindResource("ControlBackgroundBrush")!;
            }
        }
    }
}
