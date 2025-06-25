namespace ChatCaster.Core.Models;

/// <summary>
/// Кнопки геймпада (расширенная версия)
/// </summary>
/// <summary>
/// Кнопки геймпада (упрощенная версия для XInput)
/// </summary>
public enum GamepadButton
{
    // Основные кнопки
    A,
    B, 
    X,
    Y,
    
    // Бамперы и триггеры
    LeftBumper,
    RightBumper,
    LeftTrigger,
    RightTrigger,
    
    // Системные кнопки
    Back,
    Start,
    Guide,           // Xbox кнопка
    
    // Стики (нажатие)
    LeftStick,
    RightStick,
    
    // D-Pad
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight
}

/// <summary>
/// Типы геймпадов (упрощенно)
/// </summary>
public enum GamepadType
{
    Unknown,
    XboxController,  // Любой XInput совместимый
    Generic         // Для будущего расширения
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
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9, 
    NumPadAdd, NumPadSubtract, NumPadMultiply, NumPadDivide, NumPadDecimal, NumPadEnter,
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

/// <summary>
/// Уровни логирования
/// </summary>
public enum LogLevel
{
    Verbose = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Fatal = 5
}
