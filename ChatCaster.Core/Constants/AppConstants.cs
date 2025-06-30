namespace ChatCaster.Core.Constants;

/// <summary>
/// Основные константы приложения
/// </summary>
public static class AppConstants
{
    public const string AppName = "ChatCaster";
    public const string AppVersion = "1.0.0";
    public const string ConfigFileName = "chatcaster.json";
    public const string LogFileName = "chatcaster.log";
    
    // Ограничения записи
    public const int MinRecordingSeconds = 1;
    public const int MaxRecordingSeconds = 30;
    public const int DefaultRecordingSeconds = 10;
    
    // Аудио параметры
    public const int MinSampleRate = 8000;
    public const int MaxSampleRate = 48000;
    public const int DefaultSampleRate = 16000;
    
    public const float MinVolumeThreshold = 0.001f;
    public const float MaxVolumeThreshold = 1.0f;
    public const float DefaultVolumeThreshold = 0.01f;
    
    // Overlay
    public const int MinOverlayOpacity = 10; // 10%
    public const int MaxOverlayOpacity = 100; // 100%
    public const int DefaultOverlayOpacity = 90; // 90%
    
    public const int MinMovementSpeed = 1;
    public const int MaxMovementSpeed = 20;
    public const int DefaultMovementSpeed = 5;
    
    // Геймпад
    public const int MinPollingRateMs = 1;
    public const int MaxPollingRateMs = 100;
    public const int DefaultPollingRateMs = 16; // ~60 FPS
    
    // Константы для захвата комбинаций
    public const int MinHoldTimeMs = 50; // Минимальное время удержания кнопок
    public const int CapturePollingRateMs = 16; // Частота опроса при захвате (~60 FPS)
    public const int ComboDetectionTimeoutMs = 200; // Время ожидания дополнительных кнопок в комбинации
    public const int CaptureTimeoutSeconds = 5; // Таймаут захвата комбинации
    
    // Распознавание речи (общие константы для любых движков)
    public const int DefaultMaxTokens = 224;
    public const int MinMaxTokens = 50;
    public const int MaxMaxTokens = 500;
    
    // Движки распознавания речи
    public static class SpeechEngines
    {
        public const string Whisper = "Whisper";
        public const string Azure = "Azure";
        public const string Google = "Google";
        // Можно добавлять новые движки
    }
    
    // Логирование
    public static class Logging
    {
        public const string LogOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
        public const string ConsoleOutputTemplate = "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
        public const string DebugOutputTemplate = "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
        
        public const int DefaultRetainedFileCount = 7;
        public const long DefaultMaxFileSizeBytes = 10_000_000; // 10MB
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
            else if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    AppName);
            }
            else if (OperatingSystem.IsLinux())
            {
                // Используем XDG Base Directory Specification
                var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                var configDir = !string.IsNullOrEmpty(xdgConfigHome) 
                    ? xdgConfigHome 
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                
                return Path.Combine(configDir, AppName.ToLowerInvariant());
            }
            else
            {
                // Fallback для других платформ
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    AppName);
            }
        }
        
        /// <summary>
        /// Получает директорию логов для текущей платформы
        /// </summary>
        public static string GetDefaultLogDirectory()
        {
            if (OperatingSystem.IsLinux())
            {
                // На Linux логи обычно в отдельной директории
                var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                var dataDir = !string.IsNullOrEmpty(xdgDataHome) 
                    ? xdgDataHome 
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
                
                return Path.Combine(dataDir, AppName.ToLowerInvariant(), "logs");
            }
            else
            {
                // На Windows и macOS логи в поддиректории конфигурации
                return Path.Combine(GetAppDataDirectory(), "logs");
            }
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