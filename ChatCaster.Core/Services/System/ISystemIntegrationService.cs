using ChatCaster.Core.Models;

namespace ChatCaster.Core.Services.System;

/// <summary>
/// Композитный сервис системной интеграции - объединяет все платформо-специфичные сервисы
/// </summary>
public interface ISystemIntegrationService
{
    // Текстовый ввод
    Task<bool> SendTextAsync(string text);
    void SetTypingDelay(int delayMs);
    Task<bool> ClearActiveFieldAsync();
    Task<bool> SelectAllTextAsync();

    // Горячие клавиши
    Task<bool> RegisterGlobalHotkeyAsync(KeyboardShortcut shortcut);
    Task<bool> UnregisterGlobalHotkeyAsync();
    void SetHotkeyCaptureMode(bool isCapturing);

    event EventHandler<KeyboardShortcut>? GlobalHotkeyPressed;

    // Системные функции
    Task<bool> SetAutoStartAsync(bool enabled);
    Task<bool> IsAutoStartEnabledAsync();
    Task ShowNotificationAsync(string title, string message);

    // Информация о состоянии
    bool IsTextInputAvailable { get; }
    string ActiveWindowTitle { get; }
}
