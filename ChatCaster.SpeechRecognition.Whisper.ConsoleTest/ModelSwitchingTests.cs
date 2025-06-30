using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.SpeechRecognition.Whisper.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ChatCaster.SpeechRecognition.Whisper.ConsoleTest;

/// <summary>
/// Тесты переключения между моделями Whisper для выявления утечек памяти и крашей
/// </summary>
public class ModelSwitchingTests
{
    private readonly ILogger<ModelSwitchingTests> _logger;
    private readonly ISpeechRecognitionService _speechService;
    private readonly WhisperModelManager _modelManager;
    private readonly TestReportGenerator _reportGenerator;

    public ModelSwitchingTests(
        ILogger<ModelSwitchingTests> logger,
        ISpeechRecognitionService speechService,
        WhisperModelManager modelManager,
        TestReportGenerator reportGenerator)
    {
        _logger = logger;
        _speechService = speechService;
        _modelManager = modelManager;
        _reportGenerator = reportGenerator;
    }

    /// <summary>
    /// Тестирует переключение между разными моделями
    /// </summary>
    public async Task TestModelSwitchingAsync()
    {
        Console.WriteLine("\n🔄 Testing model switching...");
        
        var testScenario = new[]
        {
            WhisperConstants.ModelSizes.Tiny,
            WhisperConstants.ModelSizes.Base,
            WhisperConstants.ModelSizes.Small,
            WhisperConstants.ModelSizes.Tiny,   // Возврат к tiny
            WhisperConstants.ModelSizes.Base,   // Повторное использование base
            WhisperConstants.ModelSizes.Tiny    // Финальный возврат
        };

        var results = new List<ModelSwitchResult>();
        var testAudio = GenerateTestAudio(3.0, 440.0);

        for (int i = 0; i < testScenario.Length; i++)
        {
            var modelSize = testScenario[i];
            var stepName = $"Step {i + 1}: Switch to {modelSize}";
            
            Console.WriteLine($"   {stepName}...");
            
            try
            {
                var result = await PerformModelSwitchAsync(modelSize, testAudio, stepName);
                results.Add(result);
                
                Console.WriteLine($"   ✅ {stepName} completed");
                Console.WriteLine($"      Working Set: {result.MemoryBeforeMB:F0}MB → {result.MemoryAfterRecognitionMB:F0}MB → {result.MemoryAfterGCMB:F0}MB");
                Console.WriteLine($"      Managed: {result.ManagedMemoryBeforeMB:F1}MB → {result.ManagedMemoryAfterGCMB:F1}MB");
                Console.WriteLine($"      Time: Init {result.InitTimeMs}ms, Recognition {result.RecognitionTimeMs}ms");
                Console.WriteLine($"      Recognition: {(result.RecognitionSuccess ? "✅" : "❌")}");
                
                // Пауза между переключениями
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ {stepName} FAILED: {ex.Message}");
                _logger.LogError(ex, "Model switching failed at step {Step}", stepName);
                
                // Записываем краш в результаты
                results.Add(new ModelSwitchResult
                {
                    StepName = stepName,
                    ModelSize = modelSize,
                    Success = false,
                    ErrorMessage = ex.Message
                });
                
                // Прерываем тест при краше
                break;
            }
        }

        // Анализируем результаты
        AnalyzeModelSwitchingResults(results);
        
        // Добавляем в отчет
        _reportGenerator.AddMemoryTable(results);
        _reportGenerator.AddPerformanceTable(results);
        _reportGenerator.AddCrashAnalysis(results);
    }

    /// <summary>
    /// Тестирует быстрое переключение моделей (стресс-тест)
    /// </summary>
    public async Task TestRapidModelSwitchingAsync()
    {
        Console.WriteLine("\n⚡ Testing rapid model switching...");
        
        var models = new[] { WhisperConstants.ModelSizes.Tiny, WhisperConstants.ModelSizes.Base };
        var testAudio = GenerateTestAudio(2.0, 440.0);
        var iterations = 5;
        
        var memorySnapshots = new List<ModelSwitchResult>();

        for (int i = 0; i < iterations; i++)
        {
            var modelSize = models[i % models.Length];
            var stepName = $"Rapid {i + 1}: {modelSize}";
            
            Console.WriteLine($"   {stepName}...");
            
            try
            {
                var result = await PerformModelSwitchAsync(modelSize, testAudio, stepName, forceReload: true);
                memorySnapshots.Add(result);
                
                Console.WriteLine($"   ✅ {result.MemoryAfterGCMB:F1}MB");
                
                // Минимальная пауза
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ {stepName} crashed: {ex.Message}");
                break;
            }
        }

        AnalyzeRapidSwitchingTrend(memorySnapshots);
    }

    /// <summary>
    /// Тестирует переключение на большие модели с последующим возвратом
    /// </summary>
    public async Task TestLargeModelSwitchingAsync()
    {
        Console.WriteLine("\n🐘 Testing large model switching...");
        _reportGenerator.StartDiagnosticSection("LARGE MODEL SWITCHING");

        // Сценарий аналогичный вашей таблице
        var scenario = new[]
        {
            new { Model = WhisperConstants.ModelSizes.Tiny, Description = "Baseline (Tiny)" },
            new { Model = WhisperConstants.ModelSizes.Small, Description = "Upgrade to Small" },
            new { Model = WhisperConstants.ModelSizes.Base, Description = "Switch to Base" },
            new { Model = WhisperConstants.ModelSizes.Tiny, Description = "Return to Tiny" },
            new { Model = WhisperConstants.ModelSizes.Medium, Description = "Large upgrade (Medium)" },
            new { Model = WhisperConstants.ModelSizes.Tiny, Description = "Return to Tiny (Critical)" }
        };

        // ДИАГНОСТИКА: Логируем сценарий
        _reportGenerator.AddDiagnosticLog("SCENARIO", $"Total steps planned: {scenario.Length}");
        foreach (var step in scenario.Select((s, i) => new { Step = s, Index = i }))
        {
            _reportGenerator.AddDiagnosticLog("SCENARIO", $"Step {step.Index + 1}: {step.Step.Model} - {step.Step.Description}");
        }

        // ДИАГНОСТИКА: Проверяем константы
        _reportGenerator.AddDiagnosticLog("CONSTANTS", $"Medium constant value: '{WhisperConstants.ModelSizes.Medium}'");
        _reportGenerator.AddDiagnosticLog("CONSTANTS", $"All available models: {string.Join(", ", WhisperConstants.ModelSizes.All)}");

        var testAudio = GenerateTestAudio(5.0, 440.0);
        var results = new List<ModelSwitchResult>();

        for (int i = 0; i < scenario.Length; i++)
        {
            var step = scenario[i];
            Console.WriteLine($"   {i + 1}. {step.Description}...");
            
            try
            {
                // ДИАГНОСТИКА: Критические шаги
                if (step.Model == WhisperConstants.ModelSizes.Medium)
                {
                    _reportGenerator.AddDiagnosticLog("CRITICAL", $"About to load MEDIUM model. Memory before: {GetWorkingSetMB():F1}MB");
                }
                else if (i == 5 && step.Model == WhisperConstants.ModelSizes.Tiny)
                {
                    _reportGenerator.AddDiagnosticLog("CRITICAL", $"About to switch FROM MEDIUM TO TINY. Memory before: {GetWorkingSetMB():F1}MB");
                }

                var result = await PerformModelSwitchAsync(step.Model, testAudio, step.Description);
                results.Add(result);
                
                // Показываем детальную информацию для критических шагов
                if (step.Description.Contains("Return") || step.Description.Contains("Critical"))
                {
                    ShowDetailedMemoryInfo(result);
                }
                else
                {
                    Console.WriteLine($"      Memory after: {result.MemoryAfterGCMB:F1}MB");
                }
                
                // Показываем детальную информацию для критических шагов
                if (step.Description.Contains("Return") || step.Description.Contains("Critical"))
                {
                    ShowDetailedMemoryInfo(result);
                }
                else
                {
                    Console.WriteLine($"      Memory after: {result.MemoryAfterGCMB:F1}MB");
                }

                
                await Task.Delay(2000); // Больше времени между большими моделями
            }
            catch (Exception ex)
            {
                
                _reportGenerator.AddDiagnosticLog("CRASH", $"Step {i + 1} CRASHED: {ex.GetType().Name}: {ex.Message}");
                _reportGenerator.AddDiagnosticLog("CRASH", $"Stack trace: {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");

                Console.WriteLine($"   ❌ CRASH at step {i + 1}: {ex.Message}");
                Console.WriteLine($"   🔍 This matches your reported crash scenario!");
                
                results.Add(new ModelSwitchResult
                {
                    StepName = step.Description,
                    ModelSize = step.Model,
                    Success = false,
                    ErrorMessage = ex.Message
                });
                
                break;
            }
        }
        _reportGenerator.AddDiagnosticLog("SUMMARY", $"Completed {results.Count}/{scenario.Length} steps");

        GenerateMemoryUsageTable(results);
    }

    #region Private Methods

    private async Task<ModelSwitchResult> PerformModelSwitchAsync(
        string modelSize, 
        byte[] testAudio, 
        string stepName,
        bool forceReload = false)
    {
        _reportGenerator.AddDiagnosticLog("MODEL_SWITCH", $"Starting switch to {modelSize} (forceReload: {forceReload})");

        var result = new ModelSwitchResult
        {
            StepName = stepName,
            ModelSize = modelSize,
            Timestamp = DateTime.Now
        };

        // Замеряем память в состоянии покоя
        ForceGarbageCollection();
        result.MemoryBeforeMB = GetWorkingSetMB();
        result.ManagedMemoryBeforeMB = GetManagedMemoryMB();
        
        _reportGenerator.AddDiagnosticLog("MODEL_SWITCH", $"Memory before: {result.MemoryBeforeMB:F1}MB");

        // Подготавливаем новую конфигурацию
        var config = CreateConfigForModel(modelSize);
        _reportGenerator.AddDiagnosticLog("MODEL_SWITCH", $"Created config for {modelSize}");

        // Засекаем время инициализации
        var initStopwatch = Stopwatch.StartNew();
        
        if (forceReload || !_speechService.IsInitialized)
        {
            _reportGenerator.AddDiagnosticLog("MODEL_SWITCH", $"Calling InitializeAsync for {modelSize}");

            await _speechService.InitializeAsync(config);
        }
        else
        {
            _reportGenerator.AddDiagnosticLog("MODEL_SWITCH", $"Calling ReloadConfigAsync for {modelSize}");

            await _speechService.ReloadConfigAsync(config);
        }
        
        initStopwatch.Stop();
        result.InitTimeMs = initStopwatch.ElapsedMilliseconds;
        _reportGenerator.AddDiagnosticLog("MODEL_SWITCH", $"Initialization completed in {result.InitTimeMs}ms");

        // Замеряем память после инициализации
        result.MemoryAfterInitMB = GetWorkingSetMB();
        result.ManagedMemoryAfterInitMB = GetManagedMemoryMB();

        // Выполняем распознавание
        var recognitionStopwatch = Stopwatch.StartNew();
        var recognitionResult = await _speechService.RecognizeAsync(testAudio);
        recognitionStopwatch.Stop();
        
        result.RecognitionTimeMs = recognitionStopwatch.ElapsedMilliseconds;
        result.RecognitionSuccess = recognitionResult.Success;

        // Замеряем память после распознавания
        result.MemoryAfterRecognitionMB = GetWorkingSetMB();
        result.ManagedMemoryAfterRecognitionMB = GetManagedMemoryMB();

        // Принудительная сборка мусора
        ForceGarbageCollection();
        result.MemoryAfterGCMB = GetWorkingSetMB();
        result.ManagedMemoryAfterGCMB = GetManagedMemoryMB();

        result.Success = true;
        return result;
    }

    private void AnalyzeModelSwitchingResults(List<ModelSwitchResult> results)
    {
        Console.WriteLine("\n   📊 Model Switching Analysis:");
        
        if (results.Count == 0) return;

        var successfulResults = results.Where(r => r.Success).ToList();
        var crashes = results.Where(r => !r.Success).ToList();

        Console.WriteLine($"   • Successful switches: {successfulResults.Count}/{results.Count}");
        Console.WriteLine($"   • Crashes: {crashes.Count}");

        if (crashes.Any())
        {
            Console.WriteLine("   🚨 CRASH PATTERNS:");
            foreach (var crash in crashes)
            {
                Console.WriteLine($"     - {crash.StepName}: {crash.ErrorMessage}");
            }
        }

        if (successfulResults.Count > 1)
        {
            var firstMemory = successfulResults.First().MemoryAfterGCMB;
            var lastMemory = successfulResults.Last().MemoryAfterGCMB;
            var memoryGrowth = lastMemory - firstMemory;
            
            Console.WriteLine($"   • Memory growth: {firstMemory:F1}MB → {lastMemory:F1}MB ({memoryGrowth:F1}MB)");
            
            if (memoryGrowth > 50)
            {
                Console.WriteLine("   ⚠️ Significant memory growth detected - possible leaks");
            }
            else
            {
                Console.WriteLine("   ✅ Memory growth within acceptable limits");
            }
        }
    }

    private void AnalyzeRapidSwitchingTrend(List<ModelSwitchResult> snapshots)
    {
        if (snapshots.Count < 3) return;

        Console.WriteLine("\n   📈 Rapid Switching Trend:");
        var memories = snapshots.Select(s => s.MemoryAfterGCMB).ToArray();
        
        for (int i = 0; i < memories.Length; i++)
        {
            var trend = i > 0 ? (memories[i] - memories[i - 1]) : 0;
            var trendSymbol = trend > 5 ? "📈" : trend < -5 ? "📉" : "➡️";
            Console.WriteLine($"   {i + 1}. {memories[i]:F1}MB {trendSymbol} ({trend:+0.1;-0.1;±0.0}MB)");
        }

        var totalGrowth = memories.Last() - memories.First();
        Console.WriteLine($"   • Total growth: {totalGrowth:F1}MB");
        
        if (totalGrowth > 20)
        {
            Console.WriteLine("   ⚠️ Memory accumulation in rapid switching");
        }
    }

    private void ShowDetailedMemoryInfo(ModelSwitchResult result)
    {
        Console.WriteLine($"      📊 Detailed Memory Info:");
        Console.WriteLine($"         Before: {result.MemoryBeforeMB:F1}MB");
        Console.WriteLine($"         After init: {result.MemoryAfterInitMB:F1}MB (+{result.MemoryAfterInitMB - result.MemoryBeforeMB:F1}MB)");
        Console.WriteLine($"         After recognition: {result.MemoryAfterRecognitionMB:F1}MB (+{result.MemoryAfterRecognitionMB - result.MemoryAfterInitMB:F1}MB)");
        Console.WriteLine($"         After GC: {result.MemoryAfterGCMB:F1}MB (-{result.MemoryAfterRecognitionMB - result.MemoryAfterGCMB:F1}MB freed)");
    }

    private void GenerateMemoryUsageTable(List<ModelSwitchResult> results)
    {
        Console.WriteLine("\n   📋 Memory Usage Table (similar to your findings):");
        Console.WriteLine("   ┌─────────────────────────┬────────┬─────────────┬──────────────────┬─────────────────────┬────────┐");
        Console.WriteLine("   │ Action                  │ Model  │ Idle (MB)   │ Recognition (MB) │ After Operation (MB)│ Status │");
        Console.WriteLine("   ├─────────────────────────┼────────┼─────────────┼──────────────────┼─────────────────────┼────────┤");
        
        foreach (var result in results)
        {
            var status = result.Success ? "✅" : "❌";
            var action = result.StepName.Length > 23 ? result.StepName.Substring(0, 20) + "..." : result.StepName;
            
            if (result.Success)
            {
                Console.WriteLine($"   │ {action,-23} │ {result.ModelSize,-6} │ {result.MemoryBeforeMB,11:F1} │ {result.MemoryAfterRecognitionMB,16:F1} │ {result.MemoryAfterGCMB,19:F1} │ {status,6} │");
            }
            else
            {
                Console.WriteLine($"   │ {action,-23} │ {result.ModelSize,-6} │ {"CRASH",-11} │ {"CRASH",-16} │ {"CRASH",-19} │ {status,6} │");
            }
        }
        
        Console.WriteLine("   └─────────────────────────┴────────┴─────────────┴──────────────────┴─────────────────────┴────────┘");
    }

    private double GetManagedMemoryMB()
    {
        return GC.GetTotalMemory(false) / 1024.0 / 1024.0;
    }

    private double GetWorkingSetMB()
    {
        var process = Process.GetCurrentProcess();
        return process.WorkingSet64 / 1024.0 / 1024.0;
    }

    private void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private SpeechRecognitionConfig CreateConfigForModel(string modelSize)
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

public class ModelSwitchResult
{
    public string StepName { get; set; } = string.Empty;
    public string ModelSize { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Working Set memory (real process memory)
    public double MemoryBeforeMB { get; set; }
    public double MemoryAfterInitMB { get; set; }
    public double MemoryAfterRecognitionMB { get; set; }
    public double MemoryAfterGCMB { get; set; }
    
    // Managed memory (C# objects only)
    public double ManagedMemoryBeforeMB { get; set; }
    public double ManagedMemoryAfterInitMB { get; set; }
    public double ManagedMemoryAfterRecognitionMB { get; set; }
    public double ManagedMemoryAfterGCMB { get; set; }
    
    // Performance measurements
    public long InitTimeMs { get; set; }
    public long RecognitionTimeMs { get; set; }
    public bool RecognitionSuccess { get; set; }
}

#endregion