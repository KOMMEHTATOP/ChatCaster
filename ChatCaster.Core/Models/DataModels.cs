namespace ChatCaster.Core.Models;

/// <summary>
/// Состояние записи голоса
/// </summary>
public class RecordingState
{
    public RecordingStatus Status { get; set; } = RecordingStatus.Idle;
    public DateTime? StartTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public float CurrentVolume { get; set; }
    public string? LastRecognizedText { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsOverlayVisible { get; set; }
}

/// <summary>
/// Информация об аудио устройстве
/// </summary>
public class AudioDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsEnabled { get; set; } = true;
    public AudioDeviceType Type { get; set; }
    public int MaxChannels { get; set; }
    public int[] SupportedSampleRates { get; set; } = Array.Empty<int>();
}

/// <summary>
/// Информация о геймпаде
/// </summary>
public class GamepadInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public GamepadType Type { get; set; } = GamepadType.XboxController;
    public bool IsConnected { get; set; }
    public DateTime LastUpdateTime { get; set; } = DateTime.Now;
}

/// <summary>
/// Состояние геймпада (кнопки и стики)
/// </summary>
public class GamepadState
{
    private readonly Dictionary<GamepadButton, bool> _buttonStates = new();
    private readonly Dictionary<GamepadButton, DateTime> _buttonPressTime = new();
    
    public DateTime LastUpdateTime { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Проверяет нажата ли кнопка
    /// </summary>
    public bool IsButtonPressed(GamepadButton button)
    {
        return _buttonStates.GetValueOrDefault(button, false);
    }
    
    /// <summary>
    /// Устанавливает состояние кнопки
    /// </summary>
    public void SetButtonState(GamepadButton button, bool pressed)
    {
        var wasPressed = _buttonStates.GetValueOrDefault(button, false);
        _buttonStates[button] = pressed;
        
        if (pressed && !wasPressed)
        {
            _buttonPressTime[button] = DateTime.Now;
        }
        else if (!pressed && wasPressed)
        {
            _buttonPressTime.Remove(button);
        }
        
        LastUpdateTime = DateTime.Now;
    }
    
    /// <summary>
    /// Получает время удержания кнопки в миллисекундах
    /// </summary>
    public int GetButtonHoldTime(GamepadButton button)
    {
        if (!IsButtonPressed(button) || !_buttonPressTime.TryGetValue(button, out var value))
            return 0;
            
        return (int)(DateTime.Now - value).TotalMilliseconds;
    }
    
    /// <summary>
    /// Получает все нажатые кнопки
    /// </summary>
    public IEnumerable<GamepadButton> GetPressedButtons()
    {
        return _buttonStates.Where(kvp => kvp.Value).Select(kvp => kvp.Key);
    }
}

/// <summary>
/// Результат обработки голоса
/// </summary>
public class VoiceProcessingResult
{
    public bool Success { get; set; }
    public string? RecognizedText { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public float Confidence { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? AudioData { get; set; } // Для отладки
}

/// <summary>
/// Результат валидации конфигурации
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public void AddError(string error)
    {
        IsValid = false;
        Errors.Add(error);
    }

    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }
}