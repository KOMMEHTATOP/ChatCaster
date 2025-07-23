using System.Windows;
using Serilog;

namespace ChatCaster.Windows.Services.OverlayService;

/// <summary>
/// Утилиты для работы с WPF Dispatcher
/// Обеспечивает безопасный доступ к UI элементам из любого потока
/// </summary>
public static class DispatcherHelper
{
    private static readonly ILogger _logger = Log.ForContext(typeof(DispatcherHelper));

    /// <summary>
    /// Выполняет действие в UI потоке синхронно
    /// </summary>
    /// <param name="action">Действие для выполнения</param>
    public static void InvokeOnUI(Action action)
    {
        if (!TryGetDispatcher(out var dispatcher))
        {
            _logger.Warning("Dispatcher недоступен, действие выполнено в текущем потоке");
            action?.Invoke();
            return;
        }

        try
        {
            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка выполнения действия в UI потоке");
        }
    }

    /// <summary>
    /// Выполняет действие в UI потоке асинхронно
    /// </summary>
    /// <param name="action">Действие для выполнения</param>
    public static async Task InvokeOnUIAsync(Action action)
    {
        if (!TryGetDispatcher(out var dispatcher))
        {
            _logger.Warning("Dispatcher недоступен, действие выполнено в текущем потоке");
            action?.Invoke();
            return;
        }

        try
        {
            if (dispatcher.CheckAccess())
                action();
            else
                await dispatcher.InvokeAsync(action);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка выполнения асинхронного действия в UI потоке");
        }
    }

    /// <summary>
    /// Проверяет доступность dispatcher и возвращает его
    /// </summary>
    private static bool TryGetDispatcher(out System.Windows.Threading.Dispatcher? dispatcher)
    {
        dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            _logger.Warning("Application.Current или Dispatcher недоступен");
            return false;
        }
        return true;
    }
}