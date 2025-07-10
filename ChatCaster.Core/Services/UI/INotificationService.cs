using ChatCaster.Core.Models;

namespace ChatCaster.Core.Services.UI;

/// <summary>
/// Сервис управления уведомлениями приложения
/// Координирует системные события и пользовательские уведомления
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Инициализирует сервис уведомлений и подписывается на системные события
    /// </summary>
    Task InitializeAsync();

    #region Системные уведомления

    /// <summary>
    /// Уведомление о подключении геймпада
    /// </summary>
    /// <param name="gamepad">Информация о подключенном геймпаде</param>
    void NotifyGamepadConnected(GamepadInfo gamepad);

    /// <summary>
    /// Уведомление об отключении геймпада
    /// </summary>
    /// <param name="gamepad">Информация об отключенном геймпаде</param>
    void NotifyGamepadDisconnected(GamepadInfo gamepad);

    /// <summary>
    /// Уведомление об изменении микрофона
    /// </summary>
    /// <param name="deviceName">Название нового устройства</param>
    void NotifyMicrophoneChanged(string deviceName);

    /// <summary>
    /// Уведомление о результате теста микрофона
    /// </summary>
    /// <param name="success">Успешность теста</param>
    /// <param name="deviceName">Название тестируемого устройства (опционально)</param>
    void NotifyMicrophoneTest(bool success, string? deviceName = null);

    /// <summary>
    /// Уведомление об изменении настроек управления
    /// </summary>
    /// <param name="shortcutType">Тип комбинации (геймпад/клавиатура)</param>
    /// <param name="displayText">Текстовое представление комбинации</param>
    void NotifyControlSettingsChanged(string shortcutType, string displayText);

    #endregion

    #region Пользовательские уведомления

    /// <summary>
    /// Показать уведомление об успешном выполнении операции
    /// </summary>
    /// <param name="title">Заголовок уведомления</param>
    /// <param name="message">Текст уведомления</param>
    void NotifySuccess(string title, string message);

    /// <summary>
    /// Показать предупреждающее уведомление
    /// </summary>
    /// <param name="title">Заголовок уведомления</param>
    /// <param name="message">Текст уведомления</param>
    void NotifyWarning(string title, string message);

    /// <summary>
    /// Показать уведомление об ошибке
    /// </summary>
    /// <param name="title">Заголовок уведомления</param>
    /// <param name="message">Текст уведомления</param>
    void NotifyError(string title, string message);

    /// <summary>
    /// Показать информационное уведомление
    /// </summary>
    /// <param name="title">Заголовок уведомления</param>
    /// <param name="message">Текст уведомления</param>
    void NotifyInfo(string title, string message);

    #endregion

    #region Управление статусом

    /// <summary>
    /// Обновить статус в системном трее
    /// </summary>
    /// <param name="status">Новый статус</param>
    void UpdateStatus(string status);

    #endregion

    /// <summary>
    /// Освобождает ресурсы сервиса
    /// </summary>
    void Dispose();
}
