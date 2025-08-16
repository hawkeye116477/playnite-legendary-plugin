using System;
using System.Collections.Generic;
using System.Windows.Markup;
using Linguini.Shared.Types.Bundle;

namespace CommonPlugin
{
    public class LocalizeExtension : MarkupExtension
    {
        public string Key { get; set; }
        public string Args { get; set; }

        public LocalizeExtension() { }

        public LocalizeExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
            {
                return "[[Invalid Localization Key]]";
            }

            var finalArgs = new Dictionary<string, IFluentType>();
            if (!string.IsNullOrEmpty(Args))
            {
                var trimmedArgs = Args.Trim();
                var pairs = trimmedArgs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var pair in pairs)
                {
                    var parts = pair.Split(new[] { '=' }, 2, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        {
                            finalArgs[key] = (FluentString)value;
                        }
                    }
                }
            }

            return LocalizationManager.Instance.GetString(Key, finalArgs);
        }
    }
}