using ChatCaster.Core.Services.System;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;

namespace ChatCaster.Windows.Services.IntegrationService;

public class WindowService : IWindowService
{
    private readonly ILogger<WindowService> _logger;
    private readonly string[] _ownWindowTitles = { "ChatCaster", "ChatCaster Overlay" };

    public WindowService(ILogger<WindowService> logger)
    {
        _logger = logger;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    public string GetActiveWindowTitle()
    {
        IntPtr handle = GetForegroundWindow();
    
        if (handle == IntPtr.Zero) 
        {
            _logger.LogWarning("Не удалось получить дескриптор активного окна");
            return string.Empty;
        }

        int length = GetWindowTextLength(handle);
    
        if (length <= 0) 
        {
            _logger.LogDebug("Заголовок активного окна пустой");
            return string.Empty;
        }

        var title = new StringBuilder(length + 1);
        int result = GetWindowText(handle, title, title.Capacity);
        var windowTitle = title.ToString();
    
        _logger.LogDebug("Получен заголовок активного окна: {WindowTitle}", windowTitle);
        return windowTitle;
    }
    
    public IntPtr GetActiveWindowHandle() => GetForegroundWindow();

    public bool IsOwnWindow(string windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle))
        {
            _logger.LogDebug("Заголовок окна пустой, считаем его окном ChatCaster");
            return true; // Пустой заголовок может быть у оверлея
        }
        bool isOwn = _ownWindowTitles.Any(title => windowTitle.Contains(title, StringComparison.OrdinalIgnoreCase));
        _logger.LogDebug("Проверка IsOwnWindow: {WindowTitle}, результат: {IsOwn}", windowTitle, isOwn);
        return isOwn;
    }
}