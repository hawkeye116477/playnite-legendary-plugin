using Playnite.SDK;

namespace CliWrap
{
    internal static class CliWrapCommandExtensions
    {
        internal static Command AddCommandToLog(this Command command)
        {
            var logger = LogManager.GetLogger();
            var allEnvironmentVariables = "";
            if (command.EnvironmentVariables.Count > 0)
            {
                foreach (var env in command.EnvironmentVariables)
                {
                    allEnvironmentVariables += $"{env.Key}={env.Value} ";
                }
            }
            logger.Debug($"Executing command: {allEnvironmentVariables}{command.TargetFilePath} {command.Arguments}");
            return command;
        }
    }
}
