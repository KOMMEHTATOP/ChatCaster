using ChatCaster.Core.Models;

namespace ChatCaster.SpeechRecognition.Whisper.Models;

/// <summary>
/// Результат распознавания речи от Whisper движка
/// </summary>
public class WhisperResult
{
    /// <summary>
    /// Распознанный текст
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Уровень уверенности распознавания (0.0 - 1.0)
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Время обработки распознавания
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Обнаруженный язык (если использовалось автоопределение)
    /// </summary>
    public string? DetectedLanguage { get; set; }

    /// <summary>
    /// Вероятность обнаруженного языка (0.0 - 1.0)
    /// </summary>
    public float LanguageProbability { get; set; }

    /// <summary>
    /// Сегменты распознавания с временными метками
    /// </summary>
    public List<WhisperSegment> Segments { get; set; } = new();

    /// <summary>
    /// Токены распознавания (детальная информация)
    /// </summary>
    public List<WhisperToken> Tokens { get; set; } = new();

    /// <summary>
    /// Использованная модель Whisper
    /// </summary>
    public string ModelUsed { get; set; } = string.Empty;

    /// <summary>
    /// Размер обработанного аудио в байтах
    /// </summary>
    public int AudioSizeBytes { get; set; }

    /// <summary>
    /// Длительность аудио в миллисекундах
    /// </summary>
    public int AudioDurationMs { get; set; }

    /// <summary>
    /// Был ли использован Voice Activity Detection
    /// </summary>
    public bool VadUsed { get; set; }

    /// <summary>
    /// Было ли обнаружено молчание в аудио
    /// </summary>
    public bool SilenceDetected { get; set; }

    /// <summary>
    /// Дополнительные метаданные от Whisper
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Конвертирует в стандартный VoiceProcessingResult из Core
    /// </summary>
    public VoiceProcessingResult ToVoiceProcessingResult(bool success = true, string? errorMessage = null)
    {
        return new VoiceProcessingResult
        {
            Success = success && !string.IsNullOrWhiteSpace(Text),
            RecognizedText = Text,
            ProcessingTime = ProcessingTime,
            Confidence = Confidence,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Создает WhisperResult из стандартного VoiceProcessingResult
    /// </summary>
    public static WhisperResult FromVoiceProcessingResult(VoiceProcessingResult result)
    {
        return new WhisperResult
        {
            Text = result.RecognizedText ?? string.Empty,
            Confidence = result.Confidence,
            ProcessingTime = result.ProcessingTime
        };
    }

    /// <summary>
    /// Проверяет качество распознавания
    /// </summary>
    public RecognitionQuality GetQuality()
    {
        if (string.IsNullOrWhiteSpace(Text))
            return RecognitionQuality.Failed;

        if (Confidence >= 0.9f)
            return RecognitionQuality.Excellent;
        
        if (Confidence >= 0.7f)
            return RecognitionQuality.Good;
        
        if (Confidence >= 0.5f)
            return RecognitionQuality.Fair;
        
        return RecognitionQuality.Poor;
    }

    /// <summary>
    /// Получает краткую статистику распознавания
    /// </summary>
    public string GetSummary()
    {
        var quality = GetQuality();
        var duration = AudioDurationMs / 1000.0;
        var processingSpeed = ProcessingTime.TotalSeconds / duration;
        
        return $"Quality: {quality}, Text: {Text.Length} chars, " +
               $"Time: {ProcessingTime.TotalMilliseconds:F0}ms, " +
               $"Speed: {processingSpeed:F1}x realtime";
    }

    /// <summary>
    /// Фильтрует слишком короткие или ненадежные результаты
    /// </summary>
    public bool IsValidResult(int minTextLength = 1, float minConfidence = 0.1f)
    {
        return !string.IsNullOrWhiteSpace(Text) &&
               Text.Length >= minTextLength &&
               Confidence >= minConfidence &&
               !SilenceDetected;
    }
}

/// <summary>
/// Сегмент распознавания с временными метками
/// </summary>
public class WhisperSegment
{
    /// <summary>
    /// Текст сегмента
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Время начала в секундах
    /// </summary>
    public float StartTime { get; set; }

    /// <summary>
    /// Время окончания в секундах
    /// </summary>
    public float EndTime { get; set; }

    /// <summary>
    /// Уровень уверенности сегмента
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Средняя логарифмическая вероятность
    /// </summary>
    public float AvgLogprob { get; set; }

    /// <summary>
    /// Степень сжатия (compression ratio)
    /// </summary>
    public float CompressionRatio { get; set; }

    /// <summary>
    /// Вероятность отсутствия речи
    /// </summary>
    public float NoSpeechProb { get; set; }

    /// <summary>
    /// Длительность сегмента в секундах
    /// </summary>
    public float Duration => EndTime - StartTime;

    /// <summary>
    /// Проверяет валидность сегмента
    /// </summary>
    public bool IsValid => StartTime >= 0 && EndTime > StartTime && !string.IsNullOrWhiteSpace(Text);
}

/// <summary>
/// Токен распознавания (детальная информация)
/// </summary>
public class WhisperToken
{
    /// <summary>
    /// ID токена в словаре Whisper
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Текст токена
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Логарифмическая вероятность токена
    /// </summary>
    public float Logprob { get; set; }

    /// <summary>
    /// Время начала токена в секундах
    /// </summary>
    public float StartTime { get; set; }

    /// <summary>
    /// Время окончания токена в секундах
    /// </summary>
    public float EndTime { get; set; }

    /// <summary>
    /// Вероятность токена (exp(Logprob))
    /// </summary>
    public float Probability => (float)Math.Exp(Logprob);

    /// <summary>
    /// Длительность токена в секундах
    /// </summary>
    public float Duration => EndTime - StartTime;
}

/// <summary>
/// Качество распознавания
/// </summary>
public enum RecognitionQuality
{
    Failed = 0,      // Распознавание не удалось
    Poor = 1,        // Плохое качество (< 0.5)
    Fair = 2,        // Удовлетворительное (0.5-0.7)
    Good = 3,        // Хорошее (0.7-0.9)
    Excellent = 4    // Отличное (> 0.9)
}

/// <summary>
/// Фабрика для создания результатов Whisper
/// </summary>
public static class WhisperResultFactory
{
    /// <summary>
    /// Создает успешный результат
    /// </summary>
    public static WhisperResult CreateSuccess(string text, float confidence, TimeSpan processingTime, string modelUsed = "")
    {
        return new WhisperResult
        {
            Text = text,
            Confidence = confidence,
            ProcessingTime = processingTime,
            ModelUsed = modelUsed
        };
    }

    /// <summary>
    /// Создает результат с ошибкой
    /// </summary>
    public static WhisperResult CreateError(TimeSpan processingTime, string modelUsed = "")
    {
        return new WhisperResult
        {
            Text = string.Empty,
            Confidence = 0.0f,
            ProcessingTime = processingTime,
            ModelUsed = modelUsed,
            SilenceDetected = true
        };
    }

    /// <summary>
    /// Создает результат для молчания
    /// </summary>
    public static WhisperResult CreateSilence(TimeSpan processingTime, string modelUsed = "")
    {
        return new WhisperResult
        {
            Text = string.Empty,
            Confidence = 0.0f,
            ProcessingTime = processingTime,
            ModelUsed = modelUsed,
            SilenceDetected = true,
            VadUsed = true
        };
    }
}