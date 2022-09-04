using Cocona;
using Serilog;

namespace NineChronicles.Headless.Executable
{
    internal static class LoggerConfigurationExtensions
    {
        internal static LoggerConfiguration ConfigureMinimumLevel(
            this LoggerConfiguration loggerConfiguration,
            string minimumLevel)
        {
            switch (minimumLevel)
            {
                case "debug":
                    return loggerConfiguration.MinimumLevel.Debug();

                case "verbose":
                    return loggerConfiguration.MinimumLevel.Verbose();

                case "info":
                    return loggerConfiguration.MinimumLevel.Information();

                case "error":
                    return loggerConfiguration.MinimumLevel.Error();

                default:
                    throw new CommandExitedException("Not supported log minimum level came.", -1);
            }
        }
    }
}
