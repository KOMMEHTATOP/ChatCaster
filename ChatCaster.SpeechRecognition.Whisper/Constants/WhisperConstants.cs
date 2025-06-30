namespace ChatCaster.SpeechRecognition.Whisper.Constants;

/// <summary>
/// Константы для Whisper речевого движка
/// </summary>
public static class WhisperConstants
{
    /// <summary>
    /// Имя движка для идентификации
    /// </summary>
    public const string EngineName = "Whisper";
    
    /// <summary>
    /// Версия движка
    /// </summary>
    public const string EngineVersion = "1.0.0";
    
    /// <summary>
    /// Поддерживаемые размеры моделей Whisper
    /// </summary>
    public static class ModelSizes
    {
        public const string Tiny = "tiny";
        public const string Base = "base";
        public const string Small = "small";
        public const string Medium = "medium";
        public const string Large = "large";
        public const string LargeV2 = "large-v2";
        public const string LargeV3 = "large-v3";
        
        public static readonly string[] All = 
        {
            Tiny, Base, Small, Medium, Large, LargeV2, LargeV3
        };
        
        public static readonly string Default = Base;
    }
    
    /// <summary>
    /// Поддерживаемые языки (основные)
    /// </summary>
    public static class Languages
    {
        public const string Auto = "auto";
        public const string Russian = "ru";
        public const string English = "en";
        public const string Spanish = "es";
        public const string French = "fr";
        public const string German = "de";
        public const string Chinese = "zh";
        public const string Japanese = "ja";
        public const string Korean = "ko";
        
        public static readonly string[] Supported = 
        {
            Auto, Russian, English, Spanish, French, German, Chinese, Japanese, Korean
        };
        
        public static readonly string Default = Auto;
    }
    
    /// <summary>
    /// Настройки аудио параметров для Whisper
    /// </summary>
    public static class Audio
    {
        public const int RequiredSampleRate = 16000;
        public const int RequiredChannels = 1; // Mono
        public const int RequiredBitsPerSample = 16;
        
        public const int MinAudioLengthMs = 100;
        public const int MaxAudioLengthMs = 30000; // 30 секунд
        public const int DefaultAudioLengthMs = 10000; // 10 секунд
    }
    
    /// <summary>
    /// Лимиты производительности
    /// </summary>
    public static class Performance
    {
        public const int DefaultThreadCount = 4;
        public const int MinThreadCount = 1;
        public const int MaxThreadCount = 16;
        
        public const float DefaultTemperature = 0.0f;
        public const float MinTemperature = 0.0f;
        public const float MaxTemperature = 1.0f;
        
        public const int DefaultMaxTokens = 224;
        public const int MinMaxTokens = 50;
        public const int MaxMaxTokens = 500;
    }
    
    /// <summary>
    /// Ключи для EngineSettings Dictionary
    /// </summary>
    public static class SettingsKeys
    {
        public const string ModelSize = "ModelSize";
        public const string ModelPath = "ModelPath";
        public const string Language = "Language";
        public const string Temperature = "Temperature";
        public const string MaxTokens = "MaxTokens";
        public const string ThreadCount = "ThreadCount";
        public const string UseVAD = "UseVAD";
        public const string EnableGpu = "EnableGpu";
        public const string GpuDevice = "GpuDevice";
        public const string EnableTranslation = "EnableTranslation";
        public const string InitialPrompt = "InitialPrompt";
    }
    
    /// <summary>
    /// Значения по умолчанию для настроек
    /// </summary>
    public static class DefaultSettings
    {
        public readonly static Dictionary<string, object> Values = new()
        {
            [SettingsKeys.ModelSize] = ModelSizes.Default,
            [SettingsKeys.Language] = Languages.Default,
            [SettingsKeys.Temperature] = Performance.DefaultTemperature,
            [SettingsKeys.MaxTokens] = Performance.DefaultMaxTokens,
            [SettingsKeys.ThreadCount] = Performance.DefaultThreadCount,
            [SettingsKeys.UseVAD] = true,
            [SettingsKeys.EnableGpu] = false,
            [SettingsKeys.EnableTranslation] = false,
            [SettingsKeys.InitialPrompt] = string.Empty
        };
    }
    
    /// <summary>
    /// Сообщения об ошибках
    /// </summary>
    public static class ErrorMessages
    {
        public const string ModelNotFound = "Whisper model not found";
        public const string ModelLoadFailed = "Failed to load Whisper model";
        public const string InvalidAudioFormat = "Invalid audio format for Whisper";
        public const string AudioTooShort = "Audio too short for recognition";
        public const string AudioTooLong = "Audio too long for recognition";
        public const string RecognitionFailed = "Speech recognition failed";
        public const string InitializationFailed = "Whisper engine initialization failed";
        public const string InvalidConfiguration = "Invalid Whisper configuration";
        public const string InsufficientMemory = "Insufficient memory for Whisper model";
        public const string GpuNotAvailable = "GPU acceleration not available";
    }
    
    /// <summary>
    /// Стандартные пути для моделей
    /// </summary>
    public static class Paths
    {
        public const string DefaultModelDirectory = "models";
        public const string ModelFileExtension = ".bin";
        
        /// <summary>
        /// Получает имя файла модели по размеру
        /// </summary>
        public static string GetModelFileName(string modelSize)
        {
            return $"ggml-{modelSize}.bin";
        }
        
        /// <summary>
        /// Получает полный путь к модели
        /// </summary>
        public static string GetModelPath(string modelDirectory, string modelSize)
        {
            return Path.Combine(modelDirectory, GetModelFileName(modelSize));
        }
    }
}