using ChatCaster.Core.Services;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;

namespace ChatCaster.Windows.Services.IntegrationService;

public class WindowService : IWindowService
{
    private readonly ILogger<WindowService> _logger;
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
        if (handle == IntPtr.Zero) return string.Empty;

        int length = GetWindowTextLength(handle);
        if (length <= 0) return string.Empty;

        var title = new StringBuilder(length + 1);
        GetWindowText(handle, title, title.Capacity);
        return title.ToString();
    }

    public IntPtr GetActiveWindowHandle() => GetForegroundWindow();

    public bool IsOwnWindow(string windowTitle) => 
        windowTitle.Contains("ChatCaster", StringComparison.OrdinalIgnoreCase);

    public bool IsSteamWindow(string windowTitle)
    {
        bool isLibrary = windowTitle.ToLower().Contains("steam") && !windowTitle.ToLower().Contains("store");
        bool isStore = windowTitle.ToLower().Contains("store.steampowered.com") || 
                       windowTitle.ToLower().Contains("steam") && windowTitle.ToLower().Contains("store");
    
        _logger.LogDebug("Анализ окна: '{WindowTitle}' - Library: {IsLibrary}, Store: {IsStore}", 
            windowTitle, isLibrary, isStore);
    
        return isLibrary || isStore;
    }    
}