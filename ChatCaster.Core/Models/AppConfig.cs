namespace ChatCaster.Core.Models;

/// <summary>
/// Главная конфигурация приложения
/// </summary>
public class AppConfig
{
    public AudioConfig Audio { get; set; } = new();
    public InputConfig Input { get; set; } = new();
    public OverlayConfig Overlay { get; set; } = new();
    public WhisperConfig Whisper { get; set; } = new();
    public SystemConfig System { get; set; } = new();
}

/// <summary>
/// Настройки аудио захвата
/// </summary>
public class AudioConfig
{
    public string? SelectedDeviceId { get; set; }
    public int SampleRate { get; set; } = 16000; // Оптимально для Whisper
    public int Channels { get; set; } = 1; // Моно
    public int BitsPerSample { get; set; } = 16;
    public int RecordingTimeoutSeconds { get; set; } = 10;
    public int MaxRecordingSeconds { get; set; } = 30;
    public int MinRecordingSeconds { get; set; } = 1;
    public float VolumeThreshold { get; set; } = 0.01f; // Для автостопа по тишине
}

/// <summary>
/// Настройки управления (геймпады + клавиатура)
/// </summary>
public class InputConfig
{
    public GamepadShortcut GamepadShortcut { get; set; } = new();
    public KeyboardShortcut? KeyboardShortcut { get; set; }
    public bool EnableGamepadControl { get; set; } = true;
    public bool EnableKeyboardControl { get; set; } = true;
    public int GamepadPollingRateMs { get; set; } = 16; // ~60 FPS
}

/// <summary>
/// Комбинация кнопок геймпада
/// </summary>
public class GamepadShortcut
{
    public GamepadButton PrimaryButton { get; set; } = GamepadButton.LeftBumper;
    public GamepadButton SecondaryButton { get; set; } = GamepadButton.RightBumper;
    public bool RequireBothButtons { get; set; } = true;
    public int HoldTimeMs { get; set; } = 100; // Минимальное время удержания
}

/// <summary>
/// Горячая клавиша клавиатуры
/// </summary>
public class KeyboardShortcut
{
    public ModifierKeys Modifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Shift;
    public Key Key { get; set; } = Key.V;
    public bool IsGlobal { get; set; } = true;
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
    public OverlayMode Mode { get; set; } = OverlayMode.Normal;
    public float Opacity { get; set; } = 0.9f;
}

/// <summary>
/// Настройки Whisper распознавания
/// </summary>
public class WhisperConfig
{
    public WhisperModel Model { get; set; } = WhisperModel.Tiny;
    public string Language { get; set; } = "ru"; // Основной язык
    public bool AutoDetectLanguage { get; set; } = false;
    public string UnrecognizedPlaceholder { get; set; } = "ХХХХХХ";
    public bool UseGpu { get; set; } = false; // Попробовать GPU ускорение
    public int MaxTokens { get; set; } = 224; // Ограничение длины для чата
}

/// <summary>
/// Системные настройки
/// </summary>
public class SystemConfig
{
    public bool StartWithWindows { get; set; } = true;
    public bool StartMinimized { get; set; } = false; 
    public bool AllowCompleteExit { get; set; } = false; 
    public bool ShowNotifications { get; set; } = true;
    public LogLevel LogLevel { get; set; } = LogLevel.Info;
    public int ConfigSaveIntervalMs { get; set; } = 5000;
}
