using Serilog;
using Serilog.Events;
using ChatCaster.Core.Constants;
using ChatCaster.Core.Models;

namespace ChatCaster.Core.Logging;

public static class LoggingConfiguration
{
    public static ILogger CreateLogger(LoggingConfig config)
    {
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(ConvertLogLevel(config.MinimumLevel));

        // Определяем путь к логам
        string logDirectory = !string.IsNullOrEmpty(config.CustomLogDirectory) 
            ? config.CustomLogDirectory 
            : AppConstants.Paths.GetDefaultLogDirectory();

        // Создаем директорию если не существует
        Directory.CreateDirectory(logDirectory);

        // Логирование в файл (всегда включено)
        loggerConfig = loggerConfig.WriteTo.File(
            path: Path.Combine(logDirectory, config.LogFileTemplate),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: config.RetainedFileCount,
            fileSizeLimitBytes: config.MaxFileSizeBytes,
            rollOnFileSizeLimit: true,
            outputTemplate: AppConstants.Logging.LogOutputTemplate);

        // Консольный вывод (опционально)
        if (config.EnableConsoleLogging)
        {
            loggerConfig = loggerConfig.WriteTo.Console(
                outputTemplate: AppConstants.Logging.ConsoleOutputTemplate);
        }

        // Debug output (для разработки)
        if (config.EnableDebugOutput)
        {
            loggerConfig = loggerConfig.WriteTo.Debug(
                outputTemplate: AppConstants.Logging.DebugOutputTemplate);
        }

        return loggerConfig.CreateLogger();
    }

    private static LogEventLevel ConvertLogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Verbose => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Fatal => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}