using ChatCaster.Core.Models;

namespace ChatCaster.Core.Events;

/// <summary>
/// Событие изменения статуса записи
/// </summary>
public class RecordingStatusChangedEvent 
{
    public RecordingStatus OldStatus { get; set; }
    public RecordingStatus NewStatus { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Событие завершения распознавания голоса
/// </summary>
public class VoiceRecognitionCompletedEvent 
{
    public VoiceProcessingResult Result { get; set; } = new();
    public int AudioDataSize { get; set; }  
}

/// <summary>
/// Базовый класс для событий геймпада
/// </summary>
public class GamepadEvent 
{
    public GamepadEventType EventType { get; set; }
    public int GamepadIndex { get; set; }
    public GamepadInfo GamepadInfo { get; set; } = new();
}

/// <summary>
/// Событие нажатия комбинации кнопок на геймпаде
/// </summary>
public class GamepadShortcutPressedEvent : GamepadEvent
{
    public GamepadShortcut Shortcut { get; set; } = new();
    public GamepadState CurrentState { get; set; } = new();  
    public int HoldTimeMs { get; set; }                  
}

/// <summary>
/// Событие изменения конфигурации
/// </summary>
public class ConfigurationChangedEvent 
{
    public string? SettingName { get; set; }
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
}

/// <summary>
/// Событие возникновения ошибки
/// </summary>
public class ErrorOccurredEvent 
{
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public string? Component { get; set; }
}

/// <summary>
/// Событие изменения позиции overlay
/// </summary>
public class OverlayPositionChangedEvent 
{
    public int NewX { get; set; }
    public int NewY { get; set; }
    public string? Source { get; set; } // "mouse", "gamepad", "config"
}

/// <summary>
/// Событие изменения громкости микрофона
/// </summary>
public class VolumeChangedEvent 
{
    public float Volume { get; set; }
    public string? DeviceId { get; set; }
}

/// <summary>
/// Событие нажатия глобального хоткея
/// </summary>
public class GlobalHotkeyPressedEvent 
{
    public KeyboardShortcut Shortcut { get; set; } = new();
}
