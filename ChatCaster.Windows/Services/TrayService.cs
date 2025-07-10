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
    private bool _hasShownFirstTimeNotification = false;
    private bool _isDisposed = false;

    private const string NormalIconPath = "Resources/mic_normal.ico";

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
        Log.Information("TrayService создан");
    }

    #endregion

    #region Public Methods

    public void Initialize()
    {
        if (_isDisposed)
        {
            Log.Warning("Попытка инициализации уже освобожденного TrayService");
            return;
        }

        Log.Information("Инициализация TrayService");

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

            Log.Information("TrayService успешно инициализирован");
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
                Log.Debug("Уведомления отключены в настройках, пропускаем: {Title} - {Message}", title, message);
                return;
            }

            if (_notifyIcon == null || _isDisposed)
            {
                Log.Warning("Попытка показа уведомления при неинициализированном TrayService");
                return;
            }

            var toolTipIcon = GetToolTipIcon(type);
            
            Log.Information("Показываем уведомление [{Type}]: {Title} - {Message}", type, title, message);
            
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
                Log.Warning("Попытка обновления статуса при неинициализированном TrayService");
                return;
            }

            // Ограничиваем длину tooltip (Windows лимит ~127 символов)
            string tooltipText = status.Length > 120 ? status.Substring(0, 117) + "..." : status;
            _notifyIcon.Text = tooltipText;
            
            Log.Debug("Статус трея обновлен: {Status}", status);
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
        Log.Debug("Показано уведомление о первом сворачивании в трей");
    }

    #endregion

    #region Private Methods

    private Icon LoadIcon()
    {
        try
        {
            if (File.Exists(NormalIconPath))
            {
                return new Icon(NormalIconPath);
            }
            else
            {
                Log.Warning("Файл иконки не найден: {IconPath}, используем системную", NormalIconPath);
                return SystemIcons.Application;
            }
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
            Log.Warning("Попытка создания контекстного меню для null NotifyIcon");
            return;
        }

        try
        {
            var contextMenu = new ContextMenuStrip();
            
            contextMenu.Items.Add("📋 Показать окно", null, (s, e) => ShowMainWindowRequested?.Invoke(this, EventArgs.Empty));
            contextMenu.Items.Add("⚙️ Настройки", null, (s, e) => ShowSettingsRequested?.Invoke(this, EventArgs.Empty));
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("ℹ️ О программе", null, (s, e) => ShowAboutDialog());
            contextMenu.Items.Add("❌ Выход", null, (s, e) => ExitApplicationRequested?.Invoke(this, EventArgs.Empty));

            _notifyIcon.ContextMenuStrip = contextMenu;
            
            Log.Debug("Контекстное меню создано");
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
            Log.Debug("Двойной клик по иконке трея");
            ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обработки двойного клика по трею");
        }
    }

    // Единственная "бизнес-логика" которая остается
    private void ShowAboutDialog()
    {
        try
        {
            System.Windows.MessageBox.Show(
                "ChatCaster v1.0.0\n\n" +
                "Голосовой ввод для игр с поддержкой геймпада\n\n" +
                "Технологии: WPF, NAudio, Whisper.net, XInput\n\n" +
                "Управление:\n" +
                "• Геймпад: LB + RB (настраивается)\n" +
                "• Клавиатура: Ctrl+Shift+R",
                "О программе",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
                
            Log.Debug("Показан диалог 'О программе'");
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
            Log.Debug("TrayService уже был освобожден");
            return;
        }

        try
        {
            Log.Information("Освобождение ресурсов TrayService");

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
            
            Log.Information("TrayService успешно освобожден");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка освобождения TrayService");
        }
    }

    #endregion
}