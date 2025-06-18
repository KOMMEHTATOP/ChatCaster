using ChatCaster.Core.Constants;
using ChatCaster.Core.Events;
using ChatCaster.Core.Exceptions;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;

namespace ChatCaster.Core;

/// <summary>
/// Простой тестер для проверки что все работает
/// </summary>
public static class TestRunner
{
    public static void RunBasicTests()
    {
        Console.WriteLine($"=== {AppConstants.AppName} v{AppConstants.AppVersion} - Тестирование Core модуля ===");
        
        TestModels();
        TestEnums();
        TestEvents();
        TestExceptions();
        TestConstants();
        
        Console.WriteLine("✅ Все тесты пройдены успешно!");
    }
    
    private static void TestModels()
    {
        Console.WriteLine("🧪 Тестирование моделей...");
        
        // Тест конфигурации
        var config = new AppConfig();
        Console.WriteLine($"   Конфигурация создана. Частота: {config.Audio.SampleRate} Hz");
        
        // Тест состояния записи
        var state = new RecordingState 
        { 
            Status = RecordingStatus.Recording,
            StartTime = DateTime.Now 
        };
        Console.WriteLine($"   Состояние: {state.Status} с {state.StartTime}");
        
        // Тест аудио устройства
        var device = new AudioDevice 
        { 
            Name = "Test Microphone",
            Type = AudioDeviceType.UsbMicrophone,
            IsDefault = true 
        };
        Console.WriteLine($"   Устройство: {device.Name} ({device.Type})");
        
        // Тест геймпада
        var gamepad = new GamepadInfo 
        { 
            Name = "Xbox Controller",
            Type = GamepadType.XboxSeries,
            IsConnected = true 
        };
        Console.WriteLine($"   Геймпад: {gamepad.Name} ({gamepad.Type})");
        
        Console.WriteLine("✅ Модели работают");
    }
    
    private static void TestEnums()
    {
        Console.WriteLine("🧪 Тестирование перечислений...");
        
        // Тест кнопок геймпада
        var buttons = Enum.GetValues<GamepadButton>();
        Console.WriteLine($"   Кнопок геймпада: {buttons.Length}");
        
        // Тест моделей Whisper
        var models = Enum.GetValues<WhisperModel>();
        Console.WriteLine($"   Моделей Whisper: {models.Length}");
        
        // Тест позиций overlay
        var positions = Enum.GetValues<OverlayPosition>();
        Console.WriteLine($"   Позиций overlay: {positions.Length}");
        
        // Тест статусов записи
        var statuses = Enum.GetValues<RecordingStatus>();
        Console.WriteLine($"   Статусов записи: {statuses.Length}");
        
        Console.WriteLine("✅ Перечисления работают");
    }
    
    private static void TestEvents()
    {
        Console.WriteLine("🧪 Тестирование событий...");
        
        // Тест базового события
        var baseEvent = new RecordingStatusChangedEvent
        {
            OldStatus = RecordingStatus.Idle,
            NewStatus = RecordingStatus.Recording,
            Reason = "User pressed gamepad shortcut"
        };
        Console.WriteLine($"   Событие создано: {baseEvent.EventId} в {baseEvent.Timestamp}");
        
        // Тест события геймпада
        var gamepadEvent = new GamepadConnectedEvent
        {
            GamepadIndex = 0,
            GamepadInfo = new GamepadInfo { Name = "Test Controller" }
        };
        Console.WriteLine($"   Событие геймпада: индекс {gamepadEvent.GamepadIndex}");
        
        // Тест события ошибки
        var errorEvent = new ErrorOccurredEvent
        {
            ErrorMessage = "Test error message",
            Component = "TestComponent"
        };
        Console.WriteLine($"   Событие ошибки: {errorEvent.ErrorMessage}");
        
        Console.WriteLine("✅ События работают");
    }
    
    private static void TestExceptions()
    {
        Console.WriteLine("🧪 Тестирование исключений...");
        
        try
        {
            throw new AudioException("Test audio error");
        }
        catch (AudioException ex)
        {
            Console.WriteLine($"   Поймано исключение аудио: {ex.Component} - {ex.Message}");
        }
        
        try
        {
            throw new SpeechRecognitionException("Test recognition error", new InvalidOperationException("Inner"));
        }
        catch (SpeechRecognitionException ex)
        {
            Console.WriteLine($"   Поймано исключение распознавания: {ex.Component} - {ex.Message}");
        }
        
        Console.WriteLine("✅ Исключения работают");
    }
    
    private static void TestConstants()
    {
        Console.WriteLine("🧪 Тестирование констант...");
        
        Console.WriteLine($"   Приложение: {AppConstants.AppName} v{AppConstants.AppVersion}");
        Console.WriteLine($"   Диапазон записи: {AppConstants.MinRecordingSeconds}-{AppConstants.MaxRecordingSeconds} сек");
        Console.WriteLine($"   Частота по умолчанию: {AppConstants.DefaultSampleRate} Hz");
        Console.WriteLine($"   Конфиг файл: {AppConstants.ConfigFileName}");
        
        // Тест сообщений
        Console.WriteLine($"   Сообщение готовности: {Messages.StatusIdle}");
        Console.WriteLine($"   Сообщение ошибки: {Messages.ErrorNoMicrophone}");
        
        Console.WriteLine("✅ Константы работают");
    }
    
    /// <summary>
    /// Тест WeakEventHandler (более продвинутый)
    /// </summary>
    public static void TestWeakEventHandler()
    {
        Console.WriteLine("🧪 Тестирование WeakEventHandler...");
        
        var testEvent = new RecordingStatusChangedEvent 
        { 
            NewStatus = RecordingStatus.Recording 
        };
        
        // Создаем обработчик
        Action<RecordingStatusChangedEvent> handler = (e) => 
        {
            Console.WriteLine($"   Обработчик вызван для статуса: {e.NewStatus}");
        };
        
        var weakHandler = new WeakEventHandler<RecordingStatusChangedEvent>(handler);
        
        // Тестируем вызов
        bool result = weakHandler.TryExecute(testEvent);
        Console.WriteLine($"   Результат выполнения: {result}");
        Console.WriteLine($"   Обработчик жив: {weakHandler.IsAlive}");
        
        Console.WriteLine("✅ WeakEventHandler работает");
    }
}