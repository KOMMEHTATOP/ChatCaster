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
    
    // Пути
    public const string DefaultConfigPath = "%APPDATA%\\ChatCaster";
    public const string DefaultLogPath = "%APPDATA%\\ChatCaster\\Logs";
    
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
    
    // НОВЫЕ КОНСТАНТЫ ДЛЯ ЗАХВАТА КОМБИНАЦИЙ
    public const int MinHoldTimeMs = 50; // Минимальное время удержания кнопок
    public const int CapturePollingRateMs = 16; // Частота опроса при захвате (~60 FPS)
    public const int ComboDetectionTimeoutMs = 200; // Время ожидания дополнительных кнопок в комбинации
    public const int CaptureTimeoutSeconds = 5; // Таймаут захвата комбинации
    
    // Whisper
    public const int DefaultMaxTokens = 224;
    public const int MinMaxTokens = 50;
    public const int MaxMaxTokens = 500;
}