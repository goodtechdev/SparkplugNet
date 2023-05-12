using System;
using System.IO;
using Serilog;

namespace Goodtech.Log
{
    public static class Logger
    {
        public static readonly ILogger _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(GetLogFilePath(), rollingInterval: RollingInterval.Day, outputTemplate: "yyyy-MM-dd HH:mm:ss.fff")
            .CreateLogger();

        public static void Information(string messageTemplate, params object[] args)
        {
            _logger.Information(messageTemplate, args);
        }

        public static void Error(string messageTemplate, params object[] args)
        {
            _logger.Error(messageTemplate, args);
        }

        public static void Warning(string messageTemplate, params object[] args)
        {
            _logger.Warning(messageTemplate, args);
        }

        public static void Debug(string messageTemplate, params object[] args)
        {
            _logger.Debug(messageTemplate, args);
        }

        private static string GetLogFilePath()
        {
            var logsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MqttConnector",
                "Logs");

            Directory.CreateDirectory(logsDirectory);

            var logFileName = $"{DateTime.Today:yyyy-MM-dd}.txt";
            return Path.Combine(logsDirectory, logFileName);
        }
    }
}