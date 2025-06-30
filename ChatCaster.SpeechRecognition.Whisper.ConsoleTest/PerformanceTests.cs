using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.SpeechRecognition.Whisper.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ChatCaster.SpeechRecognition.Whisper.ConsoleTest;

/// <summary>
/// Тесты производительности Whisper модуля
/// </summary>
public class PerformanceTests
{
    private readonly ILogger<PerformanceTests> _logger;
    private readonly ISpeechRecognitionService _speechService;
    private readonly WhisperModelManager _modelManager;

    public PerformanceTests(
        ILogger<PerformanceTests> logger,
        ISpeechRecognitionService speechService,
        WhisperModelManager modelManager)
    {
        _logger = logger;
        _speechService = speechService;
        _modelManager = modelManager;
    }

    /// <summary>
    /// Тестирует производительность загрузки разных моделей
    /// </summary>
    public async Task TestModelLoadingPerformanceAsync()
    {
        Console.WriteLine("\n⚡ Testing model loading performance...");

        var modelDirectory = Path.Combine(Directory.GetCurrentDirectory(), "models");
        var modelsToTest = new[] { WhisperConstants.ModelSizes.Tiny, WhisperConstants.ModelSizes.Base };

        var results = new List<ModelLoadingResult>();

        foreach (var modelSize in modelsToTest)
        {
            Console.WriteLine($"   Testing {modelSize} model...");
            
            try
            {
                var result = await MeasureModelLoadingAsync(modelSize, modelDirectory);
                results.Add(result);
                
                Console.WriteLine($"   ✅ {modelSize}: {result.TotalTimeMs}ms (Download: {result.DownloadTimeMs}ms, Init: {result.InitTimeMs}ms)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ {modelSize} failed: {ex.Message}");
                _logger.LogError(ex, "Model loading test failed for {ModelSize}", modelSize);
            }
        }

        // Показываем сравнительную таблицу
        ShowModelLoadingComparison(results);
    }

    /// <summary>
    /// Тестирует скорость распознавания речи
    /// </summary>
    public async Task TestRecognitionSpeedAsync()
    {
        Console.WriteLine("\n🚀 Testing recognition speed...");

        // Подготавливаем движок
        await InitializeEngineAsync();

        var audioDurations = new[] { 1.0, 3.0, 5.0, 10.0 }; // секунды
        var results = new List<RecognitionSpeedResult>();

        foreach (var duration in audioDurations)
        {
            Console.WriteLine($"   Testing {duration}s audio...");
            
            try
            {
                var result = await MeasureRecognitionSpeedAsync(duration);
                results.Add(result);
                
                var realtimeRatio = result.ProcessingTimeMs / (duration * 1000);
                Console.WriteLine($"   ✅ {duration}s audio: {result.ProcessingTimeMs}ms ({realtimeRatio:F2}x realtime)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ {duration}s audio failed: {ex.Message}");
                _logger.LogError(ex, "Recognition speed test failed for {Duration}s", duration);
            }
        }

        // Показываем анализ производительности
        ShowRecognitionSpeedAnalysis(results);
    }

    /// <summary>
    /// Тестирует параллельное распознавание
    /// </summary>
    public async Task TestConcurrentRecognitionAsync()
    {
        Console.WriteLine("\n🔄 Testing concurrent recognition...");

        await InitializeEngineAsync();

        var concurrencyLevels = new[] { 1, 2, 4 };
        var audioDuration = 3.0; // секунды
        
        foreach (var concurrency in concurrencyLevels)
        {
            Console.WriteLine($"   Testing {concurrency} concurrent requests...");
            
            try
            {
                var result = await MeasureConcurrentRecognitionAsync(concurrency, audioDuration);
                
                var avgTime = result.IndividualTimes.Average();
                var maxTime = result.IndividualTimes.Max();
                var minTime = result.IndividualTimes.Min();
                
                Console.WriteLine($"   ✅ {concurrency} requests completed in {result.TotalTimeMs}ms");
                Console.WriteLine($"      Average: {avgTime:F0}ms, Min: {minTime:F0}ms, Max: {maxTime:F0}ms");
                Console.WriteLine($"      Throughput: {concurrency * 1000.0 / result.TotalTimeMs:F2} requests/second");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ {concurrency} concurrent requests failed: {ex.Message}");
                _logger.LogError(ex, "Concurrent recognition test failed for {Concurrency}", concurrency);
            }
        }
    }

    /// <summary>
    /// Тестирует производительность с разными настройками
    /// </summary>
    public async Task TestPerformanceWithDifferentSettingsAsync()
    {
        Console.WriteLine("\n⚙️ Testing performance with different settings...");

        var settings = new[]
        {
            new { ThreadCount = 1, Temperature = 0.0f, Description = "1 thread, deterministic" },
            new { ThreadCount = 2, Temperature = 0.0f, Description = "2 threads, deterministic" },
            new { ThreadCount = 4, Temperature = 0.0f, Description = "4 threads, deterministic" },
            new { ThreadCount = 2, Temperature = 0.3f, Description = "2 threads, creative" }
        };

        var audioDuration = 5.0;
        var testAudio = GenerateTestAudio(audioDuration, 440.0);

        foreach (var setting in settings)
        {
            Console.WriteLine($"   Testing: {setting.Description}...");
            
            try
            {
                var config = CreateConfigWithSettings(setting.ThreadCount, setting.Temperature);
                
                var initStopwatch = Stopwatch.StartNew();
                await _speechService.InitializeAsync(config);
                initStopwatch.Stop();

                var recognitionStopwatch = Stopwatch.StartNew();
                var result = await _speechService.RecognizeAsync(testAudio);
                recognitionStopwatch.Stop();

                Console.WriteLine($"   ✅ Init: {initStopwatch.ElapsedMilliseconds}ms, Recognition: {recognitionStopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"      Success: {result.Success}, Confidence: {result.Confidence:F2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ {setting.Description} failed: {ex.Message}");
            }
        }
    }

    #region Private Methods

    private async Task<ModelLoadingResult> MeasureModelLoadingAsync(string modelSize, string modelDirectory)
    {
        var result = new ModelLoadingResult { ModelSize = modelSize };

        // Очищаем кэш для чистого теста
        _modelManager.ClearCache();

        var totalStopwatch = Stopwatch.StartNew();
        
        // Замеряем время подготовки модели
        var prepareStopwatch = Stopwatch.StartNew();
        var modelPath = await _modelManager.PrepareModelAsync(modelSize, modelDirectory);
        prepareStopwatch.Stop();

        // Замеряем время инициализации движка
        var config = CreateTestConfig(modelSize);
        var initStopwatch = Stopwatch.StartNew();
        await _speechService.InitializeAsync(config);
        initStopwatch.Stop();

        totalStopwatch.Stop();

        result.DownloadTimeMs = prepareStopwatch.ElapsedMilliseconds;
        result.InitTimeMs = initStopwatch.ElapsedMilliseconds;
        result.TotalTimeMs = totalStopwatch.ElapsedMilliseconds;
        result.ModelPath = modelPath;

        return result;
    }

    private async Task<RecognitionSpeedResult> MeasureRecognitionSpeedAsync(double audioDuration)
    {
        var testAudio = GenerateTestAudio(audioDuration, 440.0);
        
        var stopwatch = Stopwatch.StartNew();
        var result = await _speechService.RecognizeAsync(testAudio);
        stopwatch.Stop();

        return new RecognitionSpeedResult
        {
            AudioDurationMs = audioDuration * 1000,
            ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
            Success = result.Success,
            Confidence = result.Confidence,
            AudioSizeBytes = testAudio.Length
        };
    }

    private async Task<ConcurrentRecognitionResult> MeasureConcurrentRecognitionAsync(int concurrency, double audioDuration)
    {
        var testAudio = GenerateTestAudio(audioDuration, 440.0);
        var tasks = new List<Task<double>>();
        
        var totalStopwatch = Stopwatch.StartNew();

        // Запускаем параллельные задачи
        for (int i = 0; i < concurrency; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                await _speechService.RecognizeAsync(testAudio);
                stopwatch.Stop();
                return stopwatch.Elapsed.TotalMilliseconds;
            }));
        }

        var individualTimes = await Task.WhenAll(tasks);
        totalStopwatch.Stop();

        return new ConcurrentRecognitionResult
        {
            Concurrency = concurrency,
            TotalTimeMs = totalStopwatch.ElapsedMilliseconds,
            IndividualTimes = individualTimes
        };
    }

    private async Task InitializeEngineAsync()
    {
        if (!_speechService.IsInitialized)
        {
            var config = CreateTestConfig(WhisperConstants.ModelSizes.Tiny);
            await _speechService.InitializeAsync(config);
        }
    }

    private SpeechRecognitionConfig CreateTestConfig(string modelSize)
    {
        return new SpeechRecognitionConfig
        {
            Engine = WhisperConstants.EngineName,
            Language = "auto",
            UseGpuAcceleration = false,
            MaxTokens = WhisperConstants.Performance.DefaultMaxTokens,
            EngineSettings = new Dictionary<string, object>
            {
                [WhisperConstants.SettingsKeys.ModelSize] = modelSize,
                [WhisperConstants.SettingsKeys.Temperature] = 0.0f,
                [WhisperConstants.SettingsKeys.UseVAD] = true,
                [WhisperConstants.SettingsKeys.ThreadCount] = 2
            }
        };
    }

    private SpeechRecognitionConfig CreateConfigWithSettings(int threadCount, float temperature)
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
                [WhisperConstants.SettingsKeys.Temperature] = temperature,
                [WhisperConstants.SettingsKeys.UseVAD] = true,
                [WhisperConstants.SettingsKeys.ThreadCount] = threadCount
            }
        };
    }

    private byte[] GenerateTestAudio(double duration, double frequency)
    {
        var sampleRate = WhisperConstants.Audio.RequiredSampleRate;
        var channels = WhisperConstants.Audio.RequiredChannels;
        var bitsPerSample = WhisperConstants.Audio.RequiredBitsPerSample;
        
        var totalSamples = (int)(duration * sampleRate);
        var bytesPerSample = bitsPerSample / 8;
        var audioData = new byte[totalSamples * channels * bytesPerSample];

        for (int i = 0; i < totalSamples; i++)
        {
            var time = i / (double)sampleRate;
            var amplitude = Math.Sin(2 * Math.PI * frequency * time) * Math.Exp(-time * 0.1);
            var sample = (short)(amplitude * 16384);

            var sampleBytes = BitConverter.GetBytes(sample);
            audioData[i * 2] = sampleBytes[0];
            audioData[i * 2 + 1] = sampleBytes[1];
        }

        return audioData;
    }

    private void ShowModelLoadingComparison(List<ModelLoadingResult> results)
    {
        if (results.Count == 0) return;

        Console.WriteLine("\n   📊 Model Loading Performance Comparison:");
        Console.WriteLine("   ┌─────────────┬──────────────┬──────────────┬──────────────┐");
        Console.WriteLine("   │ Model       │ Download (ms)│ Init (ms)    │ Total (ms)   │");
        Console.WriteLine("   ├─────────────┼──────────────┼──────────────┼──────────────┤");
        
        foreach (var result in results)
        {
            Console.WriteLine($"   │ {result.ModelSize,-11} │ {result.DownloadTimeMs,12} │ {result.InitTimeMs,12} │ {result.TotalTimeMs,12} │");
        }
        
        Console.WriteLine("   └─────────────┴──────────────┴──────────────┴──────────────┘");
    }

    private void ShowRecognitionSpeedAnalysis(List<RecognitionSpeedResult> results)
    {
        if (results.Count == 0) return;

        Console.WriteLine("\n   📈 Recognition Speed Analysis:");
        Console.WriteLine("   ┌─────────────┬──────────────┬──────────────┬──────────────┐");
        Console.WriteLine("   │ Audio (s)   │ Processing   │ Realtime     │ Throughput   │");
        Console.WriteLine("   │             │ Time (ms)    │ Ratio        │ (x speed)    │");
        Console.WriteLine("   ├─────────────┼──────────────┼──────────────┼──────────────┤");
        
        foreach (var result in results)
        {
            var audioSeconds = result.AudioDurationMs / 1000.0;
            var realtimeRatio = result.ProcessingTimeMs / result.AudioDurationMs;
            var throughput = 1.0 / realtimeRatio;
            
            Console.WriteLine($"   │ {audioSeconds,11:F1} │ {result.ProcessingTimeMs,12} │ {realtimeRatio,12:F2} │ {throughput,12:F2} │");
        }
        
        Console.WriteLine("   └─────────────┴──────────────┴──────────────┴──────────────┘");

        // Показываем рекомендации
        var avgRatio = results.Average(r => r.ProcessingTimeMs / r.AudioDurationMs);
        Console.WriteLine($"\n   💡 Average realtime ratio: {avgRatio:F2}x");
        
        if (avgRatio < 1.0)
        {
            Console.WriteLine("   ✅ Performance is better than realtime - suitable for real-time applications");
        }
        else if (avgRatio < 2.0)
        {
            Console.WriteLine("   ⚠️ Performance is close to realtime - may work for near real-time applications");
        }
        else
        {
            Console.WriteLine("   ❌ Performance is slower than realtime - not suitable for real-time applications");
        }
    }

    #endregion
}

#region Result Classes

public class ModelLoadingResult
{
    public string ModelSize { get; set; } = string.Empty;
    public long DownloadTimeMs { get; set; }
    public long InitTimeMs { get; set; }
    public long TotalTimeMs { get; set; }
    public string ModelPath { get; set; } = string.Empty;
}

public class RecognitionSpeedResult
{
    public double AudioDurationMs { get; set; }
    public long ProcessingTimeMs { get; set; }
    public bool Success { get; set; }
    public float Confidence { get; set; }
    public int AudioSizeBytes { get; set; }
}

public class ConcurrentRecognitionResult
{
    public int Concurrency { get; set; }
    public long TotalTimeMs { get; set; }
    public double[] IndividualTimes { get; set; } = Array.Empty<double>();
}

#endregion