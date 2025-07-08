using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.SpeechRecognition.Whisper.Models;
using ChatCaster.SpeechRecognition.Whisper.Services;
using ChatCaster.SpeechRecognition.Whisper.Utils;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ChatCaster.SpeechRecognition.Whisper.ConsoleTest;

/// <summary>
/// Функциональные тесты базовой работоспособности Whisper модуля
/// </summary>
public class FunctionalTests
{
    private readonly ILogger<FunctionalTests> _logger;
    private readonly ISpeechRecognitionService _speechService;
    private readonly WhisperModelManager _modelManager;
    private readonly AudioConverter _audioConverter;
    private readonly ModelDownloader _modelDownloader;

    public FunctionalTests(
        ILogger<FunctionalTests> logger,
        ISpeechRecognitionService speechService,
        WhisperModelManager modelManager,
        AudioConverter audioConverter,
        ModelDownloader modelDownloader)
    {
        _logger = logger;
        _speechService = speechService;
        _modelManager = modelManager;
        _audioConverter = audioConverter;
        _modelDownloader = modelDownloader;
    }

    /// <summary>
    /// Тестирует загрузку моделей
    /// </summary>
    public async Task TestModelDownloadAsync()
    {
        Log.Information("\n🔽 Testing model download and management...");

        // Тестируем tiny модель (самая маленькая)
        var modelSize = WhisperConstants.ModelSizes.Tiny;
        var modelDirectory = Path.Combine(Directory.GetCurrentDirectory(), "models");

        try
        {
            // Проверяем доступность модели
            var availability = await _modelManager.CheckModelAvailabilityAsync(modelSize, modelDirectory);
            Log.Information($"   Model {modelSize} availability:");
            Log.Information($"   - Local: {availability.IsAvailableLocally}");
            Log.Information($"   - Download: {availability.IsAvailableForDownload}");
            Log.Information($"   - Supported: {availability.IsSupported}");

            if (availability.IsAvailableForDownload)
            {
                Log.Information($"   - Download size: {availability.DownloadSizeBytes / 1024.0 / 1024.0:F1} MB");
            }

            // Подготавливаем модель (загружаем если нужно)
            Log.Information($"   Preparing model {modelSize}...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var modelPath = await _modelManager.PrepareModelAsync(modelSize, modelDirectory);
            stopwatch.Stop();

            Log.Information($"   ✅ Model ready: {Path.GetFileName(modelPath)}");
            Log.Information($"   ⏱️ Preparation time: {stopwatch.ElapsedMilliseconds}ms");

            // Проверяем файл модели
            var fileInfo = new FileInfo(modelPath);
            Log.Information($"   📁 File size: {fileInfo.Length / 1024.0 / 1024.0:F1} MB");
            Log.Information($"   📅 Last modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            Log.Information($"   ❌ Model download test failed: {ex.Message}");
            _logger.LogError(ex, "Model download test failed");
            throw;
        }
    }

    /// <summary>
    /// Тестирует конвертацию аудио
    /// </summary>
    public async Task TestAudioConversionAsync()
    {
        Log.Information("\n🎵 Testing audio conversion...");

        try
        {
            // Создаем тестовый аудио сигнал (синусоида)
            var testAudio = GenerateTestAudio(duration: 3.0, frequency: 440.0); // 3 секунды, 440 Hz
            Log.Information($"   Generated test audio: {testAudio.Length} bytes");

            // Получаем информацию об аудио
            var audioInfo = _audioConverter.GetAudioInfo(
                testAudio,
                WhisperConstants.Audio.RequiredSampleRate,
                WhisperConstants.Audio.RequiredChannels,
                WhisperConstants.Audio.RequiredBitsPerSample);

            Log.Information($"   Audio info: {audioInfo}");
            Log.Information($"   Compatible with Whisper: {audioInfo.IsCompatibleWithWhisper}");

            // Тестируем конвертацию
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var samples = await _audioConverter.ConvertToSamplesAsync(testAudio, CancellationToken.None);
            stopwatch.Stop();

            Log.Information($"   ✅ Conversion completed: {samples.Length} samples");
            Log.Information($"   ⏱️ Conversion time: {stopwatch.ElapsedMilliseconds}ms");

            // Анализируем результат
            var avgAmplitude = samples.Select(Math.Abs).Average();
            var maxAmplitude = samples.Select(Math.Abs).Max();
            Log.Information($"   📊 Average amplitude: {avgAmplitude:F4}");
            Log.Information($"   📊 Max amplitude: {maxAmplitude:F4}");

            // Тестируем разные форматы
            await TestDifferentAudioFormatsAsync();
        }
        catch (Exception ex)
        {
            Log.Information($"   ❌ Audio conversion test failed: {ex.Message}");
            _logger.LogError(ex, "Audio conversion test failed");
            throw;
        }
    }

    /// <summary>
    /// Тестирует распознавание речи
    /// </summary>
    public async Task TestSpeechRecognitionAsync()
    {
        Log.Information("\n🎤 Testing speech recognition...");

        try
        {
            // Инициализируем движок
            Log.Information("   Initializing speech recognition engine...");
            var config = CreateTestConfig();
            
            var initStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var initialized = await _speechService.InitializeAsync(config);
            initStopwatch.Stop();

            if (!initialized)
            {
                throw new InvalidOperationException("Failed to initialize speech recognition engine");
            }

            Log.Information($"   ✅ Engine initialized in {initStopwatch.ElapsedMilliseconds}ms");

            // Получаем возможности движка
            var capabilities = await _speechService.GetCapabilitiesAsync();
            Log.Information($"   🔧 Engine capabilities:");
            Log.Information($"   - Language auto-detection: {capabilities.SupportsLanguageAutoDetection}");
            Log.Information($"   - GPU acceleration: {capabilities.SupportsGpuAcceleration}");
            Log.Information($"   - Real-time processing: {capabilities.SupportsRealTimeProcessing}");
            Log.Information($"   - Requires internet: {capabilities.RequiresInternetConnection}");
            Log.Information($"   - Sample rates: {string.Join(", ", capabilities.SupportedSampleRates)}");

            // Получаем поддерживаемые языки
            var languages = await _speechService.GetSupportedLanguagesAsync();
            Log.Information($"   🌐 Supported languages: {string.Join(", ", languages.Take(5))}...");

            // Тестируем распознавание с тестовым аудио
            await TestRecognitionWithDifferentInputsAsync();
        }
        catch (Exception ex)
        {
            Log.Information($"   ❌ Speech recognition test failed: {ex.Message}");
            _logger.LogError(ex, "Speech recognition test failed");
            throw;
        }
    }

    #region Private Methods

    private byte[] GenerateTestAudio(double duration, double frequency)
    {
        var sampleRate = WhisperConstants.Audio.RequiredSampleRate;
        var channels = WhisperConstants.Audio.RequiredChannels;
        var bitsPerSample = WhisperConstants.Audio.RequiredBitsPerSample;
        
        var totalSamples = (int)(duration * sampleRate);
        var audioData = new byte[totalSamples * channels * (bitsPerSample / 8)];

        for (int i = 0; i < totalSamples; i++)
        {
            // Генерируем синусоиду с затуханием для более реалистичного звука
            var time = i / (double)sampleRate;
            var amplitude = Math.Sin(2 * Math.PI * frequency * time) * Math.Exp(-time * 0.5);
            var sample = (short)(amplitude * 16384); // 50% от максимальной амплитуды

            // Записываем в little-endian формате
            var sampleBytes = BitConverter.GetBytes(sample);
            audioData[i * 2] = sampleBytes[0];
            audioData[i * 2 + 1] = sampleBytes[1];
        }

        return audioData;
    }

    private async Task TestDifferentAudioFormatsAsync()
    {
        Log.Information("   Testing different audio formats...");

        var testCases = new[]
        {
            new { SampleRate = 8000, Channels = 1, BitsPerSample = 16, Description = "8kHz Mono 16-bit" },
            new { SampleRate = 44100, Channels = 2, BitsPerSample = 16, Description = "44.1kHz Stereo 16-bit" },
            new { SampleRate = 48000, Channels = 1, BitsPerSample = 32, Description = "48kHz Mono 32-bit" }
        };

        foreach (var testCase in testCases)
        {
            try
            {
                var testAudio = GenerateTestAudioWithFormat(1.0, 440.0, testCase.SampleRate, testCase.Channels, testCase.BitsPerSample);
                var samples = await _audioConverter.ConvertToSamplesAsync(testAudio, testCase.SampleRate, testCase.Channels, testCase.BitsPerSample, CancellationToken.None);
                Log.Information($"   ✅ {testCase.Description}: {samples.Length} samples");
            }
            catch (Exception ex)
            {
                Log.Information($"   ❌ {testCase.Description}: {ex.Message}");
            }
        }
    }

    private byte[] GenerateTestAudioWithFormat(double duration, double frequency, int sampleRate, int channels, int bitsPerSample)
    {
        var totalSamples = (int)(duration * sampleRate);
        var bytesPerSample = bitsPerSample / 8;
        var audioData = new byte[totalSamples * channels * bytesPerSample];

        for (int i = 0; i < totalSamples; i++)
        {
            var time = i / (double)sampleRate;
            var amplitude = Math.Sin(2 * Math.PI * frequency * time) * 0.5;

            for (int ch = 0; ch < channels; ch++)
            {
                var sampleIndex = (i * channels + ch) * bytesPerSample;

                if (bitsPerSample == 16)
                {
                    var sample = (short)(amplitude * 16384);
                    var sampleBytes = BitConverter.GetBytes(sample);
                    audioData[sampleIndex] = sampleBytes[0];
                    audioData[sampleIndex + 1] = sampleBytes[1];
                }
                else if (bitsPerSample == 32)
                {
                    var sample = (int)(amplitude * 1073741824); // 2^30
                    var sampleBytes = BitConverter.GetBytes(sample);
                    Array.Copy(sampleBytes, 0, audioData, sampleIndex, 4);
                }
            }
        }

        return audioData;
    }

    private async Task TestRecognitionWithDifferentInputsAsync()
    {
        Log.Information("   Testing recognition with different inputs...");

        var testCases = new[]
        {
            new { Duration = 1.0, Frequency = 440.0, Description = "Short tone (1s)" },
            new { Duration = 3.0, Frequency = 880.0, Description = "Medium tone (3s)" },
            new { Duration = 0.5, Frequency = 220.0, Description = "Very short tone (0.5s)" }
        };

        foreach (var testCase in testCases)
        {
            try
            {
                Log.Information($"     Testing: {testCase.Description}");
                var testAudio = GenerateTestAudio(testCase.Duration, testCase.Frequency);
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var result = await _speechService.RecognizeAsync(testAudio);
                stopwatch.Stop();

                Log.Information($"     ✅ Recognition completed in {stopwatch.ElapsedMilliseconds}ms");
                Log.Information($"     📝 Success: {result.Success}");
                Log.Information($"     🎯 Confidence: {result.Confidence:F2}");
                Log.Information($"     📄 Text: '{result.RecognizedText ?? "null"}'");
                
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    Log.Information($"     ⚠️ Error: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Log.Information($"     ❌ {testCase.Description} failed: {ex.Message}");
            }
        }
    }

    private SpeechRecognitionConfig CreateTestConfig()
    {
        return new SpeechRecognitionConfig
        {
            Engine = WhisperConstants.EngineName,
            Language = "auto",
            UseGpuAcceleration = false,
            MaxTokens = WhisperConstants.Performance.DefaultMaxTokens,
            EngineSettings = new Dictionary<string, object>
            {
                [WhisperConstants.SettingsKeys.ModelSize] = WhisperConstants.ModelSizes.Tiny,
                [WhisperConstants.SettingsKeys.Temperature] = 0.0f,
                [WhisperConstants.SettingsKeys.UseVAD] = true,
                [WhisperConstants.SettingsKeys.ThreadCount] = 2
            }
        };
    }

    #endregion
}