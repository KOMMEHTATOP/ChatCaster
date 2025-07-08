using ChatCaster.Core.Models;

namespace ChatCaster.Core.Services.Overlay;

/// <summary>
/// Интерфейс для платформо-специфичного отображения overlay
/// Реализуется в каждой платформе (Windows, Linux, Mac)
/// </summary>
public interface IOverlayDisplay
{
    /// <summary>
    /// Показывает overlay с указанным статусом
    /// </summary>
    /// <param name="status">Статус записи для отображения</param>
    Task ShowAsync(RecordingStatus status);

    /// <summary>
    /// Скрывает overlay
    /// </summary>
    Task HideAsync();

    /// <summary>
    /// Обновляет статус overlay
    /// </summary>
    /// <param name="status">Новый статус</param>
    /// <param name="message">Пользовательское сообщение (опционально)</param>
    Task UpdateStatusAsync(RecordingStatus status, string? message = null);
}
