using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using ChatCaster.Core.Models;
using System.IO;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Простой сервис для работы с System Tray
/// </summary>
public class TrayService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly string _normalIconPath = "Resources/mic_normal.ico";
    private bool _hasShownTrayNotification = false;
    private bool _isDisposed = false;

    // Ссылка на главное окно для прямого вызова методов
    private readonly Window _mainWindow;
    
    // ✅ ДОБАВЛЯЕМ: Ссылка на конфигурацию для проверки настроек
    private AppConfig? _config;

    public bool IsVisible => _notifyIcon?.Visible == true;

    public TrayService(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }

    // ✅ ДОБАВЛЯЕМ: Метод для установки конфигурации
    public void SetConfig(AppConfig config)
    {
        _config = config;
    }

    public void Initialize()
    {
        try
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = File.Exists(_normalIconPath) 
                    ? new Icon(_normalIconPath) 
                    : SystemIcons.Application,
                Text = "ChatCaster - Готов к работе",
                Visible = true
            };

            Console.WriteLine($"[TRAY] NotifyIcon создан");

            // Двойной клик - показать главное окно
            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();

            CreateContextMenu();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TRAY] ОШИБКА создания NotifyIcon: {ex.Message}");
            try
            {
                _notifyIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Application,
                    Text = "ChatCaster - Готов к работе",
                    Visible = true
                };
                CreateContextMenu();
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"[TRAY] КРИТИЧЕСКАЯ ОШИБКА: {fallbackEx.Message}");
            }
        }
    }

    private void CreateContextMenu()
    {
        if (_notifyIcon == null) return;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("📋 Показать окно", null, (s, e) => ShowMainWindow());
        contextMenu.Items.Add("⚙️ Настройки", null, (s, e) => ShowSettings());
        contextMenu.Items.Add("-"); // Разделитель
        contextMenu.Items.Add("ℹ️ О программе", null, (s, e) => ShowAbout());
        contextMenu.Items.Add("❌ Выход", null, (s, e) => ExitApplication());

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void ShowMainWindow()
    {
        try
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.ShowInTaskbar = true;
            _mainWindow.Activate();
            _mainWindow.Focus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка показа окна: {ex.Message}");
        }
    }

    private void ShowSettings()
    {
        try
        {
            ShowMainWindow();
            // Вызываем метод главного окна напрямую
            if (_mainWindow is ChatCaster.Windows.Views.ChatCasterWindow window)
            {
                window.NavigateToSettings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка открытия настроек: {ex.Message}");
        }
    }
    
    public void ShowNotification(string title, string message, int timeout = 3000)
    {
        try
        {
            // ✅ ИСПРАВЛЕНИЕ: Проверяем настройки перед показом уведомления
            if (_config?.System?.ShowNotifications != true)
            {
                Console.WriteLine($"🔕 Уведомления отключены в настройках, пропускаем: {title} - {message}");
                return;
            }

            if (_notifyIcon != null)
            {
                Console.WriteLine($"🔔 Показываем уведомление: {title} - {message}");
                _notifyIcon.ShowBalloonTip(timeout, title, message, ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка показа уведомления: {ex.Message}");
        }
    }

    private void ShowAbout()
    {
        System.Windows.MessageBox.Show("ChatCaster v1.0.0\n\nГолосовой ввод для игр с поддержкой геймпада\n\n" +
                        "Технологии: WPF, NAudio, Whisper.net, XInput\n\n" +
                        "Управление:\n" +
                        "• Геймпад: LB + RB (настраивается)\n" +
                        "• Клавиатура: Ctrl+Shift+R",
            "О программе", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ExitApplication()
    {
        try
        {
            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка выхода: {ex.Message}");
            Environment.Exit(0);
        }
    }

    public void UpdateStatus(string status)
    {
        try
        {
            if (_notifyIcon != null)
            {
                string trayText = status.Length > 120 ? status.Substring(0, 117) + "..." : status;
                _notifyIcon.Text = trayText;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обновления статуса: {ex.Message}");
        }
    }

    public void ShowFirstTimeNotification(AppConfig config)
    {
        // ✅ ИСПРАВЛЕНИЕ: Эта функция уже правильно проверяет настройки
        if (!_hasShownTrayNotification && config.System.ShowNotifications)
        {
            _notifyIcon?.ShowBalloonTip(3000, "ChatCaster", 
                "Приложение свернуто в системный трей. Двойной клик для возврата.", 
                ToolTipIcon.Info);
            _hasShownTrayNotification = true;
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка закрытия TrayService: {ex.Message}");
            }
            _isDisposed = true;
        }
    }
}