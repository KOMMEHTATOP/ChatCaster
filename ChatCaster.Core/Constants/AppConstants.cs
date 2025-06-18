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
    
    // Whisper
    public const int DefaultMaxTokens = 224;
    public const int MinMaxTokens = 50;
    public const int MaxMaxTokens = 500;
}

/// <summary>
/// Сообщения для UI
/// </summary>
public static class Messages
{
    // Статусы записи
    public const string StatusIdle = "Готов к записи";
    public const string StatusRecording = "Запись голоса...";
    public const string StatusProcessing = "Обработка...";
    public const string StatusCompleted = "Текст распознан";
    public const string StatusError = "Ошибка";
    public const string StatusCancelled = "Отменено";
    
    // Ошибки
    public const string ErrorNoMicrophone = "Микрофон не найден";
    public const string ErrorNoGamepad = "Геймпад не подключен";
    public const string ErrorWhisperInit = "Не удалось инициализировать Whisper";
    public const string ErrorRecordingFailed = "Ошибка записи";
    public const string ErrorRecognitionFailed = "Ошибка распознавания";
    public const string ErrorConfigLoad = "Ошибка загрузки настроек";
    public const string ErrorConfigSave = "Ошибка сохранения настроек";
    
    // Уведомления
    public const string NotificationRecordingStarted = "Запись начата";
    public const string NotificationTextRecognized = "Текст распознан и вставлен";
    public const string NotificationGamepadConnected = "Геймпад подключен";
    public const string NotificationGamepadDisconnected = "Геймпад отключен";
}