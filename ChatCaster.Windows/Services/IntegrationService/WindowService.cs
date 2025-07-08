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
        _logger.LogDebug("GetForegroundWindow returned handle: {Handle}", handle);
    
        if (handle == IntPtr.Zero) 
        {
            _logger.LogWarning("GetForegroundWindow returned Zero handle");
            return string.Empty;
        }

        int length = GetWindowTextLength(handle);
        _logger.LogDebug("Window text length: {Length}", length);
    
        if (length <= 0) 
        {
            _logger.LogWarning("Window text length is {Length}", length);
            return string.Empty;
        }

        var title = new StringBuilder(length + 1);
        int result = GetWindowText(handle, title, title.Capacity);
        var windowTitle = title.ToString();
    
        _logger.LogDebug("Window title retrieved: '{Title}' (result: {Result})", windowTitle, result);
        return windowTitle;
    }
    
    public IntPtr GetActiveWindowHandle() => GetForegroundWindow();

    public bool IsOwnWindow(string windowTitle) => 
        windowTitle.Contains("ChatCaster", StringComparison.OrdinalIgnoreCase);
}