using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.SpeechRecognition.Whisper.Services;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Diagnostics;

namespace ChatCaster.SpeechRecognition.Whisper.ConsoleTest;

/// <summary>
/// Тесты использования памяти и поиска утечек памяти
/// </summary>
public class MemoryTests
{
    private readonly ILogger<MemoryTests> _logger;
    private readonly ISpeechRecognitionService _speechService;
    private readonly WhisperModelManager _modelManager;

    public MemoryTests(
        ILogger<MemoryTests> logger,
        ISpeechRecognitionService speechService,
        WhisperModelManager modelManager)
    {
        _logger = logger;
        _speechService = speechService;
        _modelManager = modelManager;
    }

    /// <summary>
    /// Тестирует общее использование памяти
    /// </summary>
    public async Task TestMemoryUsageAsync()
    {
        Log.Information("\n💾 Testing memory usage...");

        // Замеряем базовое потребление памяти
        var baseline = MeasureMemoryUsage("Baseline");
        Log.Information($"   📊 {baseline}");

        try
        {
            // Инициализируем движок и замеряем память
            Log.Information("   Initializing speech engine...");
            var config = CreateTestConfig();
            await _speechService.InitializeAsync(config);
            
            var afterInit = MeasureMemoryUsage("After initialization");
            Log.Information($"   📊 {afterInit}");
            
            var initMemoryIncrease = afterInit.WorkingSetMB - baseline.WorkingSetMB;
            Log.Information($"   📈 Memory increase after init: {initMemoryIncrease:F1} MB");

            // Выполняем несколько распознаваний и замеряем память
            Log.Information("   Performing recognition operations...");
            await PerformMultipleRecognitionsAsync(5);
            
            var afterRecognition = MeasureMemoryUsage("After recognition");
            Log.Information($"   📊 {afterRecognition}");
            
            var recognitionMemoryIncrease = afterRecognition.WorkingSetMB - afterInit.WorkingSetMB;
            Log.Information($"   📈 Memory increase during recognition: {recognitionMemoryIncrease:F1} MB");

            // Принудительная сборка мусора
            Log.Information("   Performing garbage collection...");
            ForceGarbageCollection();
            
            var afterGC = MeasureMemoryUsage("After GC");
            Log.Information($"   📊 {afterGC}");
            
            var memoryRecovered = afterRecognition.ManagedMemoryMB - afterGC.ManagedMemoryMB;
            Log.Information($"   ♻️ Memory recovered by GC: {memoryRecovered:F1} MB");

            // Анализ эффективности памяти
            AnalyzeMemoryEfficiency(baseline, afterInit, afterRecognition, afterGC);
        }
        catch (Exception ex)
        {
            Log.Information($"   ❌ Memory usage test failed: {ex.Message}");
            _logger.LogError(ex, "Memory usage test failed");
            throw;
        }
    }

    /// <summary>
    /// Тестирует утечки памяти при повторных операциях
    /// </summary>
    public async Task TestMemoryLeaksAsync()
    {
        Log.Information("\n🔍 Testing memory leaks...");

        try
        {
            // Инициализируем движок
            await InitializeEngineAsync();
            
            var iterations = 10;
            var memorySnapshots = new List<MemorySnapshot>();

            Log.Information($"   Running {iterations} iterations...");

            for (int i = 0; i < iterations; i++)
            {
                Console.Write($"   Iteration {i + 1}/{iterations}... ");
                
                // Выполняем операции которые могут привести к утечкам
                await PerformLeakTestOperationsAsync();
                
                // Принудительная сборка мусора после каждой итерации
                ForceGarbageCollection();
                
                var snapshot = MeasureMemoryUsage($"Iteration {i + 1}");
                memorySnapshots.Add(snapshot);
                
                Log.Information($"Managed: {snapshot.ManagedMemoryMB:F1}MB, Working: {snapshot.WorkingSetMB:F1}MB");
                
                // Небольшая пауза между итерациями
                await Task.Delay(100);
            }

            // Анализируем тренд потребления памяти
            AnalyzeMemoryTrend(memorySnapshots);
        }
        catch (Exception ex)
        {
            Log.Information($"   ❌ Memory leak test failed: {ex.Message}");
            _logger.LogError(ex, "Memory leak test failed");
            throw;
        }
    }

    /// <summary>
    /// Тестирует поведение сборщика мусора
    /// </summary>
    public async Task TestGarbageCollectionAsync()
    {
        Log.Information("\n♻️ Testing garbage collection behavior...");

        try
        {
            await InitializeEngineAsync();

            var gcBefore = GetGCInfo();
            Log.Information("   GC stats before test:");
            ShowGCInfo(gcBefore);

            // Создаем нагрузку на память
            Log.Information("   Creating memory pressure...");
            await CreateMemoryPressureAsync();

            var gcAfter = GetGCInfo();
            Log.Information("   GC stats after test:");
            ShowGCInfo(gcAfter);

            // Анализируем активность GC
            AnalyzeGCActivity(gcBefore, gcAfter);

            // Тестируем поведение при больших объектах
            await TestLargeObjectHeapAsync();
        }
        catch (Exception ex)
        {
            Log.Information($"   ❌ GC test failed: {ex.Message}");
            _logger.LogError(ex, "GC test failed");
            throw;
        }
    }

    /// <summary>
    /// Тестирует поведение памяти при стрессовой нагрузке
    /// </summary>
    public async Task TestMemoryUnderStressAsync()
    {
        Log.Information("\n🔥 Testing memory under stress...");

        try
        {
            await InitializeEngineAsync();

            var stressTests = new[]
            {
                new { Name = "Rapid recognition", Action = new Func<Task>(() => TestRapidRecognitionAsync()) },
                new { Name = "Large audio files", Action = new Func<Task>(() => TestLargeAudioFilesAsync()) },
                new { Name = "Concurrent operations", Action = new Func<Task>(() => TestConcurrentMemoryUsageAsync()) }
            };

            foreach (var test in stressTests)
            {
                Log.Information($"   Running stress test: {test.Name}...");
                
                var memoryBefore = MeasureMemoryUsage("Before stress");
                var stopwatch = Stopwatch.StartNew();
                
                await test.Action();
                
                stopwatch.Stop();
                ForceGarbageCollection();
                var memoryAfter = MeasureMemoryUsage("After stress");
                
                var memoryDelta = memoryAfter.WorkingSetMB - memoryBefore.WorkingSetMB;
                Log.Information($"   ✅ {test.Name}: {stopwatch.ElapsedMilliseconds}ms, Memory delta: {memoryDelta:F1}MB");
            }
        }
        catch (Exception ex)
        {
            Log.Information($"   ❌ Stress test failed: {ex.Message}");
            _logger.LogError(ex, "Memory stress test failed");
            throw;
        }
    }

    #region Private Methods

    private MemorySnapshot MeasureMemoryUsage(string label)
    {
        var process = Process.GetCurrentProcess();
        
        return new MemorySnapshot
        {
            Label = label,
            Timestamp = DateTime.Now,
            WorkingSetMB = process.WorkingSet64 / 1024.0 / 1024.0,
            ManagedMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }

    private void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private async Task PerformMultipleRecognitionsAsync(int count)
    {
        var testAudio = GenerateTestAudio(3.0, 440.0);
        
        for (int i = 0; i < count; i++)
        {
            await _speechService.RecognizeAsync(testAudio);
        }
    }

    private async Task PerformLeakTestOperationsAsync()
    {
        // Операции которые потенциально могут привести к утечкам:
        
        // 1. Создание и освобождение аудио данных
        var testAudio = GenerateTestAudio(2.0, 440.0);
        await _speechService.RecognizeAsync(testAudio);
        
        // 2. Переконфигурация движка
        var config = CreateTestConfig();
        await _speechService.ReloadConfigAsync(config);
        
        // 3. Очистка кэша модели
        _modelManager.ClearCache();
    }

    private void AnalyzeMemoryEfficiency(
        MemorySnapshot baseline, 
        MemorySnapshot afterInit, 
        MemorySnapshot afterRecognition, 
        MemorySnapshot afterGC)
    {
        Log.Information("\n   📈 Memory Efficiency Analysis:");
        
        var initOverhead = afterInit.WorkingSetMB - baseline.WorkingSetMB;
        var recognitionOverhead = afterRecognition.WorkingSetMB - afterInit.WorkingSetMB;
        var gcEfficiency = (afterRecognition.ManagedMemoryMB - afterGC.ManagedMemoryMB) / afterRecognition.ManagedMemoryMB * 100;
        
        Log.Information($"   • Initialization overhead: {initOverhead:F1} MB");
        Log.Information($"   • Recognition overhead: {recognitionOverhead:F1} MB");
        Log.Information($"   • GC efficiency: {gcEfficiency:F1}% memory recovered");
        
        // Оценки
        if (initOverhead < 100)
            Log.Information("   ✅ Initialization memory usage is reasonable");
        else
            Log.Information("   ⚠️ High initialization memory usage");
            
        if (recognitionOverhead < 50)
            Log.Information("   ✅ Recognition memory usage is efficient");
        else
            Log.Information("   ⚠️ High recognition memory usage");
            
        if (gcEfficiency > 70)
            Log.Information("   ✅ Good garbage collection efficiency");
        else
            Log.Information("   ⚠️ Poor garbage collection efficiency - possible memory leaks");
    }

    private void AnalyzeMemoryTrend(List<MemorySnapshot> snapshots)
    {
        if (snapshots.Count < 3) return;

        Log.Information("\n   📊 Memory Trend Analysis:");
        
        var firstHalf = snapshots.Take(snapshots.Count / 2).ToList();
        var secondHalf = snapshots.Skip(snapshots.Count / 2).ToList();
        
        var firstHalfAvg = firstHalf.Average(s => s.ManagedMemoryMB);
        var secondHalfAvg = secondHalf.Average(s => s.ManagedMemoryMB);
        var trend = secondHalfAvg - firstHalfAvg;
        
        Log.Information($"   • First half average: {firstHalfAvg:F1} MB");
        Log.Information($"   • Second half average: {secondHalfAvg:F1} MB");
        Log.Information($"   • Trend: {trend:F1} MB ({(trend > 0 ? "increasing" : "stable")})");
        
        if (Math.Abs(trend) < 1.0)
        {
            Log.Information("   ✅ Memory usage is stable - no significant leaks detected");
        }
        else if (trend > 0)
        {
            Log.Information("   ⚠️ Memory usage is increasing - possible memory leak");
        }
        else
        {
            Log.Information("   ✅ Memory usage is decreasing - good memory management");
        }
    }

    private GCInfo GetGCInfo()
    {
        return new GCInfo
        {
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            TotalMemory = GC.GetTotalMemory(false),
            MaxGeneration = GC.MaxGeneration
        };
    }

    private void ShowGCInfo(GCInfo info)
    {
        Log.Information($"   • Total memory: {info.TotalMemory / 1024.0 / 1024.0:F1} MB");
        Log.Information($"   • Gen 0 collections: {info.Gen0Collections}");
        Log.Information($"   • Gen 1 collections: {info.Gen1Collections}");
        Log.Information($"   • Gen 2 collections: {info.Gen2Collections}");
        Log.Information($"   • Max generation: {info.MaxGeneration}");
    }

    private void AnalyzeGCActivity(GCInfo before, GCInfo after)
    {
        var gen0Delta = after.Gen0Collections - before.Gen0Collections;
        var gen1Delta = after.Gen1Collections - before.Gen1Collections;
        var gen2Delta = after.Gen2Collections - before.Gen2Collections;
        
        Log.Information("\n   📈 GC Activity Analysis:");
        Log.Information($"   • Gen 0 collections during test: {gen0Delta}");
        Log.Information($"   • Gen 1 collections during test: {gen1Delta}");
        Log.Information($"   • Gen 2 collections during test: {gen2Delta}");
        
        if (gen2Delta > 5)
        {
            Log.Information("   ⚠️ High Gen 2 collection activity - may indicate memory pressure");
        }
        else
        {
            Log.Information("   ✅ Normal GC activity");
        }
    }

    private async Task TestLargeObjectHeapAsync()
    {
        Log.Information("   Testing Large Object Heap behavior...");
        
        var memoryBefore = GC.GetTotalMemory(false);
        
        // Создаем объекты больше 85KB (попадают в LOH)
        var largeAudio = GenerateTestAudio(30.0, 440.0); // ~30 секунд аудио
        await _speechService.RecognizeAsync(largeAudio);
        
        var memoryAfter = GC.GetTotalMemory(false);
        var memoryDelta = (memoryAfter - memoryBefore) / 1024.0 / 1024.0;
        
        Log.Information($"   • Large object memory delta: {memoryDelta:F1} MB");
    }

    private async Task TestRapidRecognitionAsync()
    {
        var testAudio = GenerateTestAudio(1.0, 440.0);
        for (int i = 0; i < 20; i++)
        {
            await _speechService.RecognizeAsync(testAudio);
        }
    }

    private async Task TestLargeAudioFilesAsync()
    {
        var largeAudio = GenerateTestAudio(25.0, 440.0); // 25 секунд
        await _speechService.RecognizeAsync(largeAudio);
    }

    private async Task TestConcurrentMemoryUsageAsync()
    {
        var testAudio = GenerateTestAudio(3.0, 440.0);
        var tasks = new List<Task>();
        
        for (int i = 0; i < 4; i++)
        {
            tasks.Add(_speechService.RecognizeAsync(testAudio));
        }
        
        await Task.WhenAll(tasks);
    }

    private async Task CreateMemoryPressureAsync()
    {
        // Создаем множество объектов для нагрузки на GC
        var objects = new List<byte[]>();
        
        for (int i = 0; i < 100; i++)
        {
            objects.Add(GenerateTestAudio(1.0, 440.0 + i));
        }
        
        // Используем объекты
        foreach (var obj in objects)
        {
            await _speechService.RecognizeAsync(obj);
        }
    }

    private async Task InitializeEngineAsync()
    {
        if (!_speechService.IsInitialized)
        {
            var config = CreateTestConfig();
            await _speechService.InitializeAsync(config);
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

    #endregion
}

#region Data Classes

public class MemorySnapshot
{
    public string Label { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double WorkingSetMB { get; set; }
    public double ManagedMemoryMB { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }

    public override string ToString()
    {
        return $"{Label}: Working Set: {WorkingSetMB:F1}MB, Managed: {ManagedMemoryMB:F1}MB";
    }
}

public class GCInfo
{
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public long TotalMemory { get; set; }
    public int MaxGeneration { get; set; }
}

#endregion