using ChatCaster.Core.Events;
using ChatCaster.Core.Models;

namespace ChatCaster.Core.Services.Input;

/// <summary>
/// Упрощенный сервис для работы с геймпадами (только XInput)
/// </summary>
public interface IGamepadService
{
    // События
    event EventHandler<GamepadEvent>? GamepadEvent;
    event EventHandler<GamepadShortcutPressedEvent>? ShortcutPressed;

    // Управление мониторингом
    Task StartMonitoringAsync(GamepadShortcut shortcut);
    Task StopMonitoringAsync();

    // Получение информации
    Task<GamepadInfo?> GetConnectedGamepadAsync();
    GamepadState? GetCurrentState();

    // Статус
    bool IsMonitoring { get; }
    bool IsGamepadConnected { get; }

    // Тестирование (для UI настроек)
    Task<bool> TestConnectionAsync();
}
