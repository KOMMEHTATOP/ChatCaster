namespace ChatCaster.Core.Constants;

/// <summary>
/// Основные константы приложения
/// </summary>
public static class AppConstants
{
    public const string AppName = "ChatCaster";
    public const string AppVersion = "0.0.2";
    public const string ConfigFileName = "chatcaster.json";

    // Константы для захвата комбинаций
    public const int MinHoldTimeMs = 50; // Минимальное время удержания кнопок
    public const int CapturePollingRateMs = 16; // Частота опроса при захвате (~60 FPS)
    public const int CaptureTimeoutSeconds = 2; // Таймаут захвата комбинации
    
    // Логирование
    public static class Logging
    {
        public const string LogOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
        public const string ConsoleOutputTemplate = "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
        public const string DebugOutputTemplate = "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
    }
    
    // Кроссплатформенные пути
    public static class Paths
    {
        /// <summary>
        /// Получает директорию данных приложения для текущей платформы
        /// </summary>
        public static string GetAppDataDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppName);
            }

            throw new PlatformNotSupportedException($"Platform {Environment.OSVersion.Platform} is not supported yet");
        }
        
        /// <summary>
        /// Получает директорию логов для текущей платформы
        /// </summary>
        public static string GetDefaultLogDirectory()
        {
            // На Windows логи в поддиректории конфигурации
            return Path.Combine(GetAppDataDirectory(), "logs");
        }
        
        /// <summary>
        /// Получает путь к файлу конфигурации
        /// </summary>
        public static string GetConfigFilePath()
        {
            return Path.Combine(GetAppDataDirectory(), ConfigFileName);
        }
        
        /// <summary>
        /// Создает все необходимые директории если они не существуют
        /// </summary>
        public static void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(GetAppDataDirectory());
            Directory.CreateDirectory(GetDefaultLogDirectory());
        }
    }
}