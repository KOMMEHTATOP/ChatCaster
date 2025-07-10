using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.Input;
using ChatCaster.Core.Services.UI;
using Serilog;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Реализация сервиса уведомлений для Windows
/// Использует TrayService для показа уведомлений и подписывается на системные события
/// </summary>
public class NotificationService : INotificationService, IDisposable
{
    #region Fields

    private readonly ITrayService _trayService;
    private readonly IConfigurationService _configurationService;
    private readonly IGamepadService _gamepadService;
    private readonly IAudioCaptureService _audioService;
    private bool _isDisposed = false;

    #endregion

    #region Constructor

    public NotificationService(
        ITrayService trayService,
        IConfigurationService configurationService,
        IGamepadService gamepadService,
        IAudioCaptureService audioService)
    {
        _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _gamepadService = gamepadService ?? throw new ArgumentNullException(nameof(gamepadService));
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));

        Log.Information("NotificationService создан");
    }

    #endregion

    #region Initialization

    public async Task InitializeAsync()
    {
        try
        {
            Log.Debug("Инициализация NotificationService");

            // Подписываемся на системные события
            SubscribeToSystemEvents();

            Log.Information("NotificationService успешно инициализирован");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка инициализации NotificationService");
            throw;
        }
    }

    private void SubscribeToSystemEvents()
    {
        try
        {
            // События геймпада
            _gamepadService.GamepadEvent += OnGamepadEvent;

            // События конфигурации
            _configurationService.ConfigurationChanged += OnConfigurationChanged;

            Log.Debug("Подписка на системные события завершена");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка подписки на системные события");
            throw;
        }
    }
    private void OnGamepadEvent(object? sender, GamepadEvent e)
    {
        try
        {
            switch (e.EventType)
            {
                case GamepadEventType.Connected:
                    NotifyGamepadConnected(e.GamepadInfo);
                    break;
                
                case GamepadEventType.Disconnected:
                    NotifyGamepadDisconnected(e.GamepadInfo);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка обработки события геймпада: {e.EventType}");
        }
    }
    
    private void UnsubscribeFromSystemEvents()
    {
        try
        {
            // События геймпада
            _gamepadService.GamepadEvent -= OnGamepadEvent;

            // События конфигурации
            _configurationService.ConfigurationChanged -= OnConfigurationChanged;

            Log.Debug("Отписка от системных событий завершена");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка отписки от системных событий");
        }
    }

    #endregion

    #region System Notifications Implementation

    public void NotifyGamepadConnected(GamepadInfo gamepad)
    {
        try
        {
            var message = $"Геймпад подключен: {gamepad.Name}";
            _trayService.ShowNotification("Геймпад", message, NotificationType.Success);
            _trayService.UpdateStatus($"ChatCaster - {message}");
            
            Log.Information("Уведомление о подключении геймпада: {GamepadName}", gamepad.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка уведомления о подключении геймпада");
        }
    }

    public void NotifyGamepadDisconnected(GamepadInfo gamepad)
    {
        try
        {
            var message = $"Геймпад отключен: {gamepad.Name}";
            _trayService.ShowNotification("Геймпад", message, NotificationType.Warning);
            _trayService.UpdateStatus("ChatCaster - Геймпад отключен");
            
            Log.Information("Уведомление об отключении геймпада: {GamepadName}", gamepad.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка уведомления об отключении геймпада");
        }
    }

    public void NotifyMicrophoneChanged(string deviceName)
    {
        try
        {
            var message = $"Микрофон изменен: {deviceName}";
            _trayService.ShowNotification("Аудио", message, NotificationType.Info);
            
            Log.Information("Уведомление об изменении микрофона: {DeviceName}", deviceName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка уведомления об изменении микрофона");
        }
    }

    public void NotifyMicrophoneTest(bool success, string? deviceName = null)
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
            
            Log.Information("Уведомление о тесте микрофона: Success={Success}, Device={DeviceName}", success, deviceName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка уведомления о тесте микрофона");
        }
    }

    public void NotifyControlSettingsChanged(string shortcutType, string displayText)
    {
        try
        {
            var message = $"{shortcutType} изменены: {displayText}";
            _trayService.ShowNotification("Управление", message, NotificationType.Info);
            
            Log.Information("Уведомление об изменении настроек управления: {ShortcutType} - {DisplayText}", shortcutType, displayText);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка уведомления об изменении настроек управления");
        }
    }

    #endregion

    #region User Notifications Implementation

    public void NotifySuccess(string title, string message)
    {
        try
        {
            _trayService.ShowNotification(title, message, NotificationType.Success);
            Log.Information("Успешное уведомление: {Title} - {Message}", title, message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка показа успешного уведомления");
        }
    }

    public void NotifyWarning(string title, string message)
    {
        try
        {
            _trayService.ShowNotification(title, message, NotificationType.Warning);
            Log.Information("Предупреждающее уведомление: {Title} - {Message}", title, message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка показа предупреждающего уведомления");
        }
    }

    public void NotifyError(string title, string message)
    {
        try
        {
            _trayService.ShowNotification(title, message, NotificationType.Error);
            Log.Information("Ошибочное уведомление: {Title} - {Message}", title, message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка показа ошибочного уведомления");
        }
    }

    public void NotifyInfo(string title, string message)
    {
        try
        {
            _trayService.ShowNotification(title, message, NotificationType.Info);
            Log.Information("Информационное уведомление: {Title} - {Message}", title, message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка показа информационного уведомления");
        }
    }

    #endregion

    #region Status Management

    public void UpdateStatus(string status)
    {
        try
        {
            _trayService.UpdateStatus(status);
            Log.Debug("Статус обновлен: {Status}", status);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обновления статуса");
        }
    }

    #endregion

    #region Event Handlers


    private async void OnConfigurationChanged(object? sender, ConfigurationChangedEvent e)
    {
        try
        {
            // Обрабатываем только важные изменения настроек
            switch (e.SettingName)
            {
                case "GamepadShortcut":
                    if (e.NewValue is GamepadShortcut gamepadShortcut)
                    {
                        NotifyControlSettingsChanged("Комбинация геймпада", gamepadShortcut.DisplayText);
                    }
                    break;

                case "KeyboardShortcut":
                    if (e.NewValue is KeyboardShortcut keyboardShortcut)
                    {
                        NotifyControlSettingsChanged("Горячие клавиши", keyboardShortcut.DisplayText);
                    }
                    break;

                case "SelectedDeviceId":
                    await HandleMicrophoneChangeAsync(e.NewValue as string);
                    break;

                // Можно добавить обработку других важных настроек
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обработки изменения конфигурации: {SettingName}", e.SettingName);
        }
    }

    private async Task HandleMicrophoneChangeAsync(string? newDeviceId)
    {
        try
        {
            if (!string.IsNullOrEmpty(newDeviceId))
            {
                var devices = await _audioService.GetAvailableDevicesAsync();
                var device = devices.FirstOrDefault(d => d.Id == newDeviceId);

                if (device != null)
                {
                    NotifyMicrophoneChanged(device.Name);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обработки изменения микрофона");
        }
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_isDisposed)
        {
            Log.Debug("NotificationService уже был освобожден");
            return;
        }

        try
        {
            Log.Information("Освобождение ресурсов NotificationService");

            UnsubscribeFromSystemEvents();

            _isDisposed = true;
            GC.SuppressFinalize(this);
            
            Log.Information("NotificationService успешно освобожден");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка освобождения NotificationService");
        }
    }

    #endregion
}