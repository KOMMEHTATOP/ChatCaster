using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.SpeechRecognition.Whisper.Exceptions;
using ChatCaster.SpeechRecognition.Whisper.Models;
using ChatCaster.SpeechRecognition.Whisper.Utils;
using Microsoft.Extensions.Logging;

namespace ChatCaster.SpeechRecognition.Whisper.Services;

/// <summary>
/// Сервис для предобработки аудио данных перед передачей в Whisper движок
/// </summary>
public class WhisperAudioProcessor
{
    private readonly ILogger<WhisperAudioProcessor> _logger;
    private readonly AudioConverter _audioConverter;
    
    public WhisperAudioProcessor(
        ILogger<WhisperAudioProcessor> logger,
        AudioConverter audioConverter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _audioConverter = audioConverter ?? throw new ArgumentNullException(nameof(audioConverter));
    }

    /// <summary>
    /// События прогресса обработки аудио
    /// </summary>
    public event EventHandler<AudioProcessingProgressEventArgs>? ProcessingProgress;

    /// <summary>
    /// Подготавливает аудио данные для обработки Whisper
    /// </summary>
    /// <param name="audioData">Исходные аудио данные</param>
    /// <param name="config">Конфигурация Whisper</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Подготовленные аудио данные</returns>
    public async Task<ProcessedAudioData> PrepareAudioForWhisperAsync(
        byte[] audioData,
        WhisperConfig config,
        CancellationToken cancellationToken = default)
    {
        if (audioData == null || audioData.Length == 0)
        {
            throw WhisperAudioException.EmptyData();
        }

        _logger.LogDebug("Starting audio preprocessing for Whisper, input size: {Size} bytes", audioData.Length);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new ProcessedAudioData
        {
            OriginalSizeBytes = audioData.Length,
            ProcessingStartTime = DateTime.Now
        };

        try
        {
            OnProcessingProgress(new AudioProcessingProgressEventArgs
            {
                Stage = AudioProcessingStage.Validation,
                ProgressPercentage = 10,
                Message = "Validating input audio"
            });

            // Валидация входных данных
            await ValidateInputAudioAsync(audioData, config, cancellationToken);

            OnProcessingProgress(new AudioProcessingProgressEventArgs
            {
                Stage = AudioProcessingStage.NoiseReduction,
                ProgressPercentage = 25,
                Message = "Applying noise reduction"
            });

            // Предварительная очистка от шума (если включено)
            var cleanedAudio = config.UseVAD 
                ? await ApplyNoiseReductionAsync(audioData, cancellationToken)
                : audioData;

            OnProcessingProgress(new AudioProcessingProgressEventArgs
            {
                Stage = AudioProcessingStage.VoiceDetection,
                ProgressPercentage = 40,
                Message = "Detecting voice activity"
            });

            // Voice Activity Detection
            var vadResult = config.UseVAD 
                ? await DetectVoiceActivityAsync(cleanedAudio, cancellationToken)
                : new VoiceActivityResult { HasVoice = true, TrimmedAudio = cleanedAudio };

            result.VoiceActivityResult = vadResult;

            if (!vadResult.HasVoice)
            {
                _logger.LogWarning("No voice activity detected in audio");
                result.SilenceDetected = true;
                result.ProcessingTime = stopwatch.Elapsed;
                return result;
            }

            OnProcessingProgress(new AudioProcessingProgressEventArgs
            {
                Stage = AudioProcessingStage.Conversion,
                ProgressPercentage = 60,
                Message = "Converting audio format"
            });

            // Конвертация в формат Whisper
            var samples = await _audioConverter.ConvertToSamplesAsync(vadResult.TrimmedAudio, cancellationToken);

            OnProcessingProgress(new AudioProcessingProgressEventArgs
            {
                Stage = AudioProcessingStage.Enhancement,
                ProgressPercentage = 80,
                Message = "Enhancing audio quality"
            });

            // Улучшение качества аудио
            var enhancedSamples = await EnhanceAudioQualityAsync(samples, config, cancellationToken);

            OnProcessingProgress(new AudioProcessingProgressEventArgs
            {
                Stage = AudioProcessingStage.Completed,
                ProgressPercentage = 100,
                Message = "Audio preprocessing completed"
            });

            result.ProcessedSamples = enhancedSamples;
            result.ProcessingTime = stopwatch.Elapsed;
            result.Success = true;

            _logger.LogInformation("Audio preprocessing completed successfully: {OriginalSize} bytes → {SamplesCount} samples in {Time}ms",
                audioData.Length, enhancedSamples.Length, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audio preprocessing failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.ProcessingTime = stopwatch.Elapsed;
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Быстрая проверка качества аудио без полной обработки
    /// </summary>
    /// <param name="audioData">Аудио данные</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат анализа качества</returns>
    public async Task<AudioQualityAnalysis> AnalyzeAudioQualityAsync(
        byte[] audioData,
        CancellationToken cancellationToken = default)
    {
        if (audioData == null || audioData.Length == 0)
        {
            return new AudioQualityAnalysis { OverallQuality = AudioQuality.Poor, Issues = ["Empty audio data"] };
        }

        var analysis = new AudioQualityAnalysis();

        try
        {
            // Быстрая конвертация для анализа
            var samples = await _audioConverter.ConvertToSamplesAsync(audioData, cancellationToken);
            
            // Анализ уровня сигнала
            analysis.SignalLevel = AnalyzeSignalLevel(samples);
            
            // Анализ шума
            analysis.NoiseLevel = AnalyzeNoiseLevel(samples);
            
            // Анализ клиппинга
            analysis.ClippingDetected = DetectClipping(samples);
            
            // Анализ тишины
            analysis.SilencePercentage = CalculateSilencePercentage(samples);

            // Определение общего качества
            analysis.OverallQuality = DetermineOverallQuality(analysis);
            
            // Генерация рекомендаций
            analysis.Issues = GenerateQualityIssues(analysis);

            _logger.LogDebug("Audio quality analysis completed: {Quality}, Signal: {Signal:F2}, Noise: {Noise:F2}",
                analysis.OverallQuality, analysis.SignalLevel, analysis.NoiseLevel);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audio quality analysis failed");
            return new AudioQualityAnalysis 
            { 
                OverallQuality = AudioQuality.Poor, 
                Issues = [$"Analysis failed: {ex.Message}"] 
            };
        }
    }

    /// <summary>
    /// Оптимизирует аудио данные для лучшего распознавания
    /// </summary>
    /// <param name="audioData">Исходные аудио данные</param>
    /// <param name="config">Конфигурация</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Оптимизированные аудио данные</returns>
    public async Task<byte[]> OptimizeAudioForRecognitionAsync(
        byte[] audioData,
        WhisperConfig config,
        CancellationToken cancellationToken = default)
    {
        var processed = await PrepareAudioForWhisperAsync(audioData, config, cancellationToken);
        
        if (!processed.Success || processed.ProcessedSamples == null)
        {
            throw new WhisperAudioException("Failed to optimize audio for recognition");
        }

        // Конвертируем обратно в байты для совместимости
        return ConvertSamplesToBytes(processed.ProcessedSamples);
    }

    #region Private Methods

    private async Task ValidateInputAudioAsync(byte[] audioData, WhisperConfig config, CancellationToken cancellationToken)
    {
        // Проверка минимального размера
        if (audioData.Length < 1000) // Минимум 1KB
        {
            throw WhisperAudioException.EmptyData();
        }

        // Получаем информацию об аудио
        var audioInfo = _audioConverter.GetAudioInfo(audioData, 
            WhisperConstants.Audio.RequiredSampleRate,
            WhisperConstants.Audio.RequiredChannels,
            WhisperConstants.Audio.RequiredBitsPerSample);

        // Проверка длительности
        if (audioInfo.DurationMs < WhisperConstants.Audio.MinAudioLengthMs)
        {
            throw WhisperAudioException.TooShort(audioInfo.DurationMs);
        }

        if (audioInfo.DurationMs > WhisperConstants.Audio.MaxAudioLengthMs)
        {
            throw WhisperAudioException.TooLong(audioInfo.DurationMs);
        }

        await Task.CompletedTask;
    }

    private async Task<byte[]> ApplyNoiseReductionAsync(byte[] audioData, CancellationToken cancellationToken)
    {
        // Простое шумоподавление - в реальной реализации можно использовать более сложные алгоритмы
        var samples = await _audioConverter.ConvertToSamplesAsync(audioData, cancellationToken);
        
        // Применяем простой high-pass фильтр для удаления низкочастотного шума
        var filtered = ApplyHighPassFilter(samples, 80.0f); // Убираем частоты ниже 80Hz
        
        return ConvertSamplesToBytes(filtered);
    }

    private async Task<VoiceActivityResult> DetectVoiceActivityAsync(byte[] audioData, CancellationToken cancellationToken)
    {
        var samples = await _audioConverter.ConvertToSamplesAsync(audioData, cancellationToken);
        
        // Простой VAD на основе энергии сигнала
        const float energyThreshold = 0.01f;
        const int frameSize = 400; // ~25ms при 16kHz
        
        var frames = samples.Length / frameSize;
        var voiceFrames = 0;
        var startFrame = -1;
        var endFrame = -1;

        for (int i = 0; i < frames; i++)
        {
            var frameStart = i * frameSize;
            var frameEnd = Math.Min(frameStart + frameSize, samples.Length);
            var energy = CalculateFrameEnergy(samples, frameStart, frameEnd);

            if (energy > energyThreshold)
            {
                voiceFrames++;
                if (startFrame == -1) startFrame = i;
                endFrame = i;
            }
        }

        var voicePercentage = (float)voiceFrames / frames;
        var hasVoice = voicePercentage > 0.1f; // Минимум 10% кадров с голосом

        byte[] trimmedAudio = audioData;
        
        if (hasVoice && startFrame != -1 && endFrame != -1)
        {
            // Обрезаем тишину с краев (с небольшими отступами)
            var trimStart = Math.Max(0, (startFrame - 2) * frameSize);
            var trimEnd = Math.Min(samples.Length, (endFrame + 3) * frameSize);
            
            var trimmedSamples = samples[trimStart..trimEnd];
            trimmedAudio = ConvertSamplesToBytes(trimmedSamples);
        }

        return new VoiceActivityResult
        {
            HasVoice = hasVoice,
            VoicePercentage = voicePercentage,
            TrimmedAudio = trimmedAudio
        };
    }

    private async Task<float[]> EnhanceAudioQualityAsync(float[] samples, WhisperConfig config, CancellationToken cancellationToken)
    {
        var enhanced = samples.ToArray(); // Создаем копию

        await Task.Run(() =>
        {
            // Применяем фильтры для улучшения качества
            enhanced = ApplyHighPassFilter(enhanced, 80.0f); // Убираем низкий шум
            enhanced = ApplyLowPassFilter(enhanced, 8000.0f); // Убираем высокочастотный шум
            enhanced = ApplyDynamicRangeCompression(enhanced); // Компрессия динамического диапазона
            
        }, cancellationToken);

        return enhanced;
    }

    private float[] ApplyHighPassFilter(float[] samples, float cutoffFreq)
    {
        // Простой IIR high-pass фильтр первого порядка
        var sampleRate = WhisperConstants.Audio.RequiredSampleRate;
        var rc = 1.0f / (2.0f * MathF.PI * cutoffFreq);
        var dt = 1.0f / sampleRate;
        var alpha = rc / (rc + dt);

        var filtered = new float[samples.Length];
        filtered[0] = samples[0];

        for (int i = 1; i < samples.Length; i++)
        {
            filtered[i] = alpha * (filtered[i - 1] + samples[i] - samples[i - 1]);
        }

        return filtered;
    }

    private float[] ApplyLowPassFilter(float[] samples, float cutoffFreq)
    {
        // Простой IIR low-pass фильтр первого порядка
        var sampleRate = WhisperConstants.Audio.RequiredSampleRate;
        var rc = 1.0f / (2.0f * MathF.PI * cutoffFreq);
        var dt = 1.0f / sampleRate;
        var alpha = dt / (rc + dt);

        var filtered = new float[samples.Length];
        filtered[0] = samples[0];

        for (int i = 1; i < samples.Length; i++)
        {
            filtered[i] = filtered[i - 1] + alpha * (samples[i] - filtered[i - 1]);
        }

        return filtered;
    }

    private float[] ApplyDynamicRangeCompression(float[] samples)
    {
        // Простая компрессия для выравнивания громкости
        const float threshold = 0.5f;
        const float ratio = 4.0f;

        var compressed = new float[samples.Length];
        
        for (int i = 0; i < samples.Length; i++)
        {
            var sample = samples[i];
            var magnitude = MathF.Abs(sample);
            
            if (magnitude > threshold)
            {
                var excess = magnitude - threshold;
                var compressedExcess = excess / ratio;
                var newMagnitude = threshold + compressedExcess;
                compressed[i] = sample >= 0 ? newMagnitude : -newMagnitude;
            }
            else
            {
                compressed[i] = sample;
            }
        }

        return compressed;
    }

    private float CalculateFrameEnergy(float[] samples, int start, int end)
    {
        float energy = 0;
        for (int i = start; i < end; i++)
        {
            energy += samples[i] * samples[i];
        }
        return energy / (end - start);
    }

    private float AnalyzeSignalLevel(float[] samples)
    {
        return samples.Select(MathF.Abs).Average();
    }

    private float AnalyzeNoiseLevel(float[] samples)
    {
        // Оценка шума как стандартное отклонение в тихих участках
        var quietSamples = samples.Where(s => MathF.Abs(s) < 0.1f).ToArray();
        if (quietSamples.Length == 0) return 0;
        
        var mean = quietSamples.Average();
        var variance = quietSamples.Average(s => (s - mean) * (s - mean));
        return MathF.Sqrt(variance);
    }

    private bool DetectClipping(float[] samples)
    {
        const float clippingThreshold = 0.99f;
        var clippedSamples = samples.Count(s => MathF.Abs(s) > clippingThreshold);
        return clippedSamples > samples.Length * 0.001f; // Более 0.1% клиппинга
    }

    private float CalculateSilencePercentage(float[] samples)
    {
        const float silenceThreshold = 0.005f;
        var silentSamples = samples.Count(s => MathF.Abs(s) < silenceThreshold);
        return (float)silentSamples / samples.Length * 100;
    }

    private AudioQuality DetermineOverallQuality(AudioQualityAnalysis analysis)
    {
        var score = 100;
        
        if (analysis.SignalLevel < 0.01f) score -= 30; // Очень тихий сигнал
        if (analysis.NoiseLevel > 0.05f) score -= 25;  // Высокий уровень шума
        if (analysis.ClippingDetected) score -= 20;    // Клиппинг
        if (analysis.SilencePercentage > 80) score -= 35; // Слишком много тишины

        return score switch
        {
            >= 80 => AudioQuality.Excellent,
            >= 60 => AudioQuality.Good,
            >= 40 => AudioQuality.Fair,
            _ => AudioQuality.Poor
        };
    }

    private List<string> GenerateQualityIssues(AudioQualityAnalysis analysis)
    {
        var issues = new List<string>();
        
        if (analysis.SignalLevel < 0.01f) issues.Add("Signal level too low");
        if (analysis.NoiseLevel > 0.05f) issues.Add("High noise level detected");
        if (analysis.ClippingDetected) issues.Add("Audio clipping detected");
        if (analysis.SilencePercentage > 80) issues.Add("Too much silence in audio");

        return issues;
    }

    private byte[] ConvertSamplesToBytes(float[] samples)
    {
        var bytes = new byte[samples.Length * 2]; // 16-bit samples
        
        for (int i = 0; i < samples.Length; i++)
        {
            var sample = Math.Clamp(samples[i], -1.0f, 1.0f);
            var intSample = (short)(sample * 32767);
            var sampleBytes = BitConverter.GetBytes(intSample);
            bytes[i * 2] = sampleBytes[0];
            bytes[i * 2 + 1] = sampleBytes[1];
        }

        return bytes;
    }

    private void OnProcessingProgress(AudioProcessingProgressEventArgs e)
    {
        ProcessingProgress?.Invoke(this, e);
    }

    #endregion
}

/// <summary>
/// Результат обработки аудио данных
/// </summary>
public class ProcessedAudioData
{
    public bool Success { get; set; }
    public float[]? ProcessedSamples { get; set; }
    public int OriginalSizeBytes { get; set; }
    public bool SilenceDetected { get; set; }
    public VoiceActivityResult? VoiceActivityResult { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public DateTime ProcessingStartTime { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Результат детекции голосовой активности
/// </summary>
public class VoiceActivityResult
{
    public bool HasVoice { get; set; }
    public float VoicePercentage { get; set; }
    public byte[] TrimmedAudio { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Анализ качества аудио
/// </summary>
public class AudioQualityAnalysis
{
    public AudioQuality OverallQuality { get; set; }
    public float SignalLevel { get; set; }
    public float NoiseLevel { get; set; }
    public bool ClippingDetected { get; set; }
    public float SilencePercentage { get; set; }
    public List<string> Issues { get; set; } = new();
}

/// <summary>
/// Аргументы события прогресса обработки аудио
/// </summary>
public class AudioProcessingProgressEventArgs : EventArgs
{
    public AudioProcessingStage Stage { get; set; }
    public double ProgressPercentage { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Стадии обработки аудио
/// </summary>
public enum AudioProcessingStage
{
    Validation,
    NoiseReduction,
    VoiceDetection,
    Conversion,
    Enhancement,
    Completed
}

/// <summary>
/// Качество аудио
/// </summary>
public enum AudioQuality
{
    Poor,
    Fair,
    Good,
    Excellent
}