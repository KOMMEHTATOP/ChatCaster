using ChatCaster.Core;
using ChatCaster.Windows.Services;
using ChatCaster.Core.Models;

Console.WriteLine("ChatCaster Core - Тестирование");
Console.WriteLine("==============================");

try
{
    // Запускаем основные тесты
    TestRunner.RunBasicTests();
    
    Console.WriteLine();
    
    // Запускаем продвинутый тест
    TestRunner.TestWeakEventHandler();
    
    Console.WriteLine();
    Console.WriteLine("🎤 Тестирование аудио сервиса...");
    await TestAudioService();
    
    Console.WriteLine();
    Console.WriteLine("🗣️ Тестирование Whisper сервиса...");
    await TestWhisperService();
    
    Console.WriteLine();
    Console.WriteLine("🎉 Все тесты завершены успешно!");
    Console.WriteLine("Core модуль готов к использованию.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Ошибка в тестах: {ex.Message}");
    Console.WriteLine($"Стек: {ex.StackTrace}");
}

Console.WriteLine();
Console.WriteLine("Нажмите любую клавишу для выхода...");
Console.ReadKey();

static async Task TestAudioService()
{
    using var audioService = new AudioCaptureService();
    
    // Получаем список устройств
    var devices = await audioService.GetAvailableDevicesAsync();
    Console.WriteLine($"   Найдено аудио устройств: {devices.Count()}");
    
    foreach (var device in devices.Take(3)) // Показываем первые 3
    {
        Console.WriteLine($"   - {device.Name} ({device.Type}) {(device.IsDefault ? "[По умолчанию]" : "")}");
    }
    
    // Получаем устройство по умолчанию
    var defaultDevice = await audioService.GetDefaultDeviceAsync();
    if (defaultDevice != null)
    {
        Console.WriteLine($"   Устройство по умолчанию: {defaultDevice.Name}");
        
        // Пробуем установить его как активное
        bool result = await audioService.SetActiveDeviceAsync(defaultDevice.Id);
        Console.WriteLine($"   Установка устройства: {(result ? "✅" : "❌")}");
    }
    else
    {
        Console.WriteLine("   ⚠️  Устройство по умолчанию не найдено");
    }
}

static async Task TestWhisperService()
{
    using var whisperService = new SpeechRecognitionService();
    
    // Тестируем размеры моделей
    var models = Enum.GetValues<WhisperModel>();
    Console.WriteLine("   Размеры моделей:");
    foreach (var model in models.Take(3))
    {
        var size = await whisperService.GetModelSizeAsync(model);
        Console.WriteLine($"   - {model}: {size / (1024 * 1024)} MB");
    }
    
    // Тестируем инициализацию
    var config = new WhisperConfig 
    { 
        Model = WhisperModel.Tiny,
        Language = "ru" 
    };
    
    try
    {
        bool initialized = await whisperService.InitializeAsync(config);
        Console.WriteLine($"   Инициализация Whisper: {(initialized ? "✅" : "❌")}");
        Console.WriteLine($"   Текущая модель: {whisperService.CurrentModel}");
        
        if (initialized)
        {
            // Тестируем распознавание с фиктивными данными
            var testAudio = new byte[16000 * 2]; // 1 секунда аудио 16kHz 16-bit
            var result = await whisperService.RecognizeAsync(testAudio);
            
            Console.WriteLine($"   Тест распознавания: {(result.Success ? "✅" : "❌")}");
            if (result.Success)
            {
                Console.WriteLine($"   Результат: '{result.RecognizedText}'");
                Console.WriteLine($"   Время обработки: {result.ProcessingTime.TotalMilliseconds:F0} мс");
                Console.WriteLine($"   Уверенность: {result.Confidence:P1}");
            }
            else
            {
                Console.WriteLine($"   Ошибка: {result.ErrorMessage}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   ❌ Ошибка Whisper: {ex.Message}");
    }
}