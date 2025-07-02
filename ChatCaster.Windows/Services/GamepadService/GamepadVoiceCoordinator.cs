using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.Windows.Services;

namespace ChatCaster.Windows.Services.GamepadService;

/// <summary>
/// Координатор между геймпадом и записью голоса
/// Обрабатывает нажатия геймпада и управляет записью
/// </summary>
public class GamepadVoiceCoordinator : IDisposable
{
    // ✅ ИЗМЕНЕНО: используем интерфейс вместо конкретного класса
    private readonly IGamepadService _gamepadService;
    private readonly IVoiceRecordingService _voiceService;
    private readonly ISystemIntegrationService _systemService;
    private readonly IConfigurationService _configService;
    private readonly ITrayService _trayService; // ✅ ИСПРАВЛЕНО: получаем из DI

    private readonly object _lockObject = new();
    private bool _isDisposed = false;
    private bool _isInitialized = false;

    // Режимы работы
    public enum VoiceActivationMode
    {
        Toggle, // Одно нажатие старт, второе стоп
        PushToTalk // Держать для записи (будущая функция)
    }

    private VoiceActivationMode _activationMode = VoiceActivationMode.Toggle;

    // ✅ ИСПРАВЛЕНО: Добавляем ITrayService в конструктор
    public GamepadVoiceCoordinator(
        IGamepadService gamepadService,
        IVoiceRecordingService voiceService,
        ISystemIntegrationService systemService,
        IConfigurationService configService,
        ITrayService trayService) // ✅ НОВЫЙ ПАРАМЕТР
    {
        _gamepadService = gamepadService ?? throw new ArgumentNullException(nameof(gamepadService));
        _voiceService = voiceService ?? throw new ArgumentNullException(nameof(voiceService));
        _systemService = systemService ?? throw new ArgumentNullException(nameof(systemService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService)); // ✅ НОВОЕ

        Console.WriteLine("🎮 [GamepadVoiceCoordinator] Создан с ITrayService из DI");
    }

    /// <summary>
    /// Текущий режим активации голоса
    /// </summary>
    public VoiceActivationMode ActivationMode
    {
        get
        {
            lock (_lockObject)
            {
                return _activationMode;
            }
        }
        set
        {
            lock (_lockObject)
            {
                _activationMode = value;
                Console.WriteLine($"[GamepadVoiceCoordinator] Режим изменен на: {value}");
            }
        }
    }

    /// <summary>
    /// Статус инициализации
    /// </summary>
    public bool IsInitialized
    {
        get
        {
            lock (_lockObject)
            {
                return _isInitialized;
            }
        }
    }

    /// <summary>
    /// Статус активности геймпада
    /// </summary>
    public bool IsGamepadActive => _gamepadService.IsMonitoring;

    /// <summary>
    /// Инициализация координатора
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        Console.WriteLine("🎮 [GamepadVoiceCoordinator] InitializeAsync начат");

        try
        {
            lock (_lockObject)
            {
                if (_isInitialized)
                {
                    Console.WriteLine("🎮 [GamepadVoiceCoordinator] Уже инициализирован");
                    return true;
                }

                Console.WriteLine("🎮 [GamepadVoiceCoordinator] Подписываемся на события геймпада...");
                // Подписываемся на события геймпада
                _gamepadService.GamepadConnected += OnGamepadConnected;
                _gamepadService.GamepadDisconnected += OnGamepadDisconnected;
                _gamepadService.ShortcutPressed += OnGamepadShortcutPressed;

                Console.WriteLine("🎮 [GamepadVoiceCoordinator] Подписываемся на события записи...");
                // Подписываемся на события записи для логирования
                _voiceService.StatusChanged += OnVoiceRecordingStatusChanged;
                _voiceService.RecognitionCompleted += OnVoiceRecognitionCompleted;
            }

            Console.WriteLine("🎮 [GamepadVoiceCoordinator] Загружаем конфигурацию...");
            // Запускаем мониторинг геймпада с настройками из конфигурации
            var config = await _configService.LoadConfigAsync();
            Console.WriteLine(
                $"🎮 [GamepadVoiceCoordinator] Конфигурация загружена. EnableGamepadControl: {config.Input.EnableGamepadControl}");

            if (!config.Input.EnableGamepadControl)
            {
                Console.WriteLine("⚠️ [GamepadVoiceCoordinator] Управление геймпадом отключено в настройках");

                lock (_lockObject)
                {
                    _isInitialized = true; // Помечаем как инициализированный, но не запускаем
                }

                return true;
            }

            if (config.Input.GamepadShortcut == null)
            {
                Console.WriteLine("❌ [GamepadVoiceCoordinator] GamepadShortcut не настроен в конфигурации");
                return false;
            }

            Console.WriteLine(
                $"🎮 [GamepadVoiceCoordinator] Настройка геймпада: {config.Input.GamepadShortcut.PrimaryButton} + {config.Input.GamepadShortcut.SecondaryButton}");

            Console.WriteLine("🎮 [GamepadVoiceCoordinator] Запускаем мониторинг геймпада...");
            await _gamepadService.StartMonitoringAsync(config.Input.GamepadShortcut);
            Console.WriteLine("✅ [GamepadVoiceCoordinator] Мониторинг геймпада запущен");

            lock (_lockObject)
            {
                _isInitialized = true;
            }

            Console.WriteLine("✅ [GamepadVoiceCoordinator] Инициализация завершена успешно");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [GamepadVoiceCoordinator] Ошибка инициализации: {ex.Message}");
            _trayService.ShowNotification("Ошибка геймпада", "Не удалось инициализировать геймпад", NotificationType.Error);
            return false;
        }
    }
    
    /// <summary>
    /// Остановка координатора
    /// </summary>
    public async Task ShutdownAsync()
    {
        try
        {
            lock (_lockObject)
            {
                if (!_isInitialized)
                    return;

                // Отписываемся от событий
                _gamepadService.GamepadConnected -= OnGamepadConnected;
                _gamepadService.GamepadDisconnected -= OnGamepadDisconnected;
                _gamepadService.ShortcutPressed -= OnGamepadShortcutPressed;
                _voiceService.StatusChanged -= OnVoiceRecordingStatusChanged;
                _voiceService.RecognitionCompleted -= OnVoiceRecognitionCompleted;

                _isInitialized = false;
            }

            // Останавливаем мониторинг геймпада
            await _gamepadService.StopMonitoringAsync();

            // Останавливаем запись если идет
            if (_voiceService.IsRecording)
            {
                await _voiceService.CancelRecordingAsync();
            }

            Console.WriteLine("[GamepadVoiceCoordinator] Остановка завершена");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GamepadVoiceCoordinator] Ошибка остановки: {ex.Message}");
        }
    }

    /// <summary>
    /// Обновление настроек геймпада
    /// </summary>
    public async Task UpdateGamepadSettingsAsync(GamepadShortcut newShortcut)
    {
        try
        {
            if (!_isInitialized)
                return;

            // Перезапускаем мониторинг с новыми настройками
            await _gamepadService.StopMonitoringAsync();
            await _gamepadService.StartMonitoringAsync(newShortcut);

            Console.WriteLine($"[GamepadVoiceCoordinator] Настройки геймпада обновлены: {newShortcut.DisplayText}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GamepadVoiceCoordinator] Ошибка обновления настроек: {ex.Message}");
        }
    }

    /// <summary>
    /// Проверка доступности геймпада
    /// </summary>
    public async Task<bool> TestGamepadAsync()
    {
        try
        {
            return await _gamepadService.TestConnectionAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GamepadVoiceCoordinator] Ошибка тестирования геймпада: {ex.Message}");
            return false;
        }
    }

    #region Event Handlers

    /// <summary>
    /// Обработчик подключения геймпада
    /// </summary>
    private void OnGamepadConnected(object? sender, GamepadConnectedEvent e)
    {
        Console.WriteLine($"[GamepadVoiceCoordinator] 🎮 Геймпад подключен: {e.GamepadInfo.Name}");
        _trayService.ShowNotification("Геймпад", $"Подключен: {e.GamepadInfo.Name}", NotificationType.Success);
    }

    /// <summary>
    /// Обработчик отключения геймпада
    /// </summary>
    private void OnGamepadDisconnected(object? sender, GamepadDisconnectedEvent e)
    {
        Console.WriteLine($"[GamepadVoiceCoordinator] 🎮 Геймпад отключен из слота {e.GamepadIndex}");
        _trayService.ShowNotification("Геймпад", "Геймпад отключен", NotificationType.Warning);

        // Останавливаем запись если она идет
        Task.Run(async () =>
        {
            try
            {
                if (_voiceService.IsRecording)
                {
                    await _voiceService.CancelRecordingAsync();
                    Console.WriteLine("[GamepadVoiceCoordinator] Запись отменена из-за отключения геймпада");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GamepadVoiceCoordinator] Ошибка отмены записи: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Главный обработчик нажатия комбинации геймпада
    /// </summary>
    private void OnGamepadShortcutPressed(object? sender, GamepadShortcutPressedEvent e)
    {
        Console.WriteLine($"[GamepadVoiceCoordinator] 🎯 Комбинация сработала: {e.Shortcut.DisplayText} ({e.HoldTimeMs}ms)");

        // Обрабатываем нажатие в фоновом потоке
        Task.Run(async () =>
        {
            try
            {
                await HandleShortcutPressed(e);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GamepadVoiceCoordinator] Ошибка обработки комбинации: {ex.Message}");
                _trayService.ShowNotification("Ошибка", "Ошибка обработки геймпада", NotificationType.Error);
            }
        });
    }

    /// <summary>
    /// Обработка нажатия комбинации в зависимости от режима
    /// </summary>
    private async Task HandleShortcutPressed(GamepadShortcutPressedEvent e)
    {
        switch (_activationMode)
        {
            case VoiceActivationMode.Toggle:
                await HandleToggleMode();
                break;

            case VoiceActivationMode.PushToTalk:
                // Будущая функция - пока используем Toggle
                await HandleToggleMode();
                break;

            default:
                Console.WriteLine($"[GamepadVoiceCoordinator] Неизвестный режим: {_activationMode}");
                break;
        }
    }

    /// <summary>
    /// Обработка режима Toggle (переключение старт/стоп)
    /// </summary>
    private async Task HandleToggleMode()
    {
        if (_voiceService.IsRecording)
        {
            // Запись идет - останавливаем
            Console.WriteLine("[GamepadVoiceCoordinator] 🛑 Останавливаем запись по геймпаду");
            var result = await _voiceService.StopRecordingAsync();

            // Отправляем результат в систему если успешно
            if (result.Success && !string.IsNullOrEmpty(result.RecognizedText))
            {
                await _systemService.SendTextAsync(result.RecognizedText);
                Console.WriteLine($"[GamepadVoiceCoordinator] ✅ Текст отправлен: '{result.RecognizedText}'");
                _trayService.ShowNotification("Распознано", result.RecognizedText, NotificationType.Success);
            }
            else
            {
                Console.WriteLine($"[GamepadVoiceCoordinator] ❌ Ошибка распознавания: {result.ErrorMessage}");
                _trayService.ShowNotification("Ошибка", result.ErrorMessage ?? "Не удалось распознать речь", NotificationType.Error);
            }
        }
        else
        {
            // Запись не идет - запускаем
            Console.WriteLine("[GamepadVoiceCoordinator] 🎤 Запускаем запись по геймпаду");
            bool started = await _voiceService.StartRecordingAsync();

            if (started)
            {
                Console.WriteLine("[GamepadVoiceCoordinator] ✅ Запись началась");
                _trayService.ShowNotification("Запись", "Говорите...", NotificationType.Info);
            }
            else
            {
                Console.WriteLine("[GamepadVoiceCoordinator] ❌ Не удалось запустить запись");
                _trayService.ShowNotification("Ошибка", "Не удалось начать запись", NotificationType.Error);
            }
        }
    }

    /// <summary>
    /// Обработчик изменения статуса записи
    /// </summary>
    private void OnVoiceRecordingStatusChanged(object? sender, RecordingStatusChangedEvent e)
    {
        // Убрали консольные логи для статуса - они слишком частые
    }

    /// <summary>
    /// Обработчик завершения распознавания
    /// </summary>
    private void OnVoiceRecognitionCompleted(object? sender, VoiceRecognitionCompletedEvent e)
    {
        // Уведомления обрабатываются в HandleToggleMode, здесь только консоль
        if (e.Result.Success)
        {
            Console.WriteLine($"[GamepadVoiceCoordinator] 🎉 Распознавание завершено: '{e.Result.RecognizedText}'");
        }
        else
        {
            Console.WriteLine($"[GamepadVoiceCoordinator] ❌ Ошибка распознавания: {e.Result.ErrorMessage}");
        }
    }

    #endregion

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Task.Run(async () => await ShutdownAsync());
            _isDisposed = true;
        }
    }
}