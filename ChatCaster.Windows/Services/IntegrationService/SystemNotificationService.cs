using ChatCaster.Core.Services;

namespace ChatCaster.Windows.Services.IntegrationService;

public class SystemNotificationService : ISystemNotificationService
{
    public async Task ShowNotificationAsync(string title, string message)
    {
        await Task.CompletedTask;
        Console.WriteLine($"[SystemNotificationService] Уведомление: {title} - {message}");
        // Здесь можно добавить реальные Windows уведомления
    }

    public async Task<bool> SetAutoStartAsync(bool enabled)
    {
        await Task.CompletedTask;
        Console.WriteLine($"[SystemNotificationService] Автозапуск {(enabled ? "включен" : "выключен")}");
        // Здесь можно добавить работу с реестром Windows
        return true;
    }

    public async Task<bool> IsAutoStartEnabledAsync()
    {
        await Task.CompletedTask;
        // Здесь можно добавить проверку реестра Windows
        return false;
    }
}
