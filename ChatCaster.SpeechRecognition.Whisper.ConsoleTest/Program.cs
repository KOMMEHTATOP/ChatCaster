using ChatCaster.SpeechRecognition.Whisper.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatCaster.SpeechRecognition.Whisper.ConsoleTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== ChatCaster Whisper Module Test ===\n");

        // Создаем хост с DI и логированием
        var host = CreateHost();
        
        try
        {
            // Запускаем тесты
            var testRunner = host.Services.GetRequiredService<TestRunner>();
            await testRunner.RunAllTestsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Critical error: {ex.Message}");
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Регистрируем Whisper как ISpeechRecognitionService для разработки
                services.AddWhisperAsSpeechRecognition(config => {
                    config.ModelSize = "tiny";           // Быстрая модель для разработки
                    config.ThreadCount = 2;              // Меньше нагрузки на CPU
                    config.EnableGpu = false;            // Отключаем GPU для совместимости
                    config.RecognitionTimeoutSeconds = 30; // Короткий таймаут
                    config.UseVAD = true;                // Включаем VAD для лучшей отзывчивости
                });
                
                // Регистрируем тестовые сервисы
                services.AddSingleton<TestRunner>();
                services.AddSingleton<PerformanceTests>();
                services.AddSingleton<MemoryTests>();
                services.AddSingleton<FunctionalTests>();
                services.AddSingleton<ModelSwitchingTests>();
                services.AddSingleton<TestReportGenerator>();
                
                // Настраиваем логирование (только ошибки и предупреждения)
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Debug); // Убираем Debug и Info
                });
            })
            .Build();
    }
}

/// <summary>
/// Главный класс для запуска всех тестов
/// </summary>
public class TestRunner
{
    private readonly ILogger<TestRunner> _logger;
    private readonly PerformanceTests _performanceTests;
    private readonly MemoryTests _memoryTests;
    private readonly FunctionalTests _functionalTests;
    private readonly ModelSwitchingTests _modelSwitchingTests;
    private readonly TestReportGenerator _reportGenerator;

    public TestRunner(
        ILogger<TestRunner> logger,
        PerformanceTests performanceTests,
        MemoryTests memoryTests,
        FunctionalTests functionalTests,
        ModelSwitchingTests modelSwitchingTests,
        TestReportGenerator reportGenerator)
    {
        _logger = logger;
        _performanceTests = performanceTests;
        _memoryTests = memoryTests;
        _functionalTests = functionalTests;
        _modelSwitchingTests = modelSwitchingTests;
        _reportGenerator = reportGenerator;
    }

    public async Task RunAllTestsAsync()
    {
        Console.WriteLine("🚀 Starting Whisper module tests...\n");

        var overallStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 1. Функциональные тесты
            await RunTestSection("🔧 Functional Tests", async () =>
            {
                await _functionalTests.TestModelDownloadAsync();
                await _functionalTests.TestAudioConversionAsync();
                await _functionalTests.TestSpeechRecognitionAsync();
            });

            // 2. Тесты производительности
            await RunTestSection("⚡ Performance Tests", async () =>
            {
                await _performanceTests.TestModelLoadingPerformanceAsync();
                await _performanceTests.TestRecognitionSpeedAsync();
                await _performanceTests.TestConcurrentRecognitionAsync();
            });

            // 3. Тесты переключения моделей
            await RunTestSection("🔄 Model Switching Tests", async () =>
            {
                await _modelSwitchingTests.TestModelSwitchingAsync();
                await _modelSwitchingTests.TestRapidModelSwitchingAsync();
                await _modelSwitchingTests.TestLargeModelSwitchingAsync();
            });

            // 4. Тесты памяти
            await RunTestSection("💾 Memory Tests", async () =>
            {
                await _memoryTests.TestMemoryUsageAsync();
                await _memoryTests.TestMemoryLeaksAsync();
                await _memoryTests.TestGarbageCollectionAsync();
            });

            // 5. Сводка
            await ShowSummaryAsync(overallStopwatch.Elapsed);
            
            // 6. Генерируем финальный отчет
            _reportGenerator.GenerateReport();
            _reportGenerator.SaveToFile();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test execution failed");
            Console.WriteLine($"❌ Test suite failed: {ex.Message}");
            throw;
        }
    }

    private async Task RunTestSection(string sectionName, Func<Task> testAction)
    {
        Console.WriteLine($"\n{sectionName}");
        Console.WriteLine(new string('=', sectionName.Length));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            await testAction();
            Console.WriteLine($"✅ {sectionName} completed in {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ {sectionName} failed: {ex.Message}");
            _logger.LogError(ex, "Test section failed: {SectionName}", sectionName);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task ShowSummaryAsync(TimeSpan totalTime)
    {
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("📊 TEST SUMMARY");
        Console.WriteLine(new string('=', 50));
        
        Console.WriteLine($"Total execution time: {totalTime.TotalSeconds:F2} seconds");
        
        // Показываем использование памяти
        var memoryBefore = GC.GetTotalMemory(false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memoryAfter = GC.GetTotalMemory(false);
        
        Console.WriteLine($"Memory before GC: {memoryBefore / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"Memory after GC: {memoryAfter / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"Memory difference: {(memoryBefore - memoryAfter) / 1024.0 / 1024.0:F2} MB");
        
        // Информация о сборщике мусора
        for (int i = 0; i < GC.MaxGeneration + 1; i++)
        {
            Console.WriteLine($"Gen {i} collections: {GC.CollectionCount(i)}");
        }

        Console.WriteLine("\n✅ All tests completed successfully!");
        
        await Task.CompletedTask;
    }
}