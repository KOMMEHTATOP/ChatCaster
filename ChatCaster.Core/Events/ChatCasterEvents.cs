using ChatCaster.Core.Models;

namespace ChatCaster.Core.Events;

/// <summary>
/// Базовый класс для всех событий ChatCaster
/// </summary>
public abstract class ChatCasterEvent
{
    public DateTime Timestamp { get; } = DateTime.Now;
    public string EventId { get; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Событие изменения статуса записи
/// </summary>
public class RecordingStatusChangedEvent : ChatCasterEvent
{
    public RecordingStatus OldStatus { get; set; }
    public RecordingStatus NewStatus { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Событие завершения распознавания голоса
/// </summary>
public class VoiceRecognitionCompletedEvent : ChatCasterEvent
{
    public VoiceProcessingResult Result { get; set; } = new();
    public int AudioDataSize { get; set; }      // ДОБАВИТЬ
    public DateTime Timestamp { get; set; }     // ДОБАВИТЬ

}

/// <summary>
/// Базовый класс для событий геймпада
/// </summary>
public class GamepadEvent : ChatCasterEvent
{
    public int GamepadIndex { get; set; }
    public GamepadInfo GamepadInfo { get; set; } = new();
}

/// <summary>
/// Событие подключения геймпада
/// </summary>
public class GamepadConnectedEvent : GamepadEvent
{
}

/// <summary>
/// Событие отключения геймпада
/// </summary>
public class GamepadDisconnectedEvent : GamepadEvent
{
}

/// <summary>
/// Событие нажатия комбинации кнопок на геймпаде
/// </summary>
public class GamepadShortcutPressedEvent : GamepadEvent
{
    public GamepadShortcut Shortcut { get; set; } = new();
}

/// <summary>
/// Событие изменения конфигурации
/// </summary>
public class ConfigurationChangedEvent : ChatCasterEvent
{
    public string? SettingName { get; set; }
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
}

/// <summary>
/// Событие возникновения ошибки
/// </summary>
public class ErrorOccurredEvent : ChatCasterEvent
{
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public string? Component { get; set; }
}

/// <summary>
/// Событие изменения позиции overlay
/// </summary>
public class OverlayPositionChangedEvent : ChatCasterEvent
{
    public int NewX { get; set; }
    public int NewY { get; set; }
    public string? Source { get; set; } // "mouse", "gamepad", "config"
}

/// <summary>
/// Событие изменения громкости микрофона
/// </summary>
public class VolumeChangedEvent : ChatCasterEvent
{
    public float Volume { get; set; }
    public string? DeviceId { get; set; }
}

/// <summary>
/// Событие нажатия глобального хоткея
/// </summary>
public class GlobalHotkeyPressedEvent : ChatCasterEvent
{
    public KeyboardShortcut Shortcut { get; set; } = new();
}

public class ModelDownloadProgressEvent : ChatCasterEvent
{
    public WhisperModel Model { get; init; }
    public long BytesReceived { get; init; }
    public long TotalBytes { get; init; }
    public int ProgressPercentage { get; init; }
    public string Status { get; init; } = "";
}

public class ModelDownloadCompletedEvent : ChatCasterEvent
{
    public WhisperModel Model { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}