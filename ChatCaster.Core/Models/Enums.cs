namespace ChatCaster.Core.Models;

/// <summary>
/// Кнопки геймпада
/// </summary>
public enum GamepadButton
{
    A, B, X, Y,
    LeftBumper, RightBumper,
    LeftTrigger, RightTrigger,
    Back, Start, Guide,
    LeftStick, RightStick,
    DPadUp, DPadDown, DPadLeft, DPadRight
}

/// <summary>
/// Оси геймпада (стики и триггеры)
/// </summary>
public enum GamepadAxis
{
    LeftStickX, LeftStickY,
    RightStickX, RightStickY,
    LeftTriggerAxis, RightTriggerAxis
}

/// <summary>
/// Типы геймпадов
/// </summary>
public enum GamepadType
{
    Unknown,
    Xbox360,
    XboxOne,
    XboxSeries,
    PlayStation4,
    PlayStation5,
    SteamController,
    Generic
}

/// <summary>
/// Модификаторы клавиатуры
/// </summary>
[Flags]
public enum ModifierKeys
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

/// <summary>
/// Клавиши для хоткеев
/// </summary>
public enum Key
{
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    Space, Enter, Tab, Escape,
    Insert, Delete, Home, End, PageUp, PageDown,
    Up, Down, Left, Right
}

/// <summary>
/// Позиции overlay на экране
/// </summary>
public enum OverlayPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
    Custom
}

/// <summary>
/// Режимы отображения overlay
/// </summary>
public enum OverlayMode
{
    Normal,        // Обычный режим
    Transparent,   // Полупрозрачный
    AudioOnly,     // Только звуковые уведомления
    AutoCorner     // Автоматически в углу
}

/// <summary>
/// Модели Whisper для распознавания речи
/// </summary>
public enum WhisperModel
{
    Tiny,   // ~39 MB, быстро
    Base,   // ~74 MB, лучше качество
    Small,  // ~244 MB, еще лучше
    Medium, // ~769 MB, отличное качество
    Large   // ~1550 MB, максимальное качество
}

/// <summary>
/// Уровни логирования
/// </summary>
public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// Типы аудио устройств
/// </summary>
public enum AudioDeviceType
{
    Unknown,
    Microphone,
    LineIn,
    UsbMicrophone,
    BluetoothMicrophone,
    WebcamMicrophone,
    HeadsetMicrophone
}

/// <summary>
/// Статус записи голоса
/// </summary>
public enum RecordingStatus
{
    Idle,       // Ожидание
    Recording,  // Идет запись
    Processing, // Обработка распознавания
    Completed,  // Завершено успешно
    Error,      // Ошибка
    Cancelled   // Отменено пользователем
}