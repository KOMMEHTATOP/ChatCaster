using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using Serilog;
using Application = System.Windows.Application;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Сервис для работы с системным треем
/// Слабо связан с UI через события
/// </summary>
public class TrayService : ITrayService, IDisposable
{
    #region Fields

    private NotifyIcon? _notifyIcon;
    private AppConfig? _config;
    private bool _hasShownFirstTimeNotification = false;
    private bool _isDisposed = false;

    // Константы
    private const string NormalIconPath = "Resources/mic_normal.ico";
    private const string DefaultTooltip = "ChatCaster - Готов к работе";

    #endregion

    #region Properties

    public bool IsVisible => _notifyIcon?.Visible == true;

    #endregion

    #region Events

    public event EventHandler? ShowMainWindowRequested;
    public event EventHandler? ShowSettingsRequested;
    public event EventHandler? ExitApplicationRequested;

    #endregion

    #region Constructor & Initialization

    public TrayService()
    {
        Log.Information($"TrayService #{GetHashCode()} создан");

        // Подписываемся на события закрытия приложения для гарантированной очистки
        Application.Current.Exit += (s, e) => Dispose();
        Application.Current.SessionEnding += (s, e) => Dispose();
        AppDomain.CurrentDomain.ProcessExit += (s, e) => Dispose();
    }

    public void Initialize()
    {
        if (_isDisposed)
        {
            Log.Warning($"Попытка инициализации уже освобожденного TrayService #{GetHashCode()}");
            return;
        }

        Log.Information($"Инициализация TrayService #{GetHashCode()}");

        try
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = DefaultTooltip,
                Visible = true
            };

            // Двойной клик - показать главное окно
            _notifyIcon.DoubleClick += OnDoubleClick;

            CreateContextMenu();

            Log.Information($"TrayService #{GetHashCode()} успешно инициализирован");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка инициализации TrayService #{GetHashCode()}");
            
            // Fallback для критической ошибки
            try
            {
                _notifyIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Application,
                    Text = DefaultTooltip,
                    Visible = true
                };
                CreateContextMenu();
                Log.Warning($"TrayService #{GetHashCode()} инициализирован с fallback иконкой");
            }
            catch (Exception fallbackEx)
            {
                Log.Fatal(fallbackEx, $"Критическая ошибка инициализации TrayService #{GetHashCode()}");
            }
        }
    }

    /// <summary>
    /// Устанавливает конфигурацию для проверки настроек уведомлений
    /// </summary>
    public void SetConfig(AppConfig config)
    {
        _config = config;
        Log.Debug($"Конфигурация установлена в TrayService #{GetHashCode()}");
    }

    #endregion

    #region Icon Management

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

    #endregion

    #region Context Menu

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
            
            // 📋 Показать окно
            contextMenu.Items.Add("📋 Показать окно", null, (s, e) => OnShowMainWindowRequested());
            
            // ⚙️ Настройки
            contextMenu.Items.Add("⚙️ Настройки", null, (s, e) => OnShowSettingsRequested());
            
            // Разделитель
            contextMenu.Items.Add(new ToolStripSeparator());
            
            // ℹ️ О программе
            contextMenu.Items.Add("ℹ️ О программе", null, (s, e) => ShowAboutDialog());
            
            // ❌ Выход
            contextMenu.Items.Add("❌ Выход", null, (s, e) => OnExitApplicationRequested());

            _notifyIcon.ContextMenuStrip = contextMenu;
            
            Log.Debug("Контекстное меню создано");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка создания контекстного меню");
        }
    }

    #endregion

    #region Public Methods

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info, int timeout = 3000)
    {
        try
        {
            // Проверяем настройки перед показом уведомления
            if (_config?.System?.ShowNotifications != true)
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
        if (_hasShownFirstTimeNotification || _config?.System?.ShowNotifications != true)
        {
            return;
        }

        ShowNotification(
            "ChatCaster", 
            "Приложение свернуто в системный трей. Двойной клик для возврата.", 
            NotificationType.Info);
            
        _hasShownFirstTimeNotification = true;
        Log.Debug("Показано уведомление о первом сворачивании в трей");
    }

    #endregion

    #region Event Handlers

    private void OnDoubleClick(object? sender, EventArgs e)
    {
        try
        {
            Log.Debug("Двойной клик по иконке трея");
            OnShowMainWindowRequested();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка обработки двойного клика по трею");
        }
    }

    private void OnShowMainWindowRequested()
    {
        try
        {
            Log.Debug("Запрос показа главного окна из трея");
            ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка вызова события ShowMainWindowRequested");
        }
    }

    private void OnShowSettingsRequested()
    {
        try
        {
            Log.Debug("Запрос открытия настроек из трея");
            ShowSettingsRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка вызова события ShowSettingsRequested");
        }
    }

    private void OnExitApplicationRequested()
    {
        try
        {
            Log.Debug("Запрос выхода из приложения из трея");
            ExitApplicationRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка вызова события ExitApplicationRequested");
        }
    }

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

    #endregion

    #region Helper Methods

    private ToolTipIcon GetToolTipIcon(NotificationType type)
    {
        return type switch
        {
            NotificationType.Info => ToolTipIcon.Info,
            NotificationType.Success => ToolTipIcon.Info, // Windows не имеет Success иконки
            NotificationType.Warning => ToolTipIcon.Warning,
            NotificationType.Error => ToolTipIcon.Error,
            _ => ToolTipIcon.Info
        };
    }

    #endregion

    #region Disposal

    ~TrayService()
    {
        // ✅ ДОБАВЛЕНО: Финализатор для гарантированной очистки
        Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            Log.Debug($"TrayService #{GetHashCode()} уже был освобожден");
            return;
        }

        try
        {
            Log.Information($"Освобождение ресурсов TrayService #{GetHashCode()}");

            if (_notifyIcon != null)
            {
                Log.Debug($"Скрываем NotifyIcon #{GetHashCode()}");
                _notifyIcon.Visible = false;
                
                Log.Debug($"Освобождаем иконку #{GetHashCode()}");
                _notifyIcon.Icon?.Dispose();
                
                Log.Debug($"Освобождаем контекстное меню #{GetHashCode()}");
                _notifyIcon.ContextMenuStrip?.Dispose();
                
                Log.Debug($"Освобождаем NotifyIcon #{GetHashCode()}");
                _notifyIcon.Dispose();
                _notifyIcon = null;
                
                // ✅ ДОБАВЛЕНО: Принудительное обновление системного трея
                try
                {
                    // Метод 1: Win32 API
                    RefreshSystemTray();
                    
                    // Метод 2: Небольшая задержка + повторное обновление
                    Task.Delay(100).ContinueWith(_ => 
                    {
                        try
                        {
                            // Принуждаем сборщик мусора для окончательной очистки
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                            
                            // Повторное обновление трея
                            RefreshSystemTray();
                            Log.Debug("Повторное обновление системного трея выполнено");
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Ошибка повторного обновления трея");
                        }
                    });
                    
                    Log.Debug("Системный трей принудительно обновлен");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Не удалось обновить системный трей");
                }
            }

            _isDisposed = true;
            
            // ✅ ДОБАВЛЕНО: Подавляем финализатор если Dispose вызван явно
            GC.SuppressFinalize(this);
            
            Log.Information($"TrayService #{GetHashCode()} успешно освобожден");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Ошибка освобождения TrayService #{GetHashCode()}");
        }
    }

    /// <summary>
    /// Принудительно обновляет системный трей Windows
    /// </summary>
    private static void RefreshSystemTray()
    {
        try
        {
            // Получаем handle окна системного трея
            var systemTrayHandle = FindWindow("Shell_TrayWnd", null);
            if (systemTrayHandle != IntPtr.Zero)
            {
                var trayNotifyHandle = FindWindowEx(systemTrayHandle, IntPtr.Zero, "TrayNotifyWnd", null);
                if (trayNotifyHandle != IntPtr.Zero)
                {
                    var sysPagerHandle = FindWindowEx(trayNotifyHandle, IntPtr.Zero, "SysPager", null);
                    if (sysPagerHandle != IntPtr.Zero)
                    {
                        var toolbarHandle = FindWindowEx(sysPagerHandle, IntPtr.Zero, "ToolbarWindow32", null);
                        if (toolbarHandle != IntPtr.Zero)
                        {
                            // Отправляем сообщение для обновления трея
                            SendMessage(toolbarHandle, 0x001A, IntPtr.Zero, IntPtr.Zero); // WM_SETTINGCHANGE
                            Log.Debug("Отправлено сообщение обновления системного трея");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при обновлении системного трея");
        }
    }

    // ✅ ДОБАВЛЕНО: Win32 API для работы с системным треем
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    #endregion
}