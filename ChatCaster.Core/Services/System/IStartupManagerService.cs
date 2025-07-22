namespace ChatCaster.Core.Services.System;

/// <summary>
/// Сервис для управления автозапуском приложения с операционной системой
/// </summary>
public interface IStartupManagerService
{
    /// <summary>
    /// Проверяет, включен ли автозапуск приложения
    /// </summary>
    /// <returns>True если автозапуск включен</returns>
    Task<bool> IsStartupEnabledAsync();

    /// <summary>
    /// Включает автозапуск приложения
    /// </summary>
    Task EnableStartupAsync();

    /// <summary>
    /// Выключает автозапуск приложения
    /// </summary>
    Task DisableStartupAsync();

    /// <summary>
    /// Устанавливает автозапуск в соответствии с переданным значением
    /// </summary>
    /// <param name="enabled">True для включения, false для отключения</param>
    Task SetStartupAsync(bool enabled);
}
