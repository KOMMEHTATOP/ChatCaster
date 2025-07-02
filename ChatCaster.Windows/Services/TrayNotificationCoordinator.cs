using System;
using System.Threading.Tasks;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using Serilog;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Координатор уведомлений для трея
/// Подписывается на системные события и отправляет соответствующие уведомления в TrayService
/// </summary>
public class TrayNotificationCoordinator : IDisposable
{
    #region Fields

    private readonly ITrayService _trayService;
    private readonly IConfigurationService _configurationService;
    private readonly IGamepadService _gamepadService;
    private readonly IAudioCaptureService _audioService;
    
    private AppConfig? _currentConfig;
    private bool _isDisposed = false;

    #endregion

    #region Constructor

    public TrayNotificationCoordinator(
        ITrayService trayService,
        IConfigurationService configurationService,
        IGamepadService gamepadService,
        IAudioCaptureService audioService)
    {
        _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _gamepadService = gamepadService ?? throw new ArgumentNullException(nameof(gamepadService));
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));

        Log.Debug("TrayNotificationCoordinator создан");
    }

    #endregion

    #region Initialization

    public async Task InitializeAsync()
    {
        try
        {
            Log.Debug("Инициализация TrayNotificationCoordinator");

            // Получаем текущую конфигурацию
            _currentConfig = _configurationService.CurrentConfig;

            // Передаем конфигурацию в TrayService
            if (_trayService is TrayService trayServiceImpl)
            {
                trayServiceImpl.SetConfig(_currentConfig);
            }

            // Подписываемся на события
            SubscribeToEvents();

            Log.Information("TrayNotificationCoordinator успешно инициализирован");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка инициализации TrayNotificationCoordinator");
            throw;
        }
    }

    #endregion

    #region Event Subscription

    private void SubscribeToEvents()
    {
        try
        {
            // События геймпада
            _gamepadService.GamepadConnected += OnGamepadConnected;
            _gamepadService.GamepadDisconnected += OnGamepadDisconnected;

            // События конфигурации
            _configurationService.ConfigurationChanged += OnConfigurationChanged;

            Log.Debug("Подписка на события для уведомлений завершена");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка подписки на события");
        }
    }

    private void UnsubscribeFromEvents()
    {
        try
        {
            // События геймпада
            _gamepadService.GamepadConnected -= OnGamepadConnected;
            _gamepadService.GamepadDisconnected -= OnGamepadDisconnected;

            // События конфигурации
            _configurationService.ConfigurationChanged -= OnConfigurationChanged;

            Log.Debug("Отписка от событий для уведомлений завершена");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка отписки от событий");
        }
    }

    #endregion

    #region Event Handlers

    private void OnGamepadConnected(object? sender, GamepadConnectedEvent e)
    {
        try
        {
            var message = $"Геймпад подключен: {e.GamepadInfo.Name}";
            _trayService.ShowNotification("Геймпад", message, NotificationType.Success);
            _trayService.UpdateStatus($"ChatCaster - {message}");
            
            Log.Information("Уведомление о подключении геймпада отправлено: {GamepadName}", e.GamepadInfo.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обработки события подключения геймпада");
        }
    }

    private void OnGamepadDisconnected(object? sender, GamepadDisconnectedEvent e)
    {
        try
        {
            var message = $"Геймпад отключен: {e.GamepadInfo.Name}";
            _trayService.ShowNotification("Геймпад", message, NotificationType.Warning);
            _trayService.UpdateStatus("ChatCaster - Геймпад отключен");
            
            Log.Information("Уведомление об отключении геймпада отправлено: {GamepadName}", e.GamepadInfo.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обработки события отключения геймпада");
        }
    }

    private void OnConfigurationChanged(object? sender, ConfigurationChangedEvent e)
    {
        try
        {
            // Обновляем локальную копию конфигурации
            _currentConfig = _configurationService.CurrentConfig;

            // Обновляем конфигурацию в TrayService
            if (_trayService is TrayService trayServiceImpl)
            {
                trayServiceImpl.SetConfig(_currentConfig);
            }

            // Показываем уведомления для важных изменений настроек
            HandleConfigurationChangeNotification(e);
            
            Log.Debug("Конфигурация обновлена в TrayNotificationCoordinator");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обработки изменения конфигурации");
        }
    }

    private void HandleConfigurationChangeNotification(ConfigurationChangedEvent e)
    {
        try
        {
            // Уведомления для важных изменений настроек
            switch (e.SettingName)
            {
                case "GamepadShortcut":
                    if (e.NewValue is GamepadShortcut shortcut)
                    {
                        var message = $"Комбинация геймпада изменена: {shortcut.DisplayText}";
                        _trayService.ShowNotification("Управление", message, NotificationType.Info);
                        Log.Information("Уведомление об изменении комбинации геймпада: {Combo}", shortcut.DisplayText);
                    }
                    break;

                case "KeyboardShortcut":
                    if (e.NewValue is KeyboardShortcut keyboardShortcut)
                    {
                        var message = $"Горячие клавиши изменены: {keyboardShortcut.DisplayText}";
                        _trayService.ShowNotification("Управление", message, NotificationType.Info);
                        Log.Information("Уведомление об изменении горячих клавиш: {Combo}", keyboardShortcut.DisplayText);
                    }
                    break;

                case "SelectedDeviceId":
                    HandleMicrophoneChangeNotification(e);
                    break;

                // Можно добавить другие важные настройки
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обработки уведомления об изменении настройки: {SettingName}", e.SettingName);
        }
    }

    private async void HandleMicrophoneChangeNotification(ConfigurationChangedEvent e)
    {
        try
        {
            if (e.NewValue is string newDeviceId && !string.IsNullOrEmpty(newDeviceId))
            {
                // Получаем информацию об устройстве
                var devices = await _audioService.GetAvailableDevicesAsync();
                var device = devices.FirstOrDefault(d => d.Id == newDeviceId);

                if (device != null)
                {
                    var message = $"Микрофон изменен: {device.Name}";
                    _trayService.ShowNotification("Аудио", message, NotificationType.Info);
                    Log.Information("Уведомление об изменении микрофона: {DeviceName}", device.Name);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обработки уведомления об изменении микрофона");
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Отправляет уведомление о результате теста микрофона
    /// </summary>
    public void NotifyMicrophoneTestResult(bool success, string? deviceName = null)
    {
        try
        {
            if (success)
            {
                var message = !string.IsNullOrEmpty(deviceName) 
                    ? $"Микрофон работает: {deviceName}" 
                    : "Микрофон работает нормально";
                _trayService.ShowNotification("Тест микрофона", message, NotificationType.Success);
            }
            else
            {
                _trayService.ShowNotification("Тест микрофона", "Обнаружена проблема с микрофоном", NotificationType.Error);
            }
            
            Log.Information("Уведомление о тесте микрофона отправлено: Success={Success}, Device={DeviceName}", success, deviceName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка отправки уведомления о тесте микрофона");
        }
    }

    /// <summary>
    /// Отправляет произвольное уведомление
    /// </summary>
    public void SendCustomNotification(string title, string message, NotificationType type = NotificationType.Info)
    {
        try
        {
            _trayService.ShowNotification(title, message, type);
            Log.Information("Произвольное уведомление отправлено: {Title} - {Message}", title, message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка отправки произвольного уведомления: {Title} - {Message}", title, message);
        }
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_isDisposed)
            return;

        try
        {
            Log.Debug("Освобождение ресурсов TrayNotificationCoordinator");
            
            UnsubscribeFromEvents();
            
            _isDisposed = true;
            Log.Information("TrayNotificationCoordinator успешно освобожден");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка освобождения TrayNotificationCoordinator");
        }
    }

    #endregion
}