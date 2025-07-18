using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.UI;
using Serilog;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Упрощенная реализация TrayService - только работа с NotifyIcon
/// Вся сложная бизнес-логика вынесена в Window
/// </summary>
public class TrayService : ITrayService, IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly IConfigurationService _configService;
    private bool _hasShownFirstTimeNotification;
    private bool _isDisposed;
    
    #region Properties

    public bool IsVisible => _notifyIcon?.Visible == true;

    #endregion

    #region Events

    public event EventHandler? ShowMainWindowRequested;
    public event EventHandler? ShowSettingsRequested;
    public event EventHandler? ExitApplicationRequested;

    #endregion

    #region Constructor

    public TrayService(IConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    #endregion

    #region Public Methods

    public void Initialize()
    {
        if (_isDisposed)
        {
            return;
        }
        
        try
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "ChatCaster - Готов к работе",
                Visible = true
            };

            // Двойной клик - показать главное окно
            _notifyIcon.DoubleClick += OnDoubleClick;

            CreateContextMenu();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка инициализации TrayService");
            throw;
        }
    }

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info, int timeout = 3000)
    {
        try
        {
            // Проверяем настройки перед показом уведомления
            if (_configService.CurrentConfig?.System?.ShowNotifications != true)
            {
                return;
            }

            if (_notifyIcon == null || _isDisposed)
            {
                return;
            }

            var toolTipIcon = GetToolTipIcon(type);
            
            _notifyIcon.ShowBalloonTip(timeout, title, message, toolTipIcon);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка показа уведомления: {Title} - {Message}", title, message);
        }
    }

    public void UpdateStatus(string status)
    {
        try
        {
            if (_notifyIcon == null || _isDisposed)
            {
                return;
            }

            // Ограничиваем длину tooltip (Windows лимит ~127 символов)
            string tooltipText = status.Length > 120 ? status.Substring(0, 117) + "..." : status;
            _notifyIcon.Text = tooltipText;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обновления статуса трея: {Status}", status);
        }
    }

    public void ShowFirstTimeNotification()
    {
        if (_hasShownFirstTimeNotification) return;
        
        ShowNotification(
            "ChatCaster", 
            "Приложение свернуто в системный трей. Двойной клик для возврата.", 
            NotificationType.Info);
            
        _hasShownFirstTimeNotification = true;
    }

    #endregion

    #region Private Methods

    private Icon LoadIcon()
    {
        try
        {
            // Попытка загрузки из ресурсов приложения
            var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Resources/Logo.ico"));
            if (iconStream != null)
            {
                return new Icon(iconStream.Stream);
            }
        
            // Fallback: попытка загрузки как файл
            if (File.Exists("Resources/Logo.ico"))
            {
                return new Icon("Resources/Logo.ico");
            }
        
            return SystemIcons.Application;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка загрузки иконки, используем системную");
            return SystemIcons.Application;
        }
    }
    
    private void CreateContextMenu()
    {
        if (_notifyIcon == null) 
        {
            return;
        }

        try
        {
            var contextMenu = new ContextMenuStrip();
            
            contextMenu.Items.Add("📋 Показать окно", null, (s, e) => ShowMainWindowRequested?.Invoke(this, EventArgs.Empty));
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("ℹ️ О программе", null, (s, e) => ShowAboutDialog());
            contextMenu.Items.Add("❌ Выход", null, (s, e) => ExitApplicationRequested?.Invoke(this, EventArgs.Empty));

            _notifyIcon.ContextMenuStrip = contextMenu;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка создания контекстного меню");
        }
    }

    private void OnDoubleClick(object? sender, EventArgs e)
    {
        try
        {
            ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обработки двойного клика по трею");
        }
    }

    // Единственная "бизнес-логика" оставил для удобства.
    private void ShowAboutDialog()
    {
        try
        {
            System.Windows.MessageBox.Show(
                "ChatCaster v1.0.0\n\n" +
                "Голосовой ввод для игр с поддержкой геймпада\n\n" +
                "Управление:\n" +
                "• Геймпад: LB + RB (настраивается)\n" +
                "• Клавиатура: Ctrl+Shift+R",
                "О программе",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка показа диалога 'О программе'");
        }
    }

    private ToolTipIcon GetToolTipIcon(NotificationType type)
    {
        return type switch
        {
            NotificationType.Info => ToolTipIcon.Info,
            NotificationType.Success => ToolTipIcon.Info, 
            NotificationType.Warning => ToolTipIcon.Warning,
            NotificationType.Error => ToolTipIcon.Error,
            _ => ToolTipIcon.Info
        };
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Icon?.Dispose();
                _notifyIcon.ContextMenuStrip?.Dispose();
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка освобождения TrayService");
        }
    }

    #endregion
}