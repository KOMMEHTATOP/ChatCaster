using Serilog.Events;

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
    public float VolumeThreshold { get; set; } = 0.01f; //отвечает за порог громкости захвата. Сделал для отсечения лишних тихих звуков
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
            ? $"{GetButtonDisplayName(PrimaryButton)} + {GetButtonDisplayName(SecondaryButton)}"
            : GetButtonDisplayName(PrimaryButton);
    }

    private string GetButtonDisplayName(GamepadButton button)
    {
        return button switch
        {
            GamepadButton.LeftBumper => "LB",
            GamepadButton.RightBumper => "RB",
            GamepadButton.LeftTrigger => "LT",
            GamepadButton.RightTrigger => "RT",
            GamepadButton.A => "A",
            GamepadButton.B => "B",
            GamepadButton.X => "X",
            GamepadButton.Y => "Y",
            GamepadButton.Start => "Start",
            GamepadButton.Back => "Back",
            GamepadButton.Guide => "Guide",
            GamepadButton.LeftStick => "LS",
            GamepadButton.RightStick => "RS",
            GamepadButton.DPadUp => "D-Up",
            GamepadButton.DPadDown => "D-Down",
            GamepadButton.DPadLeft => "D-Left",
            GamepadButton.DPadRight => "D-Right",
            _ => button.ToString()
        };
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
            var parts = new List<string>();

            if (Modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Windows))
                parts.Add("Win");

            parts.Add(GetKeyDisplayName(Key));

            return string.Join(" + ", parts);
        }
    }

    private string GetKeyDisplayName(Key key)
    {
        return key switch
        {
            Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
            Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
            Key.NumPad0 => "NumPad0", Key.NumPad1 => "NumPad1", Key.NumPad2 => "NumPad2",
            Key.NumPad3 => "NumPad3", Key.NumPad4 => "NumPad4", Key.NumPad5 => "NumPad5",
            Key.NumPad6 => "NumPad6", Key.NumPad7 => "NumPad7", Key.NumPad8 => "NumPad8",
            Key.NumPad9 => "NumPad9",
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            Key.Escape => "Esc",
            _ => key.ToString()
        };
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