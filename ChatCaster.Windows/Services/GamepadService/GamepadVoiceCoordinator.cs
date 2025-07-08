using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.Windows.Services;
using Serilog;

namespace ChatCaster.Windows.Services.GamepadService;

/// <summary>
/// Координатор между геймпадом и записью голоса
/// Обрабатывает нажатия геймпада и управляет записью
/// </summary>
public class GamepadVoiceCoordinator : IDisposable
{
    private readonly IGamepadService _gamepadService;
    private readonly IVoiceRecordingService _voiceService;
    private readonly ISystemIntegrationService _systemService;
    private readonly IConfigurationService _configService;
    private readonly ITrayService _trayService; 

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
        ITrayService trayService) 
    {
        _gamepadService = gamepadService ?? throw new ArgumentNullException(nameof(gamepadService));
        _voiceService = voiceService ?? throw new ArgumentNullException(nameof(voiceService));
        _systemService = systemService ?? throw new ArgumentNullException(nameof(systemService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService)); 

        Log.Information("🎮 [GamepadVoiceCoordinator] Создан с ITrayService из DI");
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
                Log.Information($"[GamepadVoiceCoordinator] Режим изменен на: {value}");
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
    
    private async void HandleClearField()
    {
        try
        {
            Log.Information("[GamepadVoiceCoordinator] 🧹 Активирована очистка поля");
        
            // Выделяем текст для визуальной обратной связи
            await _systemService.SelectAllTextAsync();
            Thread.Sleep(200); // Пауза чтобы пользователь увидел выделение
        
            // Очищаем поле (Delete или Backspace)
            bool success = await _systemService.ClearActiveFieldAsync();
        
            if (success)
            {
                Log.Information($"[GamepadVoiceCoordinator] ✅ Поле очищено");
                _trayService.ShowNotification("ChatCaster", "✅ Поле очищено", NotificationType.Success, 1500);
            }
            else
            {
                Log.Information($"[GamepadVoiceCoordinator] ❌ Не удалось очистить поле");
            }
        }
        catch (Exception ex)
        {
            Log.Information($"[GamepadVoiceCoordinator] ❌ Ошибка очистки: {ex.Message}");
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
        Log.Information("🎮 [GamepadVoiceCoordinator] InitializeAsync начат");

        try
        {
            lock (_lockObject)
            {
                if (_isInitialized)
                {
                    Log.Information("🎮 [GamepadVoiceCoordinator] Уже инициализирован");
                    return true;
                }

                Log.Information("🎮 [GamepadVoiceCoordinator] Подписываемся на события геймпада...");
                // Подписываемся на события геймпада
                _gamepadService.GamepadEvent += OnGamepadEvent;
                _gamepadService.ShortcutPressed += OnGamepadShortcutPressed;
                
                // Подписка на обратную связь длинного удержания
                if (_gamepadService is MainGamepadService mainService)
                {
                    mainService.ShortcutDetector.LongHoldFeedbackTriggered += OnLongHoldFeedback;
                    Log.Information("🎮 [GamepadVoiceCoordinator] Подписались на обратную связь длинного удержания");
                }

                Log.Information("🎮 [GamepadVoiceCoordinator] Подписываемся на события записи...");
                // Подписываемся на события записи для логирования
                _voiceService.StatusChanged += OnVoiceRecordingStatusChanged;
                _voiceService.RecognitionCompleted += OnVoiceRecognitionCompleted;
            }

            Log.Information("🎮 [GamepadVoiceCoordinator] Загружаем конфигурацию...");
            // Запускаем мониторинг геймпада с настройками из конфигурации
            var config = await _configService.LoadConfigAsync();
            Log.Information(
                $"🎮 [GamepadVoiceCoordinator] Конфигурация загружена. EnableGamepadControl: {config.Input.EnableGamepadControl}");

            if (!config.Input.EnableGamepadControl)
            {
                Log.Information("⚠️ [GamepadVoiceCoordinator] Управление геймпадом отключено в настройках");

                lock (_lockObject)
                {
                    _isInitialized = true; // Помечаем как инициализированный, но не запускаем
                }

                return true;
            }

            if (config.Input.GamepadShortcut == null)
            {
                Log.Information("❌ [GamepadVoiceCoordinator] GamepadShortcut не настроен в конфигурации");
                return false;
            }

            Log.Information(
                $"🎮 [GamepadVoiceCoordinator] Настройка геймпада: {config.Input.GamepadShortcut.PrimaryButton} + {config.Input.GamepadShortcut.SecondaryButton}");

            Log.Information("🎮 [GamepadVoiceCoordinator] Запускаем мониторинг геймпада...");
            await _gamepadService.StartMonitoringAsync(config.Input.GamepadShortcut);
            Log.Information("✅ [GamepadVoiceCoordinator] Мониторинг геймпада запущен");

            lock (_lockObject)
            {
                _isInitialized = true;
            }

            Log.Information("✅ [GamepadVoiceCoordinator] Инициализация завершена успешно");
            return true;
        }
        catch (Exception ex)
        {
            Log.Information($"❌ [GamepadVoiceCoordinator] Ошибка инициализации: {ex.Message}");
            _trayService.ShowNotification("Ошибка геймпада", "Не удалось инициализировать геймпад", NotificationType.Error);
            return false;
        }
    }
    private void OnGamepadEvent(object? sender, GamepadEvent e)
    {
        switch (e.EventType)
        {
            case GamepadEventType.Connected:
                Log.Information($"[GamepadVoiceCoordinator] 🎮 Геймпад подключен: {e.GamepadInfo.Name}");
                _trayService.ShowNotification("Геймпад", $"Подключен: {e.GamepadInfo.Name}", NotificationType.Success);
                break;
            
            case GamepadEventType.Disconnected:
                Log.Information($"[GamepadVoiceCoordinator] 🎮 Геймпад отключен из слота {e.GamepadIndex}");
                _trayService.ShowNotification("Геймпад", "Геймпад отключен", NotificationType.Warning);
            
                // Останавливаем запись если она идет
                Task.Run(async () =>
                {
                    try
                    {
                        if (_voiceService.IsRecording)
                        {
                            await _voiceService.CancelRecordingAsync();
                            Log.Information("[GamepadVoiceCoordinator] Запись отменена из-за отключения геймпада");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Information($"[GamepadVoiceCoordinator] Ошибка отмены записи: {ex.Message}");
                    }
                });
                break;
        }
    }
    
    private async void OnLongHoldFeedback(object? sender, EventArgs e)
    {
        try
        {
            Log.Information("[GamepadVoiceCoordinator] 💡 Длинное удержание - показываем обратную связь");
            await _systemService.SelectAllTextAsync();
            Log.Information("[GamepadVoiceCoordinator] ✅ Текст выделен - можно отпускать кнопки");
        }
        catch (Exception ex)
        {
            Log.Information($"[GamepadVoiceCoordinator] ❌ Ошибка обратной связи: {ex.Message}");
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
                _gamepadService.GamepadEvent -= OnGamepadEvent;
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

            Log.Information("[GamepadVoiceCoordinator] Остановка завершена");
        }
        catch (Exception ex)
        {
            Log.Information($"[GamepadVoiceCoordinator] Ошибка остановки: {ex.Message}");
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

            Log.Information($"[GamepadVoiceCoordinator] Настройки геймпада обновлены: {newShortcut.DisplayText}");
        }
        catch (Exception ex)
        {
            Log.Information($"[GamepadVoiceCoordinator] Ошибка обновления настроек: {ex.Message}");
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
            Log.Information($"[GamepadVoiceCoordinator] Ошибка тестирования геймпада: {ex.Message}");
            return false;
        }
    }

    #region Event Handlers


    /// <summary>
    /// Главный обработчик нажатия комбинации геймпада
    /// </summary>
    private void OnGamepadShortcutPressed(object? sender, GamepadShortcutPressedEvent e)
    {
        Log.Information($"[GamepadVoiceCoordinator] 🎯 Комбинация сработала: {e.Shortcut.DisplayText} ({e.HoldTimeMs}ms)");

        // Определяем действие по времени удержания
        if (e.HoldTimeMs >= 2000) // Длинное удержание - очистка поля
        {
            Log.Information("[GamepadVoiceCoordinator] 🧹 Длинное удержание - очищаем поле");
            HandleClearField();
        }
        else // Короткое нажатие - голосовой ввод
        {
            Log.Information("[GamepadVoiceCoordinator] 🎤 Короткое нажатие - голосовой ввод");
        
            // Обрабатываем голосовой ввод в фоновом потоке
            Task.Run(async () =>
            {
                try
                {
                    await HandleVoiceInput(e);
                }
                catch (Exception ex)
                {
                    Log.Information($"[GamepadVoiceCoordinator] Ошибка обработки комбинации: {ex.Message}");
                    _trayService.ShowNotification("Ошибка", "Ошибка обработки геймпада", NotificationType.Error);
                }
            });
        }
    }
    /// <summary>
    /// Обработка нажатия комбинации в зависимости от режима
    /// </summary>
    private async Task HandleVoiceInput(GamepadShortcutPressedEvent e)
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
                Log.Information($"[GamepadVoiceCoordinator] Неизвестный режим: {_activationMode}");
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
            Log.Information("[GamepadVoiceCoordinator] 🛑 Останавливаем запись по геймпаду");
            var result = await _voiceService.StopRecordingAsync();

            // Отправляем результат в систему если успешно
            if (result.Success && !string.IsNullOrEmpty(result.RecognizedText))
            {
                await _systemService.SendTextAsync(result.RecognizedText);
                Log.Information($"[GamepadVoiceCoordinator] ✅ Текст отправлен: '{result.RecognizedText}'");
                _trayService.ShowNotification("Распознано", result.RecognizedText, NotificationType.Success);
            }
            else
            {
                Log.Information($"[GamepadVoiceCoordinator] ❌ Ошибка распознавания: {result.ErrorMessage}");
                _trayService.ShowNotification("Ошибка", result.ErrorMessage ?? "Не удалось распознать речь", NotificationType.Error);
            }
        }
        else
        {
            // Запись не идет - запускаем
            Log.Information("[GamepadVoiceCoordinator] 🎤 Запускаем запись по геймпаду");
            bool started = await _voiceService.StartRecordingAsync();

            if (started)
            {
                Log.Information("[GamepadVoiceCoordinator] ✅ Запись началась");
                _trayService.ShowNotification("Запись", "Говорите...", NotificationType.Info);
            }
            else
            {
                Log.Information("[GamepadVoiceCoordinator] ❌ Не удалось запустить запись");
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
            Log.Information($"[GamepadVoiceCoordinator] 🎉 Распознавание завершено: '{e.Result.RecognizedText}'");
        }
        else
        {
            Log.Information($"[GamepadVoiceCoordinator] ❌ Ошибка распознавания: {e.Result.ErrorMessage}");
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