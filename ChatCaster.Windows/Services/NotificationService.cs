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
    private bool _isDisposed;

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
    }

    #endregion

    #region Initialization

    public Task InitializeAsync()
    {
        try
        {
            // Подписываемся на системные события
            SubscribeToSystemEvents();

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка инициализации NotificationService");
            return Task.FromException(ex);
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
            _trayService.ShowNotification("Аудио", message);
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
            _trayService.ShowNotification("Управление", message);
            
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
            _trayService.ShowNotification(title, message);
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
            return;
        }

        try
        {
            UnsubscribeFromSystemEvents();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка освобождения NotificationService");
        }
    }

    #endregion
}