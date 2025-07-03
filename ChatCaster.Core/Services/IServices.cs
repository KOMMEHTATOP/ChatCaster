using ChatCaster.Core.Events;
using ChatCaster.Core.Models;

namespace ChatCaster.Core.Services;

/// <summary>
/// Главный сервис управления записью голоса
/// </summary>
public interface IVoiceRecordingService
{
    event EventHandler<RecordingStatusChangedEvent>? StatusChanged;
    event EventHandler<VoiceRecognitionCompletedEvent>? RecognitionCompleted;

    RecordingState CurrentState { get; }
    bool IsRecording { get; }

    Task<bool> StartRecordingAsync(CancellationToken cancellationToken = default);
    Task<VoiceProcessingResult> StopRecordingAsync(CancellationToken cancellationToken = default);
    Task CancelRecordingAsync();

    Task<bool> TestMicrophoneAsync();
    Task<VoiceProcessingResult> ProcessAudioDataAsync(byte[] audioData, CancellationToken cancellationToken = default);
}

/// <summary>
/// Сервис для работы с аудио устройствами (Platform-specific)
/// </summary>
public interface IAudioCaptureService
{
    event EventHandler<float>? VolumeChanged;
    event EventHandler<byte[]>? AudioDataReceived;
    Task<bool> TestMicrophoneAsync();

    Task<IEnumerable<AudioDevice>> GetAvailableDevicesAsync();
    Task<AudioDevice?> GetDefaultDeviceAsync();
    Task<bool> SetActiveDeviceAsync(string deviceId);

    Task<bool> StartCaptureAsync(AudioConfig config);
    Task StopCaptureAsync();

    bool IsCapturing { get; }
    float CurrentVolume { get; }
    AudioDevice? ActiveDevice { get; }
}

/// <summary>
/// Абстрактный сервис распознавания речи (не привязан к конкретному движку)
/// </summary>
public interface ISpeechRecognitionService
{
    Task<bool> InitializeAsync(SpeechRecognitionConfig config);
    Task<VoiceProcessingResult> RecognizeAsync(byte[] audioData, CancellationToken cancellationToken = default);
    Task<bool> ReloadConfigAsync(SpeechRecognitionConfig config);

    event EventHandler<SpeechRecognitionProgressEvent>? RecognitionProgress;
    event EventHandler<SpeechRecognitionErrorEvent>? RecognitionError;

    bool IsInitialized { get; }
    string EngineName { get; }
    string EngineVersion { get; }

    // Получение информации о возможностях движка
    Task<SpeechEngineCapabilities> GetCapabilitiesAsync();
    Task<IEnumerable<string>> GetSupportedLanguagesAsync();
}

/// <summary>
/// Информация о возможностях речевого движка
/// </summary>
public class SpeechEngineCapabilities
{
    public bool SupportsLanguageAutoDetection { get; set; }
    public bool SupportsGpuAcceleration { get; set; }
    public bool SupportsRealTimeProcessing { get; set; }
    public bool RequiresInternetConnection { get; set; }
    public int[] SupportedSampleRates { get; set; } = [];
    public int MinAudioDurationMs { get; set; }
    public int MaxAudioDurationMs { get; set; }
}

/// <summary>
/// Упрощенный сервис для работы с геймпадами (только XInput)
/// </summary>
public interface IGamepadService
{
    // События
    event EventHandler<GamepadConnectedEvent>? GamepadConnected;
    event EventHandler<GamepadDisconnectedEvent>? GamepadDisconnected;
    event EventHandler<GamepadShortcutPressedEvent>? ShortcutPressed;

    // Управление мониторингом
    Task StartMonitoringAsync(GamepadShortcut shortcut);
    Task StopMonitoringAsync();

    // Получение информации
    Task<GamepadInfo?> GetConnectedGamepadAsync();
    GamepadState? GetCurrentState();

    // Статус
    bool IsMonitoring { get; }
    bool IsGamepadConnected { get; }

    // Тестирование (для UI настроек)
    Task<bool> TestConnectionAsync();
}

/// <summary>
/// Сервис overlay индикатора (Platform-specific)
/// </summary>
public interface IOverlayService
{
    event EventHandler<OverlayPositionChangedEvent>? PositionChanged;

    Task ShowAsync(RecordingStatus status);
    Task HideAsync();
    Task UpdateStatusAsync(RecordingStatus status, string? message = null);
    Task UpdatePositionAsync(int x, int y);

    Task<bool> ApplyConfigAsync(OverlayConfig config);

    bool IsVisible { get; }
    (int X, int Y) CurrentPosition { get; }
}

/// <summary>
/// Композитный сервис системной интеграции - объединяет все платформо-специфичные сервисы
/// </summary>
public interface ISystemIntegrationService
{
    // Текстовый ввод
    Task<bool> SendTextAsync(string text);
    void SetTypingDelay(int delayMs);
    Task<bool> ClearActiveFieldAsync();
    Task<bool> SelectAllTextAsync();

    // Горячие клавиши
    Task<bool> RegisterGlobalHotkeyAsync(KeyboardShortcut shortcut);
    Task<bool> UnregisterGlobalHotkeyAsync();

    event EventHandler<KeyboardShortcut>? GlobalHotkeyPressed;
    
    // Системные функции
    Task<bool> SetAutoStartAsync(bool enabled);
    Task<bool> IsAutoStartEnabledAsync();
    Task ShowNotificationAsync(string title, string message);
    
    // Информация о состоянии
    bool IsTextInputAvailable { get; }
    string ActiveWindowTitle { get; }
}

/// <summary>
/// Сервис для работы с окнами системы
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Получает заголовок активного окна
    /// </summary>
    string GetActiveWindowTitle();
    
    /// <summary>
    /// Проверяет, является ли окно собственным окном приложения
    /// </summary>
    bool IsOwnWindow(string windowTitle);
    
    /// <summary>
    /// Проверяет, является ли окно Steam-приложением
    /// </summary>
    bool IsSteamWindow(string windowTitle);
    
    /// <summary>
    /// Получает handle активного окна
    /// </summary>
    IntPtr GetActiveWindowHandle();
}

/// <summary>
/// Сервис для ввода текста в активное окно
/// </summary>
public interface ITextInputService
{
    /// <summary>
    /// Отправляет текст в активное окно
    /// </summary>
    Task<bool> SendTextAsync(string text);
    Task<bool> ClearActiveFieldAsync();
    Task<bool> SelectAllTextAsync();
    /// <summary>
    /// Устанавливает задержку между вводом символов
    /// </summary>
    void SetTypingDelay(int delayMs);
    
    /// <summary>
    /// Проверяет возможность ввода в текущее активное окно
    /// </summary>
    bool CanSendToActiveWindow();
}

/// <summary>
/// Сервис для работы с глобальными горячими клавишами
/// </summary>
public interface IGlobalHotkeyService
{
    event EventHandler<KeyboardShortcut>? GlobalHotkeyPressed;
    
    Task<bool> RegisterAsync(KeyboardShortcut shortcut);
    Task<bool> UnregisterAsync();
    
    bool IsRegistered { get; }
    KeyboardShortcut? CurrentShortcut { get; }
}

/// <summary>
/// Сервис для системных уведомлений и автозапуска
/// </summary>
public interface ISystemNotificationService
{
    Task ShowNotificationAsync(string title, string message);
    Task<bool> SetAutoStartAsync(bool enabled);
    Task<bool> IsAutoStartEnabledAsync();
}



/// <summary>
/// Интерфейс для работы с системным треем
/// </summary>
public interface ITrayService
{
    /// <summary>
    /// Инициализирует трей-сервис
    /// </summary>
    void Initialize();

    /// <summary>
    /// Показывает уведомление в трее
    /// </summary>
    /// <param name="title">Заголовок уведомления</param>
    /// <param name="message">Текст уведомления</param>
    /// <param name="type">Тип уведомления</param>
    /// <param name="timeout">Время показа в миллисекундах</param>
    void ShowNotification(string title, string message, NotificationType type = NotificationType.Info, int timeout = 3000);

    /// <summary>
    /// Обновляет статус в tooltip трея
    /// </summary>
    /// <param name="status">Новый статус</param>
    void UpdateStatus(string status);

    /// <summary>
    /// Показывает уведомление при первом сворачивании в трей
    /// </summary>
    void ShowFirstTimeNotification();

    /// <summary>
    /// Видимость иконки в трее
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Освобождает ресурсы
    /// </summary>
    void Dispose();

    #region События для слабой связанности

    /// <summary>
    /// Событие запроса показа главного окна (двойной клик или пункт меню)
    /// </summary>
    event EventHandler? ShowMainWindowRequested;

    /// <summary>
    /// Событие запроса открытия настроек (пункт меню)
    /// </summary>
    event EventHandler? ShowSettingsRequested;

    /// <summary>
    /// Событие запроса выхода из приложения (пункт меню)
    /// </summary>
    event EventHandler? ExitApplicationRequested;

    #endregion

}

/// <summary>
/// Типы уведомлений в трее
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Информационное сообщение
    /// </summary>
    Info,

    /// <summary>
    /// Успешное выполнение операции
    /// </summary>
    Success,

    /// <summary>
    /// Предупреждение
    /// </summary>
    Warning,

    /// <summary>
    /// Ошибка
    /// </summary>
    Error
}

/// <summary>
/// Сервис управления конфигурацией
/// </summary>
public interface IConfigurationService
{
    event EventHandler<ConfigurationChangedEvent>? ConfigurationChanged;

    Task<AppConfig> LoadConfigAsync();
    Task SaveConfigAsync(AppConfig config);

    AppConfig CurrentConfig { get; }
    string ConfigPath { get; }
}

/// <summary>
/// Сервис управления уведомлениями приложения
/// Координирует системные события и пользовательские уведомления
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Инициализирует сервис уведомлений и подписывается на системные события
    /// </summary>
    Task InitializeAsync();

    #region Системные уведомления

    /// <summary>
    /// Уведомление о подключении геймпада
    /// </summary>
    /// <param name="gamepad">Информация о подключенном геймпаде</param>
    void NotifyGamepadConnected(GamepadInfo gamepad);

    /// <summary>
    /// Уведомление об отключении геймпада
    /// </summary>
    /// <param name="gamepad">Информация об отключенном геймпаде</param>
    void NotifyGamepadDisconnected(GamepadInfo gamepad);

    /// <summary>
    /// Уведомление об изменении микрофона
    /// </summary>
    /// <param name="deviceName">Название нового устройства</param>
    void NotifyMicrophoneChanged(string deviceName);

    /// <summary>
    /// Уведомление о результате теста микрофона
    /// </summary>
    /// <param name="success">Успешность теста</param>
    /// <param name="deviceName">Название тестируемого устройства (опционально)</param>
    void NotifyMicrophoneTest(bool success, string? deviceName = null);

    /// <summary>
    /// Уведомление об изменении настроек управления
    /// </summary>
    /// <param name="shortcutType">Тип комбинации (геймпад/клавиатура)</param>
    /// <param name="displayText">Текстовое представление комбинации</param>
    void NotifyControlSettingsChanged(string shortcutType, string displayText);

    #endregion

    #region Пользовательские уведомления

    /// <summary>
    /// Показать уведомление об успешном выполнении операции
    /// </summary>
    /// <param name="title">Заголовок уведомления</param>
    /// <param name="message">Текст уведомления</param>
    void NotifySuccess(string title, string message);

    /// <summary>
    /// Показать предупреждающее уведомление
    /// </summary>
    /// <param name="title">Заголовок уведомления</param>
    /// <param name="message">Текст уведомления</param>
    void NotifyWarning(string title, string message);

    /// <summary>
    /// Показать уведомление об ошибке
    /// </summary>
    /// <param name="title">Заголовок уведомления</param>
    /// <param name="message">Текст уведомления</param>
    void NotifyError(string title, string message);

    /// <summary>
    /// Показать информационное уведомление
    /// </summary>
    /// <param name="title">Заголовок уведомления</param>
    /// <param name="message">Текст уведомления</param>
    void NotifyInfo(string title, string message);

    #endregion

    #region Управление статусом

    /// <summary>
    /// Обновить статус в системном трее
    /// </summary>
    /// <param name="status">Новый статус</param>
    void UpdateStatus(string status);

    #endregion

    /// <summary>
    /// Освобождает ресурсы сервиса
    /// </summary>
    void Dispose();
}

/// <summary>
/// Сервис логирования
/// </summary>
public interface ILoggingService
{
    void LogTrace(string message, params object[] args);
    void LogDebug(string message, params object[] args);
    void LogInfo(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(string message, params object[] args);
    void LogError(Exception exception, string message, params object[] args);
    void LogFatal(string message, params object[] args);
    void LogFatal(Exception exception, string message, params object[] args);

    Task<string[]> GetRecentLogsAsync(int maxLines = 1000);
    Task ClearLogsAsync();
}

/// <summary>
/// Менеджер событий для межкомпонентного взаимодействия
/// </summary>
public interface IEventBusService
{
    void Subscribe<T>(Action<T> handler) where T : ChatCasterEvent;
    void Unsubscribe<T>(Action<T> handler) where T : ChatCasterEvent;
    Task PublishAsync<T>(T eventData) where T : ChatCasterEvent;

    void SubscribeWeak<T>(WeakEventHandler<T> handler) where T : ChatCasterEvent;
}

/// <summary>
/// Главный сервис приложения - координатор всех компонентов
/// </summary>
public interface IChatCasterService
{
    event EventHandler<RecordingStatusChangedEvent>? StatusChanged;
    event EventHandler<ErrorOccurredEvent>? ErrorOccurred;

    Task<bool> InitializeAsync();
    Task ShutdownAsync();

    Task<bool> StartVoiceInputAsync();
    Task StopVoiceInputAsync();

    Task<AppConfig> GetConfigurationAsync();
    Task UpdateConfigurationAsync(AppConfig config);

    Task<IEnumerable<AudioDevice>> GetAudioDevicesAsync();
    Task<IEnumerable<GamepadInfo>> GetGamepadsAsync();

    RecordingState CurrentState { get; }
    bool IsInitialized { get; }
    string Version { get; }
}
