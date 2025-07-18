using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.Input;
using ChatCaster.Core.Services.System;
using ChatCaster.Core.Services.UI;
using Serilog;

namespace ChatCaster.Windows.Services.GamepadService;

/// <summary>
/// Координатор между геймпадом и записью голоса
/// Обрабатывает нажатия геймпада и управляет записью
/// </summary>
public class GamepadVoiceCoordinator : IDisposable
{
    private readonly static ILogger _logger = Log.ForContext<GamepadVoiceCoordinator>();

    private readonly IGamepadService _gamepadService;
    private readonly IVoiceRecordingService _voiceService;
    private readonly ISystemIntegrationService _systemService;
    private readonly IConfigurationService _configService;
    private readonly ITrayService _trayService; 

    private readonly object _lockObject = new();
    private bool _isDisposed;
    private bool _isInitialized;

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
    }
    
    private async void HandleClearField()
    {
        try
        {
            _logger.Information("Активирована очистка поля");
        
            // Выделяем текст для визуальной обратной связи
            await _systemService.SelectAllTextAsync();
            Thread.Sleep(200); // Пауза чтобы пользователь увидел выделение
        
            // Очищаем поле (Delete или Backspace)
            bool success = await _systemService.ClearActiveFieldAsync();
        
            if (success)
            {
                _logger.Information("Поле очищено");
                _trayService.ShowNotification("ChatCaster", "✅ Поле очищено", NotificationType.Success, 1500);
            }
            else
            {
                _logger.Warning("Не удалось очистить поле");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка очистки поля");
        }
    }

    /// <summary>
    /// Инициализация координатора
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        try
        {
            lock (_lockObject)
            {
                if (_isInitialized)
                {
                    _logger.Debug("Уже инициализирован");
                    return true;
                }

                // Подписываемся на события геймпада
                _gamepadService.GamepadEvent += OnGamepadEvent;
                _gamepadService.ShortcutPressed += OnGamepadShortcutPressed;
                
                // Подписка на обратную связь длинного удержания
                if (_gamepadService is MainGamepadService mainService)
                {
                    mainService.ShortcutDetector.LongHoldFeedbackTriggered += OnLongHoldFeedback;
                }

                // Подписываемся на события записи для логирования
                _voiceService.StatusChanged += OnVoiceRecordingStatusChanged;
                _voiceService.RecognitionCompleted += OnVoiceRecognitionCompleted;
            }

            var config = _configService.CurrentConfig;
            // Запускаем мониторинг геймпада с настройками из конфигурации

            if (!config.Input.EnableGamepadControl)
            {
                _logger.Information("Управление геймпадом отключено в настройках");

                lock (_lockObject)
                {
                    _isInitialized = true; // Помечаем как инициализированный, но не запускаем
                }

                return true;
            }

            _logger.Debug("Настройка геймпада: {Primary} + {Secondary}", 
                config.Input.GamepadShortcut.PrimaryButton, config.Input.GamepadShortcut.SecondaryButton);

            await _gamepadService.StartMonitoringAsync(config.Input.GamepadShortcut);

            lock (_lockObject)
            {
                _isInitialized = true;
            }

            _logger.Information("Инициализация GamepadVoiceCoordinator завершена успешно");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка инициализации GamepadVoiceCoordinator");
            _trayService.ShowNotification("Ошибка геймпада", "Не удалось инициализировать геймпад", NotificationType.Error);
            return false;
        }
    }

    private void OnGamepadEvent(object? sender, GamepadEvent e)
    {
        switch (e.EventType)
        {
            case GamepadEventType.Connected:
                _logger.Information("Геймпад подключен: {Name}", e.GamepadInfo.Name);
                _trayService.ShowNotification("Геймпад", $"Подключен: {e.GamepadInfo.Name}", NotificationType.Success);
                break;
            
            case GamepadEventType.Disconnected:
                _logger.Information("Геймпад отключен из слота {Index}", e.GamepadIndex);
                _trayService.ShowNotification("Геймпад", "Геймпад отключен", NotificationType.Warning);
            
                // Останавливаем запись если она идет
                Task.Run(async () =>
                {
                    try
                    {
                        if (_voiceService.IsRecording)
                        {
                            await _voiceService.CancelRecordingAsync();
                            _logger.Information("Запись отменена из-за отключения геймпада");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Ошибка отмены записи при отключении геймпада");
                    }
                });
                break;
        }
    }
    
    private async void OnLongHoldFeedback(object? sender, EventArgs e)
    {
        try
        {
            _logger.Debug("Длинное удержание - показываем обратную связь");
            await _systemService.SelectAllTextAsync();
            _logger.Debug("Текст выделен - можно отпускать кнопки");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка обратной связи длинного удержания");
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

            _logger.Information("Остановка GamepadVoiceCoordinator завершена");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка остановки GamepadVoiceCoordinator");
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

            _logger.Information("Настройки геймпада обновлены: {Shortcut}", newShortcut.DisplayText);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка обновления настроек геймпада");
        }
    }

    #region Event Handlers

    /// <summary>
    /// Главный обработчик нажатия комбинации геймпада
    /// </summary>
    private void OnGamepadShortcutPressed(object? sender, GamepadShortcutPressedEvent e)
    {
        _logger.Information("Комбинация сработала: {Shortcut} ({HoldTime}ms)", e.Shortcut.DisplayText, e.HoldTimeMs);

        // Определяем действие по времени удержания
        if (e.HoldTimeMs >= 2000) // Длинное удержание - очистка поля
        {
            _logger.Information("Длинное удержание - очищаем поле");
            HandleClearField();
        }
        else // Короткое нажатие - голосовой ввод
        {
            _logger.Information("Короткое нажатие - голосовой ввод");
        
            // Обрабатываем голосовой ввод в фоновом потоке
            Task.Run(async () =>
            {
                try
                {
                    await HandleVoiceInput();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Ошибка обработки голосового ввода");
                    _trayService.ShowNotification("Ошибка", "Ошибка обработки геймпада", NotificationType.Error);
                }
            });
        }
    }

    /// <summary>
    /// Обработка голосового ввода (только Toggle режим)
    /// </summary>
    private async Task HandleVoiceInput()
    {
        if (_voiceService.IsRecording)
        {
            // Запись идет - останавливаем
            _logger.Information("Останавливаем запись по геймпаду");
            var result = await _voiceService.StopRecordingAsync();

            // Отправляем результат в систему если успешно
            if (result.Success && !string.IsNullOrEmpty(result.RecognizedText))
            {
                await _systemService.SendTextAsync(result.RecognizedText);
                _logger.Information("Текст отправлен: '{Text}'", result.RecognizedText);
                _trayService.ShowNotification("Распознано", result.RecognizedText, NotificationType.Success);
            }
            else
            {
                _logger.Warning("Ошибка распознавания: {Error}", result.ErrorMessage);
                _trayService.ShowNotification("Ошибка", result.ErrorMessage ?? "Не удалось распознать речь", NotificationType.Error);
            }
        }
        else
        {
            // Запись не идет - запускаем
            _logger.Information("Запускаем запись по геймпаду");
            bool started = await _voiceService.StartRecordingAsync();

            if (started)
            {
                _logger.Information("Запись началась");
                _trayService.ShowNotification("Запись", "Говорите...");
            }
            else
            {
                _logger.Warning("Не удалось запустить запись");
                _trayService.ShowNotification("Ошибка", "Не удалось начать запись", NotificationType.Error);
            }
        }
    }

    /// <summary>
    /// Обработчик изменения статуса записи
    /// </summary>
    private void OnVoiceRecordingStatusChanged(object? sender, RecordingStatusChangedEvent e)
    {
        // Логирование статуса убрано - слишком частые события
    }

    /// <summary>
    /// Обработчик завершения распознавания
    /// </summary>
    private void OnVoiceRecognitionCompleted(object? sender, VoiceRecognitionCompletedEvent e)
    {
        if (e.Result.Success)
        {
            _logger.Information("Распознавание завершено: '{Text}'", e.Result.RecognizedText);
        }
        else
        {
            _logger.Warning("Ошибка распознавания: {Error}", e.Result.ErrorMessage);
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