using ChatCaster.Core.Models;

namespace ChatCaster.Core.Services.Input;

/// <summary>
/// Сервис для работы с глобальными горячими клавишами
/// </summary>
public interface IGlobalHotkeyService
{
    event EventHandler<KeyboardShortcut>? GlobalHotkeyPressed;

    Task<bool> RegisterAsync(KeyboardShortcut shortcut);
    Task<bool> UnregisterAsync();

    bool IsRegistered { get; }
    KeyboardShortcut? CurrentShortcut { get; }
}
