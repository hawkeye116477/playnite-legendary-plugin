using Linguini.Bundle;
using Linguini.Bundle.Builder;
using Linguini.Shared.Types.Bundle;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CommonPlugin
{
    public class LocalizationManager
    {
        private ILogger logger = LogManager.GetLogger();
        private static readonly LocalizationManager _instance = new LocalizationManager();
        public static LocalizationManager Instance => _instance;
        private FluentBundle _bundle;
        private Dictionary<string, IFluentType> _commonArgs = new Dictionary<string, IFluentType>();
        private string fallbackLanguage = "en-US";

        private LocalizationManager()
        {
            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                SetLanguage(fallbackLanguage);
            }
        }

        private FluentBundle MakeBundle(string language)
        {
            var resources = new List<string>
            {
                ReadFtl(fallbackLanguage)
            };
            var builder = LinguiniBuilder.Builder().CultureInfo(new CultureInfo(language)).AddResources(resources).UncheckedBuild();
            if (language != fallbackLanguage)
            {
                builder.AddResourceOverriding(ReadFtl(language));
            }
            return builder;
        }

        private string ReadFtl(string language)
        {
            var combinedContent = new StringBuilder();
            List<string> localizationSources;
            string baseDir;

            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                baseDir = Environment.CurrentDirectory;
                string configPath = Path.Combine(baseDir, "LocalizationPathsForDesignMode.txt");
                if (File.Exists(configPath))
                {
                    localizationSources = File.ReadAllLines(configPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
                }
                else
                {
                    localizationSources = new List<string> { "Localization" };
                }
                
            }
            else
            {
                baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                localizationSources = new List<string> { "Localization" };
            }

            foreach (var relativePath in localizationSources)
            {
                var locDir = Path.Combine(baseDir, relativePath.Trim(), language);
                if (Directory.Exists(locDir))
                {
                    var ftlFiles = Directory.EnumerateFiles(locDir, "*.ftl", SearchOption.AllDirectories);
                    foreach (var file in ftlFiles)
                    {
                        try
                        {
                            combinedContent.AppendLine(File.ReadAllText(file));
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Error reading file {file}: {ex.Message}");
                        }
                    }
                }
            }
            return combinedContent.ToString();
        }

        public void SetLanguage(string language)
        {
            language = language.Replace("_", "-");
            _bundle = MakeBundle(language);
        }

        public void SetCommonArgs(Dictionary<string, IFluentType> args)
        {
            if (args != null)
            {
                _commonArgs = args;
            }
        }

        public string GetString(string key, Dictionary<string, IFluentType> args = null)
        {
            var finalArgs = new Dictionary<string, IFluentType>();
            foreach (var arg in _commonArgs)
            {
                finalArgs[arg.Key] = arg.Value;
            }
            if (args != null)
            {
                foreach (var arg in args)
                {
                    finalArgs[arg.Key] = arg.Value;
                }
            }
            if (!finalArgs.ContainsKey("count"))
            {
                finalArgs["count"] = (FluentNumber)1;
            }
            var message = _bundle.GetAttrMessage(key, finalArgs);
            if (message == null)
            {
                return $"[[{key}]]";
            }
            return message;
        }
    }
}
