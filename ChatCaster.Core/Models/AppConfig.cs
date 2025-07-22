using Serilog.Events;
using ChatCaster.Core.Utilities;

namespace ChatCaster.Core.Models;

/// <summary>
/// Главная конфигурация приложения
/// </summary>
public class AppConfig
{
    public AudioConfig Audio { get; set; } = new();
    public InputConfig Input { get; set; } = new();
    public OverlayConfig Overlay { get; set; } = new();
    public SpeechRecognitionConfig SpeechRecognition { get; set; } = new(); 
    public SystemConfig System { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

/// <summary>
/// Настройки аудио захвата
/// </summary>
public class AudioConfig
{
    public string? SelectedDeviceId { get; set; }
    public int SampleRate { get; set; } = 16000;
    public int Channels { get; set; } = 1; 
    public int BitsPerSample { get; set; } = 16;
    public int MaxRecordingSeconds { get; set; } = 30;
    public int MinRecordingSeconds { get; set; } = 2;
    public float VolumeThreshold { get; set; } = 0.005f; //отвечает за порог громкости захвата. Сделал для отсечения лишних тихих звуков
}

/// <summary>
/// Настройки управления (геймпады + клавиатура)
/// </summary>
public class InputConfig
{
    public GamepadShortcut GamepadShortcut { get; set; } = new();
    public KeyboardShortcut? KeyboardShortcut { get; set; }
    public bool EnableGamepadControl { get; set; } = true;
    public int GamepadPollingRateMs { get; set; } = 16; // ~60 FPS
}

/// <summary>
/// Комбинация кнопок геймпада (кроссплатформенная)
/// </summary>
public class GamepadShortcut
{
    public GamepadButton PrimaryButton { get; set; } = GamepadButton.LeftBumper;
    public GamepadButton SecondaryButton { get; set; } = GamepadButton.RightBumper;
    public bool RequireBothButtons { get; set; } = true;
    public int HoldTimeMs { get; set; } = 100; // Минимальное время удержания

    /// <summary>
    /// Проверяет нажата ли комбинация в указанном состоянии геймпада
    /// </summary>
    public bool IsPressed(GamepadState state)
    {
        bool primaryPressed = state.IsButtonPressed(PrimaryButton);
        
        if (RequireBothButtons && PrimaryButton != SecondaryButton)
        {
            bool secondaryPressed = state.IsButtonPressed(SecondaryButton);
            return primaryPressed && secondaryPressed;
        }

        return primaryPressed;
    }

    /// <summary>
    /// Текстовое представление комбинации для UI
    /// </summary>
    public string DisplayText
    {
        get => RequireBothButtons && PrimaryButton != SecondaryButton
            ? $"{InputDisplayHelper.GetButtonDisplayName(PrimaryButton)} + {InputDisplayHelper.GetButtonDisplayName(SecondaryButton)}"
            : InputDisplayHelper.GetButtonDisplayName(PrimaryButton);
    }
}

/// <summary>
/// Горячая клавиша клавиатуры (кроссплатформенная)
/// </summary>
public class KeyboardShortcut
{
    public ModifierKeys Modifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Shift;
    public Key Key { get; set; } = Key.V;

    /// <summary>
    /// Текстовое представление комбинации для UI
    /// </summary>
    public string DisplayText
    {
        get
        {
            var parts = InputDisplayHelper.GetModifierDisplayNames(Modifiers).ToList();
            parts.Add(InputDisplayHelper.GetKeyDisplayName(Key));
            return string.Join(" + ", parts);
        }
    }
}

/// <summary>
/// Настройки overlay индикатора
/// </summary>
public class OverlayConfig
{
    public bool IsEnabled { get; set; } = true;
    public OverlayPosition Position { get; set; } = OverlayPosition.TopRight;
    public int OffsetX { get; set; } = 50;
    public int OffsetY { get; set; } = 50;
    public float Opacity { get; set; } = 0.9f;
}

/// <summary>
/// Настройки распознавания речи (абстрактные, не привязанные к конкретному движку)
/// </summary>
public class SpeechRecognitionConfig
{
    public string Engine { get; set; } = "Whisper"; // Тип движка распознавания
    public string Language { get; set; } = "ru"; // Основной язык
    public bool UseGpuAcceleration { get; set; }
    public int MaxTokens { get; set; } = 224; // Ограничение длины для чата
    
    // Настройки специфичные для движков будут храниться в Dictionary
    public Dictionary<string, object> EngineSettings { get; set; } = new();
}

/// <summary>
/// Системные настройки
/// </summary>
public class SystemConfig
{
    public bool StartWithSystem { get; set; } = true; 
    public bool StartMinimized { get; set; }
    
    public bool AllowCompleteExit { get; set; }
    
    public bool ShowNotifications { get; set; }
    public string SelectedLanguage { get; set; } = "ru-RU"; // По умолчанию русский
}

/// <summary>
/// Настройки логирования
/// </summary>
public class LoggingConfig
{
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;
    public bool EnableConsoleLogging { get; set; } 
    public bool EnableDebugOutput { get; set; } = true;
    public int RetainedFileCount { get; set; } = 7;
    public long MaxFileSizeBytes { get; set; } = 10_000_000; // 10MB
    public string LogFileTemplate { get; set; } = "chatcaster-.log";
    public string? CustomLogDirectory { get; set; }
}