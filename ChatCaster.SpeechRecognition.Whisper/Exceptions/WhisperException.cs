using ChatCaster.SpeechRecognition.Whisper.Constants;

namespace ChatCaster.SpeechRecognition.Whisper.Exceptions;

/// <summary>
/// Базовое исключение для всех ошибок Whisper движка
/// </summary>
public class WhisperException : Exception
{
    public string? ErrorCode { get; }
    public Dictionary<string, object> Context { get; }

    public WhisperException(string message) : base(message)
    {
        Context = new Dictionary<string, object>();
    }

    public WhisperException(string message, Exception innerException) : base(message, innerException)
    {
        Context = new Dictionary<string, object>();
    }

    public WhisperException(string message, string? errorCode) : base(message)
    {
        ErrorCode = errorCode;
        Context = new Dictionary<string, object>();
    }

    public WhisperException(string message, string? errorCode, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    /// Добавляет контекстную информацию к исключению
    /// </summary>
    protected void AddContext(string key, object value)
    {
        Context[key] = value;
    }
}

/// <summary>
/// Исключение при ошибке инициализации Whisper движка
/// </summary>
public class WhisperInitializationException : WhisperException
{
    public WhisperInitializationException(string message) : base(message, "WHISPER_INIT_FAILED") { }
    public WhisperInitializationException(string message, Exception innerException) : base(message, "WHISPER_INIT_FAILED", innerException) { }

    public static WhisperInitializationException ModelNotFound(string modelPath)
    {
        var ex = new WhisperInitializationException($"Whisper model not found: {modelPath}");
        ex.AddContext("ModelPath", modelPath);
        return ex;
    }

    public static WhisperInitializationException ModelLoadFailed(string modelPath, Exception innerException)
    {
        var ex = new WhisperInitializationException($"Failed to load Whisper model: {modelPath}", innerException);
        ex.AddContext("ModelPath", modelPath);
        return ex;
    }

    public static WhisperInitializationException InsufficientMemory(string modelSize)
    {
        var ex = new WhisperInitializationException($"Insufficient memory to load {modelSize} model");
        ex.AddContext("ModelSize", modelSize);
        return ex;
    }
}

/// <summary>
/// Исключение при ошибке конфигурации Whisper
/// </summary>
public class WhisperConfigurationException : WhisperException
{
    public string? SettingName { get; }

    public WhisperConfigurationException(string message) : base(message, "WHISPER_CONFIG_INVALID") { }
    public WhisperConfigurationException(string message, string settingName) : base(message, "WHISPER_CONFIG_INVALID")
    {
        SettingName = settingName;
    }

    public static WhisperConfigurationException InvalidModelSize(string modelSize)
    {
        var ex = new WhisperConfigurationException($"Invalid model size: {modelSize}. Supported: {string.Join(", ", WhisperConstants.ModelSizes.All)}", WhisperConstants.SettingsKeys.ModelSize);
        ex.AddContext("ModelSize", modelSize);
        ex.AddContext("SupportedSizes", WhisperConstants.ModelSizes.All);
        return ex;
    }

    public static WhisperConfigurationException InvalidLanguage(string language)
    {
        var ex = new WhisperConfigurationException($"Invalid language: {language}. Supported: {string.Join(", ", WhisperConstants.Languages.Supported)}", WhisperConstants.SettingsKeys.Language);
        ex.AddContext("Language", language);
        ex.AddContext("SupportedLanguages", WhisperConstants.Languages.Supported);
        return ex;
    }

    public static WhisperConfigurationException InvalidTemperature(float temperature)
    {
        var ex = new WhisperConfigurationException($"Temperature must be between {WhisperConstants.Performance.MinTemperature} and {WhisperConstants.Performance.MaxTemperature}, got: {temperature}", WhisperConstants.SettingsKeys.Temperature);
        ex.AddContext("Temperature", temperature);
        ex.AddContext("MinTemperature", WhisperConstants.Performance.MinTemperature);
        ex.AddContext("MaxTemperature", WhisperConstants.Performance.MaxTemperature);
        return ex;
    }

    public static WhisperConfigurationException InvalidThreadCount(int threadCount)
    {
        var ex = new WhisperConfigurationException($"Thread count must be between {WhisperConstants.Performance.MinThreadCount} and {WhisperConstants.Performance.MaxThreadCount}, got: {threadCount}", WhisperConstants.SettingsKeys.ThreadCount);
        ex.AddContext("ThreadCount", threadCount);
        ex.AddContext("MinThreadCount", WhisperConstants.Performance.MinThreadCount);
        ex.AddContext("MaxThreadCount", WhisperConstants.Performance.MaxThreadCount);
        return ex;
    }

    public static WhisperConfigurationException InvalidConfiguration(string details)
    {
        return new WhisperConfigurationException($"Invalid Whisper configuration: {details}");
    }
}

/// <summary>
/// Исключение при ошибке обработки аудио данных
/// </summary>
public class WhisperAudioException : WhisperException
{
    public WhisperAudioException(string message) : base(message, "WHISPER_AUDIO_ERROR") { }
    public WhisperAudioException(string message, Exception innerException) : base(message, "WHISPER_AUDIO_ERROR", innerException) { }

    public static WhisperAudioException InvalidFormat(int sampleRate, int channels, int bitsPerSample)
    {
        var ex = new WhisperAudioException($"Invalid audio format. Expected: {WhisperConstants.Audio.RequiredSampleRate}Hz, {WhisperConstants.Audio.RequiredChannels} channel, {WhisperConstants.Audio.RequiredBitsPerSample}-bit. Got: {sampleRate}Hz, {channels} channels, {bitsPerSample}-bit");
        ex.AddContext("SampleRate", sampleRate);
        ex.AddContext("Channels", channels);
        ex.AddContext("BitsPerSample", bitsPerSample);
        ex.AddContext("RequiredSampleRate", WhisperConstants.Audio.RequiredSampleRate);
        ex.AddContext("RequiredChannels", WhisperConstants.Audio.RequiredChannels);
        ex.AddContext("RequiredBitsPerSample", WhisperConstants.Audio.RequiredBitsPerSample);
        return ex;
    }

    public static WhisperAudioException TooShort(int lengthMs)
    {
        var ex = new WhisperAudioException($"Audio too short: {lengthMs}ms. Minimum: {WhisperConstants.Audio.MinAudioLengthMs}ms");
        ex.AddContext("AudioLengthMs", lengthMs);
        ex.AddContext("MinLengthMs", WhisperConstants.Audio.MinAudioLengthMs);
        return ex;
    }

    public static WhisperAudioException TooLong(int lengthMs)
    {
        var ex = new WhisperAudioException($"Audio too long: {lengthMs}ms. Maximum: {WhisperConstants.Audio.MaxAudioLengthMs}ms");
        ex.AddContext("AudioLengthMs", lengthMs);
        ex.AddContext("MaxLengthMs", WhisperConstants.Audio.MaxAudioLengthMs);
        return ex;
    }

    public static WhisperAudioException EmptyData()
    {
        return new WhisperAudioException("Audio data is null or empty");
    }
}

/// <summary>
/// Исключение при ошибке распознавания речи
/// </summary>
public class WhisperRecognitionException : WhisperException
{
    public TimeSpan? ProcessingTime { get; }

    public WhisperRecognitionException(string message) : base(message, "WHISPER_RECOGNITION_FAILED") { }
    public WhisperRecognitionException(string message, Exception innerException) : base(message, "WHISPER_RECOGNITION_FAILED", innerException) { }
    public WhisperRecognitionException(string message, TimeSpan processingTime) : base(message, "WHISPER_RECOGNITION_FAILED")
    {
        ProcessingTime = processingTime;
    }

    public static WhisperRecognitionException ProcessingFailed(Exception innerException, TimeSpan processingTime)
    {
        var ex = new WhisperRecognitionException("Speech recognition processing failed", processingTime);
        ex.AddContext("ProcessingTime", processingTime);
        ex.AddContext("InnerException", innerException.Message);
        return ex;
    }

    public static WhisperRecognitionException Timeout(TimeSpan timeout)
    {
        var ex = new WhisperRecognitionException($"Recognition timeout after {timeout.TotalSeconds:F1} seconds");
        ex.AddContext("TimeoutSeconds", timeout.TotalSeconds);
        return ex;
    }

    public static WhisperRecognitionException NoSpeechDetected()
    {
        return new WhisperRecognitionException("No speech detected in audio data");
    }
}

/// <summary>
/// Исключение при ошибках GPU ускорения
/// </summary>
public class WhisperGpuException : WhisperException
{
    public WhisperGpuException(string message) : base(message, "WHISPER_GPU_ERROR") { }
    public WhisperGpuException(string message, Exception innerException) : base(message, "WHISPER_GPU_ERROR", innerException) { }

    public static WhisperGpuException NotAvailable()
    {
        return new WhisperGpuException("GPU acceleration is not available on this system");
    }

    public static WhisperGpuException InitializationFailed(Exception innerException)
    {
        return new WhisperGpuException("Failed to initialize GPU acceleration", innerException);
    }

    public static WhisperGpuException OutOfMemory()
    {
        return new WhisperGpuException("GPU out of memory for Whisper model");
    }
}