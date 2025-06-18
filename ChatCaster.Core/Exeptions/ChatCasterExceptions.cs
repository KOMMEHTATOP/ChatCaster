namespace ChatCaster.Core.Exceptions;

/// <summary>
/// Базовое исключение ChatCaster
/// </summary>
public abstract class ChatCasterException : Exception
{
    public string Component { get; }
    public DateTime Timestamp { get; }

    protected ChatCasterException(string component, string message) : base(message)
    {
        Component = component;
        Timestamp = DateTime.Now;
    }

    protected ChatCasterException(string component, string message, Exception innerException) 
        : base(message, innerException)
    {
        Component = component;
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// Ошибки аудио подсистемы
/// </summary>
public class AudioException : ChatCasterException
{
    public AudioException(string message) : base("Audio", message) { }
    public AudioException(string message, Exception innerException) : base("Audio", message, innerException) { }
}

/// <summary>
/// Ошибки геймпад подсистемы
/// </summary>
public class GamepadException : ChatCasterException
{
    public GamepadException(string message) : base("Gamepad", message) { }
    public GamepadException(string message, Exception innerException) : base("Gamepad", message, innerException) { }
}

/// <summary>
/// Ошибки распознавания речи
/// </summary>
public class SpeechRecognitionException : ChatCasterException
{
    public SpeechRecognitionException(string message) : base("SpeechRecognition", message) { }
    public SpeechRecognitionException(string message, Exception innerException) : base("SpeechRecognition", message, innerException) { }
}

/// <summary>
/// Ошибки overlay
/// </summary>
public class OverlayException : ChatCasterException
{
    public OverlayException(string message) : base("Overlay", message) { }
    public OverlayException(string message, Exception innerException) : base("Overlay", message, innerException) { }
}

/// <summary>
/// Ошибки конфигурации
/// </summary>
public class ConfigurationException : ChatCasterException
{
    public ConfigurationException(string message) : base("Configuration", message) { }
    public ConfigurationException(string message, Exception innerException) : base("Configuration", message, innerException) { }
}

/// <summary>
/// Ошибки инициализации
/// </summary>
public class InitializationException : ChatCasterException
{
    public InitializationException(string message) : base("Initialization", message) { }
    public InitializationException(string message, Exception innerException) : base("Initialization", message, innerException) { }
}