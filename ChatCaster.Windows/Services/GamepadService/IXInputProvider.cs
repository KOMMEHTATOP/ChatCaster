using ChatCaster.Core.Models;

namespace ChatCaster.Windows.Services.GamepadService;

/// <summary>
/// Интерфейс для работы с XInput драйвером
/// Абстрагирует низкоуровневую работу с геймпадами
/// </summary>
public interface IXInputProvider
{
    /// <summary>
    /// Проверяет подключен ли геймпад в указанном слоте
    /// </summary>
    /// <param name="controllerIndex">Индекс контроллера (0-3)</param>
    /// <returns>True если геймпад подключен</returns>
    bool IsControllerConnected(int controllerIndex);
    
    /// <summary>
    /// Получает текущее состояние геймпада
    /// </summary>
    /// <param name="controllerIndex">Индекс контроллера (0-3)</param>
    /// <returns>Состояние геймпада или null если не подключен</returns>
    GamepadState? GetControllerState(int controllerIndex);
    
    /// <summary>
    /// Находит первый подключенный геймпад
    /// </summary>
    /// <returns>Индекс первого подключенного геймпада или -1 если не найден</returns>
    int FindFirstConnectedController();
    
    /// <summary>
    /// Получает информацию о геймпаде
    /// </summary>
    /// <param name="controllerIndex">Индекс контроллера</param>
    /// <returns>Информация о геймпаде или null если не подключен</returns>
    GamepadInfo? GetControllerInfo(int controllerIndex);
    
    /// <summary>
    /// Проверяет доступность XInput в системе
    /// </summary>
    /// <returns>True если XInput доступен</returns>
    bool IsXInputAvailable();
}
