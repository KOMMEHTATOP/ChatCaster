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
   public LoggingConfig Logging { get; set; } = new();
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
   public int MaxRecordingSeconds { get; set; } = 10; // ✅ ИСПРАВЛЕНО: 10 вместо 30
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
   
   /// <summary>
   /// Проверяет соответствие состояния геймпада этому шорткату
   /// </summary>
   public bool IsPressed(GamepadState state)
   {
       var primaryPressed = state.IsButtonPressed(PrimaryButton);
       var secondaryPressed = state.IsButtonPressed(SecondaryButton);
       
       if (RequireBothButtons)
       {
           return primaryPressed && secondaryPressed;
       }
       
       return primaryPressed || secondaryPressed;
   }
   
   /// <summary>
   /// Текстовое представление комбинации для UI
   /// </summary>
   public string DisplayText
   {
       get => RequireBothButtons && PrimaryButton != SecondaryButton
           ? $"{PrimaryButton} + {SecondaryButton}"
           : PrimaryButton.ToString(); // Показываем только одну кнопку если они одинаковые
   }
}

/// <summary>
/// Горячая клавиша клавиатуры
/// </summary>
public class KeyboardShortcut
{
   public ModifierKeys Modifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Shift;
   public Key Key { get; set; } = Key.V;
   public bool IsGlobal { get; set; } = true;
   
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
   public OverlayMode Mode { get; set; } = OverlayMode.Normal;
   public float Opacity { get; set; } = 0.9f;
}

/// <summary>
/// Настройки Whisper распознавания
/// </summary>
public class WhisperConfig
{
   public WhisperModel Model { get; set; } = WhisperModel.Tiny; // ✅ УЖЕ ПРАВИЛЬНО: Tiny для экономии памяти
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
   public int ConfigSaveIntervalMs { get; set; } = 5000;
}

/// <summary>
/// Настройки логирования
/// </summary>
public class LoggingConfig
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public bool EnableConsoleLogging { get; set; } = false;
    public bool EnableDebugOutput { get; set; } = true;
    public int RetainedFileCount { get; set; } = 7;
    public long MaxFileSizeBytes { get; set; } = 10_000_000; // 10MB
    public string LogFileTemplate { get; set; } = "chatcaster-.log";
    public string? CustomLogDirectory { get; set; } // null = использовать стандартную папку
}