namespace ChatCaster.Core.Services.UI;

/// <summary>
/// Интерфейс для работы с системным треем
/// </summary>
public interface ITrayService
{
    /// <summary>
    /// Инициализирует трей-сервис
    /// </summary>
    void Initialize();

    /// <summary>
    /// Показывает уведомление в трее
    /// </summary>
    /// <param name="title">Заголовок уведомления</param>
    /// <param name="message">Текст уведомления</param>
    /// <param name="type">Тип уведомления</param>
    /// <param name="timeout">Время показа в миллисекундах</param>
    void ShowNotification(string title, string message, NotificationType type = NotificationType.Info, int timeout = 3000);

    /// <summary>
    /// Обновляет статус в tooltip трея
    /// </summary>
    /// <param name="status">Новый статус</param>
    void UpdateStatus(string status);

    /// <summary>
    /// Показывает уведомление при первом сворачивании в трей
    /// </summary>
    void ShowFirstTimeNotification();

    /// <summary>
    /// Видимость иконки в трее
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Освобождает ресурсы
    /// </summary>
    void Dispose();

    #region События для слабой связанности

    /// <summary>
    /// Событие запроса показа главного окна (двойной клик или пункт меню)
    /// </summary>
    event EventHandler? ShowMainWindowRequested;

    /// <summary>
    /// Событие запроса открытия настроек (пункт меню)
    /// </summary>
    event EventHandler? ShowSettingsRequested;

    /// <summary>
    /// Событие запроса выхода из приложения (пункт меню)
    /// </summary>
    event EventHandler? ExitApplicationRequested;

    #endregion

}

/// <summary>
/// Типы уведомлений в трее
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Информационное сообщение
    /// </summary>
    Info,

    /// <summary>
    /// Успешное выполнение операции
    /// </summary>
    Success,

    /// <summary>
    /// Предупреждение
    /// </summary>
    Warning,

    /// <summary>
    /// Ошибка
    /// </summary>
    Error
}
