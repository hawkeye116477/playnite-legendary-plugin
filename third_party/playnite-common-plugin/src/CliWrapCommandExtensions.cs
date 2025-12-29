using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CliWrap
{
    internal static class CliWrapCommandExtensions
    {
        internal static Command AddCommandToLog(this Command command)
        {
            var logger = LogManager.GetLogger();
            var allEnvironmentVariables = "";
            var sensitiveValues = new HashSet<string> { "secret", "password", "token", "user" };

            if (command.EnvironmentVariables.Count > 0)
            {
                foreach (var env in command.EnvironmentVariables)
                {
                    if (sensitiveValues.Any(s => env.Key.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        allEnvironmentVariables += $"{env.Key}=*** ";
                    }
                    else
                    {
                        allEnvironmentVariables += $"{env.Key}={env.Value} ";
                    }
                }
            }

            var tokens = (command.Arguments ?? "").Split(' ').ToList();
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                string current = tokens[i];
                string next = tokens[i + 1];

                if ((current.StartsWith("--") || current.StartsWith("-"))
                    && sensitiveValues.Any(s => current.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    tokens[i + 1] = "***";
                }
            }
            var safeArguments = string.Join(" ", tokens);

            logger.Debug($"Executing command: {allEnvironmentVariables}{command.TargetFilePath} {safeArguments}");
            return command;
        }
    }
}
