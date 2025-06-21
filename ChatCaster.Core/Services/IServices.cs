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
/// Сервис распознавания речи через Whisper
/// </summary>
public interface ISpeechRecognitionService
{
    Task<bool> InitializeAsync(WhisperConfig config);
    Task<VoiceProcessingResult> RecognizeAsync(byte[] audioData, CancellationToken cancellationToken = default);
    Task<bool> ChangeModelAsync(WhisperModel model);
    event EventHandler<ModelDownloadProgressEvent>? DownloadProgress;
    event EventHandler<ModelDownloadCompletedEvent>? DownloadCompleted;
    Task<bool> IsModelAvailableAsync(WhisperModel model);

    bool IsInitialized { get; }
    WhisperModel CurrentModel { get; }
    Task<long> GetModelSizeAsync(WhisperModel model);
}

/// <summary>
/// Сервис для работы с геймпадами (Platform-specific)
/// </summary>
public interface IGamepadService
{
    event EventHandler<GamepadConnectedEvent>? GamepadConnected;
    event EventHandler<GamepadDisconnectedEvent>? GamepadDisconnected;
    event EventHandler<GamepadShortcutPressedEvent>? ShortcutPressed;
    
    Task StartMonitoringAsync(InputConfig config);
    Task StopMonitoringAsync();
    
    Task<IEnumerable<GamepadInfo>> GetConnectedGamepadsAsync();
    GamepadState? GetGamepadState(int index);
    GamepadInfo? GetActiveGamepadSync();

    bool IsMonitoring { get; }
    int ConnectedGamepadCount { get; }
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
/// Сервис системной интеграции (Platform-specific)
/// </summary>
public interface ISystemIntegrationService
{
    Task<bool> SendTextAsync(string text);
    Task<bool> RegisterGlobalHotkeyAsync(KeyboardShortcut shortcut);
    Task<bool> UnregisterGlobalHotkeyAsync();
    
    Task<bool> SetAutoStartAsync(bool enabled);
    Task<bool> IsAutoStartEnabledAsync();
    
    Task ShowNotificationAsync(string title, string message);
    
    event EventHandler<KeyboardShortcut>? GlobalHotkeyPressed;
}

/// <summary>
/// Сервис управления конфигурацией
/// </summary>
public interface IConfigurationService
{
    event EventHandler<ConfigurationChangedEvent>? ConfigurationChanged;
    
    Task<AppConfig> LoadConfigAsync();
    Task SaveConfigAsync(AppConfig config);
    Task<T> GetSettingAsync<T>(string key, T defaultValue);
    Task SetSettingAsync<T>(string key, T value);
    
    AppConfig CurrentConfig { get; }
    string ConfigPath { get; }
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