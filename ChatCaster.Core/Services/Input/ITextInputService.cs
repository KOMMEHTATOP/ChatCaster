namespace ChatCaster.Core.Services.Input;

/// <summary>
/// Сервис для ввода текста в активное окно
/// </summary>
public interface ITextInputService
{
    /// <summary>
    /// Отправляет текст в активное окно
    /// </summary>
    Task<bool> SendTextAsync(string text);
    Task<bool> ClearActiveFieldAsync();
    Task<bool> SelectAllTextAsync();
    /// <summary>
    /// Устанавливает задержку между вводом символов
    /// </summary>
    void SetTypingDelay(int delayMs);

    /// <summary>
    /// Проверяет возможность ввода в текущее активное окно
    /// </summary>
    bool CanSendToActiveWindow();
}
