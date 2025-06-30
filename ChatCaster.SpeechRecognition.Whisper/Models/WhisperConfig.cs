using ChatCaster.Core.Models;
using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.SpeechRecognition.Whisper.Exceptions;

namespace ChatCaster.SpeechRecognition.Whisper.Models;

/// <summary>
/// Strongly-typed конфигурация для Whisper движка
/// </summary>
public class WhisperConfig
{
    /// <summary>
    /// Размер модели Whisper (tiny, base, small, medium, large, etc.)
    /// </summary>
    public string ModelSize { get; set; } = WhisperConstants.ModelSizes.Default;

    /// <summary>
    /// Путь к директории с моделями
    /// </summary>
    public string ModelPath { get; set; } = WhisperConstants.Paths.DefaultModelDirectory;

    /// <summary>
    /// Язык распознавания (auto для автоопределения)
    /// </summary>
    public string Language { get; set; } = WhisperConstants.Languages.Default;

    /// <summary>
    /// Температура генерации (0.0 - детерминистично, 1.0 - творчески)
    /// </summary>
    public float Temperature { get; set; } = WhisperConstants.Performance.DefaultTemperature;

    /// <summary>
    /// Максимальное количество токенов для генерации
    /// </summary>
    public int MaxTokens { get; set; } = WhisperConstants.Performance.DefaultMaxTokens;

    /// <summary>
    /// Количество потоков CPU для обработки
    /// </summary>
    public int ThreadCount { get; set; } = WhisperConstants.Performance.DefaultThreadCount;

    /// <summary>
    /// Использовать Voice Activity Detection
    /// </summary>
    public bool UseVAD { get; set; } = true;

    /// <summary>
    /// Включить GPU ускорение (если доступно)
    /// </summary>
    public bool EnableGpu { get; set; } = false;

    /// <summary>
    /// ID GPU устройства (0 - первое, -1 - любое доступное)
    /// </summary>
    public int GpuDevice { get; set; } = 0;

    /// <summary>
    /// Включить перевод на английский язык
    /// </summary>
    public bool EnableTranslation { get; set; } = false;

    /// <summary>
    /// Начальная подсказка для контекста
    /// </summary>
    public string InitialPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Таймаут инициализации модели в секундах
    /// </summary>
    public int InitializationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Таймаут распознавания в секундах
    /// </summary>
    public int RecognitionTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Создает WhisperConfig из SpeechRecognitionConfig
    /// </summary>
    public static WhisperConfig FromSpeechConfig(SpeechRecognitionConfig speechConfig)
    {
        var config = new WhisperConfig
        {
            Language = speechConfig.Language,
            MaxTokens = speechConfig.MaxTokens
        };

        // Применяем настройки из EngineSettings Dictionary
        foreach (var setting in speechConfig.EngineSettings)
        {
            config.ApplySetting(setting.Key, setting.Value);
        }

        return config;
    }

    /// <summary>
    /// Применяет отдельную настройку из EngineSettings
    /// </summary>
    public void ApplySetting(string key, object value)
    {
        switch (key)
        {
            case WhisperConstants.SettingsKeys.ModelSize:
                ModelSize = value?.ToString() ?? WhisperConstants.ModelSizes.Default;
                break;

            case WhisperConstants.SettingsKeys.ModelPath:
                ModelPath = value?.ToString() ?? WhisperConstants.Paths.DefaultModelDirectory;
                break;

            case WhisperConstants.SettingsKeys.Language:
                Language = value?.ToString() ?? WhisperConstants.Languages.Default;
                break;

            case WhisperConstants.SettingsKeys.Temperature:
                Temperature = Convert.ToSingle(value ?? WhisperConstants.Performance.DefaultTemperature);
                break;

            case WhisperConstants.SettingsKeys.MaxTokens:
                MaxTokens = Convert.ToInt32(value ?? WhisperConstants.Performance.DefaultMaxTokens);
                break;

            case WhisperConstants.SettingsKeys.ThreadCount:
                ThreadCount = Convert.ToInt32(value ?? WhisperConstants.Performance.DefaultThreadCount);
                break;

            case WhisperConstants.SettingsKeys.UseVAD:
                UseVAD = Convert.ToBoolean(value ?? true);
                break;

            case WhisperConstants.SettingsKeys.EnableGpu:
                EnableGpu = Convert.ToBoolean(value ?? false);
                break;

            case WhisperConstants.SettingsKeys.GpuDevice:
                GpuDevice = Convert.ToInt32(value ?? 0);
                break;

            case WhisperConstants.SettingsKeys.EnableTranslation:
                EnableTranslation = Convert.ToBoolean(value ?? false);
                break;

            case WhisperConstants.SettingsKeys.InitialPrompt:
                InitialPrompt = value?.ToString() ?? string.Empty;
                break;
        }
    }

    /// <summary>
    /// Конвертирует обратно в Dictionary для EngineSettings
    /// </summary>
    public Dictionary<string, object> ToEngineSettings()
    {
        return new Dictionary<string, object>
        {
            [WhisperConstants.SettingsKeys.ModelSize] = ModelSize,
            [WhisperConstants.SettingsKeys.ModelPath] = ModelPath,
            [WhisperConstants.SettingsKeys.Language] = Language,
            [WhisperConstants.SettingsKeys.Temperature] = Temperature,
            [WhisperConstants.SettingsKeys.MaxTokens] = MaxTokens,
            [WhisperConstants.SettingsKeys.ThreadCount] = ThreadCount,
            [WhisperConstants.SettingsKeys.UseVAD] = UseVAD,
            [WhisperConstants.SettingsKeys.EnableGpu] = EnableGpu,
            [WhisperConstants.SettingsKeys.GpuDevice] = GpuDevice,
            [WhisperConstants.SettingsKeys.EnableTranslation] = EnableTranslation,
            [WhisperConstants.SettingsKeys.InitialPrompt] = InitialPrompt
        };
    }

    /// <summary>
    /// Получает полный путь к файлу модели
    /// </summary>
    public string GetModelFilePath()
    {
        return WhisperConstants.Paths.GetModelPath(ModelPath, ModelSize);
    }

    /// <summary>
    /// Валидирует конфигурацию
    /// </summary>
    public ValidationResult Validate()
    {
        var result = new ValidationResult { IsValid = true };

        // Проверка размера модели
        if (!WhisperConstants.ModelSizes.All.Contains(ModelSize))
        {
            result.AddError($"Invalid model size: {ModelSize}. Supported: {string.Join(", ", WhisperConstants.ModelSizes.All)}");
        }

        // Проверка языка
        if (!WhisperConstants.Languages.Supported.Contains(Language))
        {
            result.AddError($"Invalid language: {Language}. Supported: {string.Join(", ", WhisperConstants.Languages.Supported)}");
        }

        // Проверка температуры
        if (Temperature < WhisperConstants.Performance.MinTemperature || Temperature > WhisperConstants.Performance.MaxTemperature)
        {
            result.AddError($"Temperature must be between {WhisperConstants.Performance.MinTemperature} and {WhisperConstants.Performance.MaxTemperature}, got: {Temperature}");
        }

        // Проверка количества токенов
        if (MaxTokens < WhisperConstants.Performance.MinMaxTokens || MaxTokens > WhisperConstants.Performance.MaxMaxTokens)
        {
            result.AddError($"MaxTokens must be between {WhisperConstants.Performance.MinMaxTokens} and {WhisperConstants.Performance.MaxMaxTokens}, got: {MaxTokens}");
        }

        // Проверка количества потоков
        if (ThreadCount < WhisperConstants.Performance.MinThreadCount || ThreadCount > WhisperConstants.Performance.MaxThreadCount)
        {
            result.AddError($"ThreadCount must be between {WhisperConstants.Performance.MinThreadCount} and {WhisperConstants.Performance.MaxThreadCount}, got: {ThreadCount}");
        }

        // Проверка пути к модели
        if (string.IsNullOrWhiteSpace(ModelPath))
        {
            result.AddError("ModelPath cannot be empty");
        }

        // Проверка файла модели
        var modelFilePath = GetModelFilePath();
        if (!File.Exists(modelFilePath))
        {
            result.AddWarning($"Model file not found: {modelFilePath}");
        }

        // Проверка GPU настроек
        if (EnableGpu && GpuDevice < 0)
        {
            result.AddWarning("GPU device ID should be >= 0 for specific device selection");
        }

        // Проверка таймаутов
        if (InitializationTimeoutSeconds <= 0)
        {
            result.AddError("InitializationTimeoutSeconds must be greater than 0");
        }

        if (RecognitionTimeoutSeconds <= 0)
        {
            result.AddError("RecognitionTimeoutSeconds must be greater than 0");
        }

        return result;
    }

    /// <summary>
    /// Создает копию конфигурации
    /// </summary>
    public WhisperConfig Clone()
    {
        return new WhisperConfig
        {
            ModelSize = ModelSize,
            ModelPath = ModelPath,
            Language = Language,
            Temperature = Temperature,
            MaxTokens = MaxTokens,
            ThreadCount = ThreadCount,
            UseVAD = UseVAD,
            EnableGpu = EnableGpu,
            GpuDevice = GpuDevice,
            EnableTranslation = EnableTranslation,
            InitialPrompt = InitialPrompt,
            InitializationTimeoutSeconds = InitializationTimeoutSeconds,
            RecognitionTimeoutSeconds = RecognitionTimeoutSeconds
        };
    }

    /// <summary>
    /// Создает конфигурацию с настройками по умолчанию
    /// </summary>
    public static WhisperConfig CreateDefault()
    {
        return new WhisperConfig();
    }

    /// <summary>
    /// Создает конфигурацию из Dictionary значений по умолчанию
    /// </summary>
    public static WhisperConfig FromDefaults()
    {
        var config = new WhisperConfig();
        
        foreach (var setting in WhisperConstants.DefaultSettings.Values)
        {
            config.ApplySetting(setting.Key, setting.Value);
        }

        return config;
    }
}