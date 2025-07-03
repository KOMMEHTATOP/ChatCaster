using ChatCaster.Core.Services;
using ChatCaster.Core.Models;

namespace ChatCaster.Windows.Services.IntegrationService;

/// <summary>
/// Композитный сервис системной интеграции - объединяет все платформо-специфичные сервисы
/// </summary>
public class SystemIntegrationService : ISystemIntegrationService, IDisposable
{
    private readonly ITextInputService _textInputService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISystemNotificationService _notificationService;
    private readonly IWindowService _windowService;

    public SystemIntegrationService(
        ITextInputService textInputService,
        IGlobalHotkeyService hotkeyService,
        ISystemNotificationService notificationService,
        IWindowService windowService)
    {
        _textInputService = textInputService;
        _hotkeyService = hotkeyService;
        _notificationService = notificationService;
        _windowService = windowService;

        Console.WriteLine("🔥 SystemIntegrationService создан");
    }

    #region События
    public event EventHandler<KeyboardShortcut>? GlobalHotkeyPressed
    {
        add => _hotkeyService.GlobalHotkeyPressed += value;
        remove => _hotkeyService.GlobalHotkeyPressed -= value;
    }
    #endregion

    #region Текстовый ввод
    public Task<bool> SendTextAsync(string text) => _textInputService.SendTextAsync(text);

    public Task<bool> ClearActiveFieldAsync() => _textInputService.ClearActiveFieldAsync(); 
    public Task<bool> SelectAllTextAsync() => _textInputService.SelectAllTextAsync();

    public void SetTypingDelay(int delayMs) => _textInputService.SetTypingDelay(delayMs);
    #endregion
    
    #region Горячие клавиши
    public Task<bool> RegisterGlobalHotkeyAsync(KeyboardShortcut shortcut) => 
        _hotkeyService.RegisterAsync(shortcut);

    public Task<bool> UnregisterGlobalHotkeyAsync() => 
        _hotkeyService.UnregisterAsync();
    #endregion

    #region Системные функции
    public Task<bool> SetAutoStartAsync(bool enabled) => 
        _notificationService.SetAutoStartAsync(enabled);

    public Task<bool> IsAutoStartEnabledAsync() => 
        _notificationService.IsAutoStartEnabledAsync();

    public Task ShowNotificationAsync(string title, string message) => 
        _notificationService.ShowNotificationAsync(title, message);
    #endregion

    #region Информация о состоянии
    public bool IsTextInputAvailable => _textInputService.CanSendToActiveWindow();

    public string ActiveWindowTitle => _windowService.GetActiveWindowTitle();
    #endregion

    public void Dispose()
    {
        try
        {
            if (_hotkeyService is IDisposable disposableHotkey)
            {
                disposableHotkey.Dispose();
            }
            Console.WriteLine("✅ SystemIntegrationService disposed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка при освобождении ресурсов SystemIntegrationService: {ex.Message}");
        }
    }
}