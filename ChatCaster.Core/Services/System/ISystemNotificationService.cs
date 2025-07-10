namespace ChatCaster.Core.Services.System;

/// <summary>
/// Сервис для системных уведомлений и автозапуска
/// </summary>
public interface ISystemNotificationService
{
    Task ShowNotificationAsync(string title, string message);
    Task<bool> SetAutoStartAsync(bool enabled);
    Task<bool> IsAutoStartEnabledAsync();
}
