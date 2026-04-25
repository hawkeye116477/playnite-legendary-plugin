using Linguini.Bundle;
using Linguini.Bundle.Builder;
using Linguini.Shared.Types.Bundle;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Playnite;

namespace CommonPlugin
{
    public class LocalizationManager
    {
        private readonly ILogger _logger = LogManager.GetLogger();
        public static LocalizationManager Instance { get; } = new LocalizationManager();

        private FluentBundle _bundle = null!;
        private Dictionary<string, IFluentType> _commonArgs = new Dictionary<string, IFluentType>();
        private const string FallbackLanguage = "en-US";

        private LocalizationManager()
        {
            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                SetLanguage(FallbackLanguage);
            }
        }

        private FluentBundle MakeBundle(string language)
        {
            var resources = new List<string>
            {
                ReadFtl(FallbackLanguage)
            };
            var builder = LinguiniBuilder.Builder().CultureInfo(new CultureInfo(language)).AddResources(resources).UncheckedBuild();
            if (language != FallbackLanguage)
            {
                builder.AddResourceOverriding(ReadFtl(language));
            }
            return builder;
        }

        private string ReadFtl(string language)
        {
            var combinedContent = new StringBuilder();
            List<string> localizationSources;
            string? baseDir;

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
                    localizationSources = ["Localization"];
                }
                
            }
            else
            {
                baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                localizationSources = ["Localization"];
            }

            foreach (var relativePath in localizationSources)
            {
                if (baseDir == null)
                {
                    continue;
                }

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
                            _logger.Error($"Error reading file {file}: {ex.Message}");
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

        public void SetCommonArgs(Dictionary<string, IFluentType>? args)
        {
            if (args != null)
            {
                _commonArgs = args;
            }
        }

        public string GetString(string key, Dictionary<string, IFluentType>? args = null)
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
            _bundle.TryGetAttrMessage(key, finalArgs, out var errors, out var message);
            return message ?? $"[[{key}]]";
        }
    }
}
