using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.SpeechRecognition.Whisper.Exceptions;
using Microsoft.Extensions.Logging;

namespace ChatCaster.SpeechRecognition.Whisper.Utils;

/// <summary>
/// Утилита для конвертации аудио данных в формат, подходящий для Whisper
/// </summary>
public class AudioConverter
{
    private readonly ILogger<AudioConverter> _logger;

    public AudioConverter(ILogger<AudioConverter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Конвертирует байтовый массив аудио данных в массив float samples для Whisper
    /// </summary>
    /// <param name="audioData">RAW аудио данные (PCM)</param>
    /// <param name="sampleRate">Частота дискретизации исходного аудио</param>
    /// <param name="channels">Количество каналов в исходном аудио</param>
    /// <param name="bitsPerSample">Битность исходного аудио</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив float samples для Whisper (моно, 16kHz)</returns>
    public async Task<float[]> ConvertToSamplesAsync(
        byte[] audioData, 
        int sampleRate = 16000, 
        int channels = 1, 
        int bitsPerSample = 16,
        CancellationToken cancellationToken = default)
    {
        if (audioData == null || audioData.Length == 0)
        {
            throw WhisperAudioException.EmptyData();
        }

        _logger.LogDebug("Converting audio: {Size} bytes, {SampleRate}Hz, {Channels}ch, {Bits}bit", 
            audioData.Length, sampleRate, channels, bitsPerSample);

        try
        {
            // Проверяем длительность аудио
            var durationMs = CalculateAudioDuration(audioData.Length, sampleRate, channels, bitsPerSample);
            ValidateAudioDuration(durationMs);

            // Конвертируем в float samples
            float[] samples;
            
            if (bitsPerSample == 16)
            {
                samples = await ConvertInt16ToFloatAsync(audioData, cancellationToken);
            }
            else if (bitsPerSample == 32)
            {
                samples = await ConvertInt32ToFloatAsync(audioData, cancellationToken);
            }
            else
            {
                throw WhisperAudioException.InvalidFormat(sampleRate, channels, bitsPerSample);
            }

            // Конвертируем в моно если нужно
            if (channels > 1)
            {
                samples = await ConvertToMonoAsync(samples, channels, cancellationToken);
            }

            // Ресэмплируем если нужно
            if (sampleRate != WhisperConstants.Audio.RequiredSampleRate)
            {
                samples = await ResampleAsync(samples, sampleRate, WhisperConstants.Audio.RequiredSampleRate, cancellationToken);
            }

            // Нормализуем громкость
            samples = await NormalizeVolumeAsync(samples, cancellationToken);

            _logger.LogDebug("Audio converted successfully: {OutputSamples} samples", samples.Length);
            return samples;
        }
        catch (Exception ex) when (!(ex is WhisperAudioException))
        {
            _logger.LogError(ex, "Failed to convert audio data");
            throw new WhisperAudioException("Audio conversion failed", ex);
        }
    }

    /// <summary>
    /// Упрощенная версия - конвертирует аудио с параметрами по умолчанию для Whisper
    /// </summary>
    /// <param name="audioData">PCM аудио данные (16kHz, моно, 16-bit)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Float samples для Whisper</returns>
    public async Task<float[]> ConvertToSamplesAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        return await ConvertToSamplesAsync(
            audioData, 
            WhisperConstants.Audio.RequiredSampleRate,
            WhisperConstants.Audio.RequiredChannels,
            WhisperConstants.Audio.RequiredBitsPerSample,
            cancellationToken);
    }

    /// <summary>
    /// Проверяет совместимость аудио формата с Whisper
    /// </summary>
    /// <param name="sampleRate">Частота дискретизации</param>
    /// <param name="channels">Количество каналов</param>
    /// <param name="bitsPerSample">Битность</param>
    /// <returns>true если формат поддерживается</returns>
    public bool IsFormatSupported(int sampleRate, int channels, int bitsPerSample)
    {
        return sampleRate > 0 && sampleRate <= 48000 &&
               channels > 0 && channels <= 8 &&
               (bitsPerSample == 16 || bitsPerSample == 32);
    }

    /// <summary>
    /// Вычисляет длительность аудио в миллисекундах
    /// </summary>
    public int CalculateAudioDuration(int dataSize, int sampleRate, int channels, int bitsPerSample)
    {
        var bytesPerSample = bitsPerSample / 8;
        var totalSamples = dataSize / (bytesPerSample * channels);
        return (int)(totalSamples * 1000.0 / sampleRate);
    }

    /// <summary>
    /// Получает информацию об аудио данных
    /// </summary>
    public AudioInfo GetAudioInfo(byte[] audioData, int sampleRate, int channels, int bitsPerSample)
    {
        var duration = CalculateAudioDuration(audioData.Length, sampleRate, channels, bitsPerSample);
        var samplesCount = audioData.Length / (bitsPerSample / 8 * channels);

        return new AudioInfo
        {
            DataSize = audioData.Length,
            DurationMs = duration,
            SampleRate = sampleRate,
            Channels = channels,
            BitsPerSample = bitsPerSample,
            SamplesCount = samplesCount,
            IsCompatibleWithWhisper = sampleRate == WhisperConstants.Audio.RequiredSampleRate &&
                                    channels == WhisperConstants.Audio.RequiredChannels &&
                                    bitsPerSample == WhisperConstants.Audio.RequiredBitsPerSample
        };
    }

    #region Private Methods

    private async Task<float[]> ConvertInt16ToFloatAsync(byte[] data, CancellationToken cancellationToken)
    {
        const int batchSize = 8192; // Обрабатываем по частям для больших файлов
        var samplesCount = data.Length / 2;
        var samples = new float[samplesCount];

        await Task.Run(() =>
        {
            for (int i = 0; i < samplesCount; i += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var endIndex = Math.Min(i + batchSize, samplesCount);
                for (int j = i; j < endIndex; j++)
                {
                    var sample = BitConverter.ToInt16(data, j * 2);
                    samples[j] = sample / 32768.0f; // Нормализация к диапазону [-1, 1]
                }
            }
        }, cancellationToken);

        return samples;
    }

    private async Task<float[]> ConvertInt32ToFloatAsync(byte[] data, CancellationToken cancellationToken)
    {
        const int batchSize = 4096;
        var samplesCount = data.Length / 4;
        var samples = new float[samplesCount];

        await Task.Run(() =>
        {
            for (int i = 0; i < samplesCount; i += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var endIndex = Math.Min(i + batchSize, samplesCount);
                for (int j = i; j < endIndex; j++)
                {
                    var sample = BitConverter.ToInt32(data, j * 4);
                    samples[j] = sample / 2147483648.0f; // Нормализация к диапазону [-1, 1]
                }
            }
        }, cancellationToken);

        return samples;
    }

    private async Task<float[]> ConvertToMonoAsync(float[] samples, int channels, CancellationToken cancellationToken)
    {
        if (channels == 1) return samples;

        var monoSamplesCount = samples.Length / channels;
        var monoSamples = new float[monoSamplesCount];

        await Task.Run(() =>
        {
            for (int i = 0; i < monoSamplesCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                float sum = 0;
                for (int ch = 0; ch < channels; ch++)
                {
                    sum += samples[i * channels + ch];
                }
                monoSamples[i] = sum / channels; // Усредняем каналы
            }
        }, cancellationToken);

        return monoSamples;
    }

    private async Task<float[]> ResampleAsync(float[] samples, int fromRate, int toRate, CancellationToken cancellationToken)
    {
        if (fromRate == toRate) return samples;

        _logger.LogDebug("Resampling from {FromRate}Hz to {ToRate}Hz", fromRate, toRate);

        // Простой линейный ресэмплинг (для продакшена лучше использовать более качественные алгоритмы)
        var ratio = (double)toRate / fromRate;
        var newLength = (int)(samples.Length * ratio);
        var resampled = new float[newLength];

        await Task.Run(() =>
        {
            for (int i = 0; i < newLength; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var sourceIndex = i / ratio;
                var lowerIndex = (int)Math.Floor(sourceIndex);
                var upperIndex = Math.Min(lowerIndex + 1, samples.Length - 1);
                var fraction = sourceIndex - lowerIndex;

                // Линейная интерполяция
                resampled[i] = samples[lowerIndex] * (1 - (float)fraction) + 
                              samples[upperIndex] * (float)fraction;
            }
        }, cancellationToken);

        return resampled;
    }

    private async Task<float[]> NormalizeVolumeAsync(float[] samples, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // Находим максимальную амплитуду
            float maxAmplitude = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                maxAmplitude = Math.Max(maxAmplitude, Math.Abs(samples[i]));
            }

            // Нормализуем если нужно (оставляем небольшой запас)
            if (maxAmplitude > 0.95f)
            {
                var normalizationFactor = 0.95f / maxAmplitude;
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] *= normalizationFactor;
                }
                
                _logger.LogDebug("Audio normalized with factor: {Factor:F3}", normalizationFactor);
            }
        }, cancellationToken);

        return samples;
    }

    private void ValidateAudioDuration(int durationMs)
    {
        if (durationMs < WhisperConstants.Audio.MinAudioLengthMs)
        {
            throw WhisperAudioException.TooShort(durationMs);
        }

        if (durationMs > WhisperConstants.Audio.MaxAudioLengthMs)
        {
            throw WhisperAudioException.TooLong(durationMs);
        }
    }

    #endregion
}

/// <summary>
/// Информация об аудио данных
/// </summary>
public class AudioInfo
{
    public int DataSize { get; set; }
    public int DurationMs { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int BitsPerSample { get; set; }
    public int SamplesCount { get; set; }
    public bool IsCompatibleWithWhisper { get; set; }

    public override string ToString()
    {
        return $"{SampleRate}Hz, {Channels}ch, {BitsPerSample}bit, {DurationMs}ms, {DataSize} bytes";
    }
}