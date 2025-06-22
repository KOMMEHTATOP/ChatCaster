using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.Windows.Services;
using ChatCaster.Core.Events;
using NHotkey;
using NHotkey.Wpf;
using System.Windows.Threading;

// Алиасы для разделения WPF и Core моделей
using WpfKey = System.Windows.Input.Key;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;
using CoreKey = ChatCaster.Core.Models.Key;
using CoreModifierKeys = ChatCaster.Core.Models.ModifierKeys;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class ControlSettingsView : Page
{
    private readonly GamepadService? _gamepadService;
    private readonly SystemIntegrationService? _systemService;
    private readonly ConfigurationService? _configurationService;
    private readonly ServiceContext? _serviceContext;

    private bool _isLoadingUI = false; // Флаг чтобы не применять настройки во время загрузки UI
    
    private bool _isSettingsLoaded = false;

    // Состояние ожидания ввода
    private bool _waitingForGamepadInput = false;
    private bool _waitingForKeyboardInput = false;
    
    // Для захвата клавиатуры через NHotkey
    private string _tempHotkeyName = "TempCapture";
    private readonly Dictionary<string, (WpfKey Key, WpfModifierKeys Modifiers)> _registeredHotkeys = new();

    public ControlSettingsView()
    {
        InitializeComponent();
        LoadInitialData();
    }

    // Конструктор с сервисами
    public ControlSettingsView(GamepadService gamepadService, 
                              SystemIntegrationService systemService, 
                              ConfigurationService configurationService,
                              ServiceContext serviceContext) : this()
    {
        _gamepadService = gamepadService;
        _systemService = systemService;
        _configurationService = configurationService;
        _serviceContext = serviceContext;
        
        // Подписываемся на события геймпада для захвата ввода
        if (_gamepadService != null)
        {
            _gamepadService.GamepadConnected += OnGamepadConnected;
            _gamepadService.GamepadDisconnected += OnGamepadDisconnected;
            _gamepadService.ShortcutPressed += OnGamepadShortcutPressed;
        }
        
        if (!_isSettingsLoaded)
        {
            _ = LoadCurrentSettings();
            _isSettingsLoaded = true;
        }

    }

    private void LoadInitialData()
    {
        _isLoadingUI = true;
        
        // Устанавливаем значения по умолчанию
        GamepadComboText.Text = "LB + RB";
        KeyboardComboText.Text = "Ctrl + Shift + R";
        
        _isLoadingUI = false;
    }

// И в LoadCurrentSettings() добавляем регистрацию при загрузке
    private async Task LoadCurrentSettings()
    {
        try
        {
            _isLoadingUI = true;
        
            if (_serviceContext?.Config == null) return;

            var config = _serviceContext.Config;

            // Загружаем настройки геймпада
            var gamepadShortcut = config.Input.GamepadShortcut;
            GamepadComboText.Text = FormatGamepadShortcut(gamepadShortcut);

            // Загружаем настройки клавиатуры
            var keyboardShortcut = config.Input.KeyboardShortcut;
            if (keyboardShortcut != null)
            {
                KeyboardComboText.Text = FormatKeyboardShortcut(keyboardShortcut);
            
                // ИСПРАВЛЕНИЕ: НЕ регистрируем хоткей здесь!
                // Хоткей уже зарегистрирован в ChatCasterWindow_Loaded
                Console.WriteLine($"Настройки загружены. Хоткей: {FormatKeyboardShortcut(keyboardShortcut)} (регистрация не требуется)");
            }

            Console.WriteLine("Настройки управления загружены");
            await CheckGamepadStatus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки настроек управления: {ex.Message}");
        }
        finally
        {
            _isLoadingUI = false;
        }
    }
    
    // Обработчики кликов на поля комбинаций
    private async void GamepadComboBorder_Click(object sender, MouseButtonEventArgs e)
    {
        if (_waitingForGamepadInput) return;
        
        try
        {
            await StartGamepadCapture();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка захвата геймпада: {ex.Message}");
        }
    }

    private async void KeyboardComboBorder_Click(object sender, MouseButtonEventArgs e)
    {
        if (_waitingForKeyboardInput) return;
        
        try
        {
            await StartKeyboardCapture();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка захвата клавиатуры: {ex.Message}");
        }
    }

    private async Task StartKeyboardCapture()
    {
        try
        {
            _waitingForKeyboardInput = true;
            
            KeyboardComboText.Text = "Нажмите любую комбинацию клавиш...";
            KeyboardComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
            
            // Регистрируем временный обработчик для ВСЕХ возможных комбинаций
            // Мы будем перехватывать события через глобальный хук
            Console.WriteLine("Ожидание нажатия клавиш...");
            
            // Подписываемся на все возможные комбинации через NHotkey
            RegisterAllPossibleHotkeys();
            
        }
        catch (Exception ex)
        {
            KeyboardComboText.Text = $"Ошибка: {ex.Message}";
            KeyboardComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
        }
    }

    private void RegisterAllPossibleHotkeys()
    {
        _registeredHotkeys.Clear();
        
        // Список часто используемых клавиш для быстрой настройки
        var commonKeys = new[]
        {
            WpfKey.F1, WpfKey.F2, WpfKey.F3, WpfKey.F4, WpfKey.F5, WpfKey.F6,
            WpfKey.F7, WpfKey.F8, WpfKey.F9, WpfKey.F10, WpfKey.F11, WpfKey.F12,
            WpfKey.NumPad0, WpfKey.NumPad1, WpfKey.NumPad2, WpfKey.NumPad3,
            WpfKey.Insert, WpfKey.Delete, WpfKey.Home, WpfKey.End,
            WpfKey.PageUp, WpfKey.PageDown
        };

        var modifiers = new[]
        {
            WpfModifierKeys.None,
            WpfModifierKeys.Control,
            WpfModifierKeys.Shift,
            WpfModifierKeys.Alt,
            WpfModifierKeys.Control | WpfModifierKeys.Shift,
            WpfModifierKeys.Control | WpfModifierKeys.Alt,
            WpfModifierKeys.Shift | WpfModifierKeys.Alt
        };

        int hotkeyIndex = 0;
        foreach (var modifier in modifiers)
        {
            foreach (var key in commonKeys)
            {
                try
                {
                    var hotkeyName = $"{_tempHotkeyName}_{hotkeyIndex++}";
                    HotkeyManager.Current.AddOrReplace(hotkeyName, key, modifier, OnTempHotkeyPressed);
                    
                    // Сохраняем информацию о зарегистрированном хоткее
                    _registeredHotkeys[hotkeyName] = (key, modifier);
                }
                catch
                {
                    // Игнорируем конфликты с уже зарегистрированными хотkeys
                }
            }
        }
    }

// Также добавляем в OnTempHotkeyPressed()
private async void OnTempHotkeyPressed(object? sender, HotkeyEventArgs e)
{
    if (!_waitingForKeyboardInput) return;

    try
    {
        if (_registeredHotkeys.TryGetValue(e.Name, out var hotkeyInfo))
        {
            var keyboardShortcut = new KeyboardShortcut
            {
                Modifiers = ConvertToCore(hotkeyInfo.Modifiers),
                Key = ConvertToCore(hotkeyInfo.Key)
            };

            var comboText = FormatKeyboardShortcut(keyboardShortcut);
            KeyboardComboText.Text = comboText;
            KeyboardComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGreen);

            StopKeyboardCapture();

            if (_serviceContext?.Config != null)
            {
                _serviceContext.Config.Input.KeyboardShortcut = keyboardShortcut;
                await SaveCurrentSettingsAsync();
                
                // НОВОЕ: Регистрируем хоткей глобально
                if (_systemService != null)
                {
                    Console.WriteLine($"Регистрируем глобальный хоткей: {comboText}");
                    bool registered = await _systemService.RegisterGlobalHotkeyAsync(keyboardShortcut);
                    Console.WriteLine(registered ? "Хоткей зарегистрирован успешно" : "Ошибка регистрации хоткея");
                }
                
                Console.WriteLine($"Сохранена комбинация: {comboText}");
            }

            await Task.Delay(1000);
            KeyboardComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка сохранения комбинации: {ex.Message}");
        KeyboardComboText.Text = "Ошибка сохранения";
        KeyboardComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
        StopKeyboardCapture();
    }

    e.Handled = true;
}

    private async Task StartGamepadCapture()
    {
        _waitingForGamepadInput = true;
        GamepadComboText.Text = "Нажмите комбинацию на геймпаде и удерживайте 2 секунды...";
        GamepadComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
        
        // TODO: Подписаться на события геймпада для захвата
        
        // Пока просто таймаут
        await Task.Delay(10000);
        
        if (_waitingForGamepadInput)
        {
            StopGamepadCapture();
            GamepadComboText.Text = "Время ожидания истекло";
            GamepadComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            
            await Task.Delay(2000);
            
            // Возвращаем старое значение
            if (_serviceContext?.Config?.Input?.GamepadShortcut != null)
            {
                GamepadComboText.Text = FormatGamepadShortcut(_serviceContext.Config.Input.GamepadShortcut);
                GamepadComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
            }
        }
    }

    private DispatcherTimer? _holdTimer;
    private WpfModifierKeys _capturedModifiers;
    private WpfKey _capturedKey;
    private bool _captureCompleted = false; // Флаг успешного захвата

    private void OnCaptureKeyDown(object sender, KeyEventArgs e)
    {
        if (!_waitingForKeyboardInput || _captureCompleted) return;

        // Игнорируем системные клавиши
        if (e.Key == WpfKey.LeftCtrl || e.Key == WpfKey.RightCtrl ||
            e.Key == WpfKey.LeftShift || e.Key == WpfKey.RightShift ||
            e.Key == WpfKey.LeftAlt || e.Key == WpfKey.RightAlt ||
            e.Key == WpfKey.LWin || e.Key == WpfKey.RWin)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var key = e.Key;

        // Проверяем что есть хотя бы один модификатор
        if (modifiers == WpfModifierKeys.None)
        {
            KeyboardComboText.Text = "Нужна комбинация с Ctrl, Shift или Alt";
            KeyboardComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            return;
        }

        _capturedModifiers = modifiers;
        _capturedKey = key;
        _captureCompleted = false;
        
        // Показываем захваченную комбинацию
        var comboText = FormatWpfKeyboardCombo(modifiers, key);
        KeyboardComboText.Text = $"{comboText} (удерживайте...)";
        KeyboardComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightBlue);

        // Запускаем таймер удержания
        _holdTimer?.Stop();
        _holdTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(2000)
        };
        _holdTimer.Tick += (s, args) =>
        {
            _holdTimer.Stop();
            _captureCompleted = true; // Помечаем что захват завершен успешно
            AcceptKeyboardCapture();
        };
        _holdTimer.Start();

        e.Handled = true;
    }

    private void OnCaptureKeyUp(object sender, KeyEventArgs e)
    {
        if (!_waitingForKeyboardInput || _captureCompleted) return; // Не прерываем если захват уже завершен

        // Если отпустили клавишу до завершения таймера - прерываем захват
        _holdTimer?.Stop();
        KeyboardComboText.Text = "Комбинация прервана. Кликните снова для повтора";
        KeyboardComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
        _captureCompleted = false;

        e.Handled = true;
    }

// В методе AcceptKeyboardCapture() добавляем регистрацию хоткея
    private async void AcceptKeyboardCapture()
    {
        try
        {
            var keyboardShortcut = new KeyboardShortcut
            {
                Modifiers = ConvertToCore(_capturedModifiers),
                Key = ConvertToCore(_capturedKey)
            };

            KeyboardComboText.Text = FormatKeyboardShortcut(keyboardShortcut);
            KeyboardComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGreen);

            // Сохраняем в конфигурацию
            if (_serviceContext?.Config != null)
            {
                _serviceContext.Config.Input.KeyboardShortcut = keyboardShortcut;
                await SaveCurrentSettingsAsync();
            
                // НОВОЕ: Регистрируем хоткей глобально
                if (_systemService != null)
                {
                    Console.WriteLine($"Регистрируем глобальный хоткей: {FormatKeyboardShortcut(keyboardShortcut)}");
                    bool registered = await _systemService.RegisterGlobalHotkeyAsync(keyboardShortcut);
                    Console.WriteLine(registered ? "Хоткей зарегистрирован успешно" : "Ошибка регистрации хоткея");
                }
            }

            await Task.Delay(1000);
            KeyboardComboText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка сохранения комбинации: {ex.Message}");
        }
        finally
        {
            StopKeyboardCapture();
        }
    }
    
    private void StopKeyboardCapture()
    {
        _waitingForKeyboardInput = false;
        _captureCompleted = false;
        _holdTimer?.Stop();
        
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.KeyDown -= OnCaptureKeyDown;
            window.KeyUp -= OnCaptureKeyUp;
        }
    }

    private void StopGamepadCapture()
    {
        _waitingForGamepadInput = false;
        // TODO: Отписаться от событий геймпада
    }

    private string FormatWpfKeyboardCombo(WpfModifierKeys modifiers, WpfKey key)
    {
        var parts = new List<string>();
        
        if (modifiers.HasFlag(WpfModifierKeys.Control))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(WpfModifierKeys.Shift))
            parts.Add("Shift");
        if (modifiers.HasFlag(WpfModifierKeys.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(WpfModifierKeys.Windows))
            parts.Add("Win");
            
        parts.Add(GetWpfKeyDisplayName(key));
        
        return string.Join(" + ", parts);
    }

    private string GetWpfKeyDisplayName(WpfKey key)
    {
        return key switch
        {
            WpfKey.D0 => "0", WpfKey.D1 => "1", WpfKey.D2 => "2", WpfKey.D3 => "3", WpfKey.D4 => "4",
            WpfKey.D5 => "5", WpfKey.D6 => "6", WpfKey.D7 => "7", WpfKey.D8 => "8", WpfKey.D9 => "9",
            WpfKey.NumPad0 => "NumPad0", WpfKey.NumPad1 => "NumPad1", WpfKey.NumPad2 => "NumPad2",
            WpfKey.NumPad3 => "NumPad3", WpfKey.NumPad4 => "NumPad4", WpfKey.NumPad5 => "NumPad5",
            WpfKey.NumPad6 => "NumPad6", WpfKey.NumPad7 => "NumPad7", WpfKey.NumPad8 => "NumPad8", WpfKey.NumPad9 => "NumPad9",
            WpfKey.Space => "Space",
            WpfKey.Enter => "Enter",
            WpfKey.Tab => "Tab",
            WpfKey.Escape => "Esc",
            _ => key.ToString()
        };
    }

    private CoreModifierKeys ConvertToCore(WpfModifierKeys wpfModifiers)
    {
        var coreModifiers = CoreModifierKeys.None;
        
        if (wpfModifiers.HasFlag(WpfModifierKeys.Control))
            coreModifiers |= CoreModifierKeys.Control;
        if (wpfModifiers.HasFlag(WpfModifierKeys.Shift))
            coreModifiers |= CoreModifierKeys.Shift;
        if (wpfModifiers.HasFlag(WpfModifierKeys.Alt))
            coreModifiers |= CoreModifierKeys.Alt;
        if (wpfModifiers.HasFlag(WpfModifierKeys.Windows))
            coreModifiers |= CoreModifierKeys.Windows;
            
        return coreModifiers;
    }

    private CoreKey ConvertToCore(WpfKey wpfKey)
    {
        return wpfKey switch
        {
            WpfKey.A => CoreKey.A, WpfKey.B => CoreKey.B, WpfKey.C => CoreKey.C, WpfKey.D => CoreKey.D,
            WpfKey.E => CoreKey.E, WpfKey.F => CoreKey.F, WpfKey.G => CoreKey.G, WpfKey.H => CoreKey.H,
            WpfKey.I => CoreKey.I, WpfKey.J => CoreKey.J, WpfKey.K => CoreKey.K, WpfKey.L => CoreKey.L,
            WpfKey.M => CoreKey.M, WpfKey.N => CoreKey.N, WpfKey.O => CoreKey.O, WpfKey.P => CoreKey.P,
            WpfKey.Q => CoreKey.Q, WpfKey.R => CoreKey.R, WpfKey.S => CoreKey.S, WpfKey.T => CoreKey.T,
            WpfKey.U => CoreKey.U, WpfKey.V => CoreKey.V, WpfKey.W => CoreKey.W, WpfKey.X => CoreKey.X,
            WpfKey.Y => CoreKey.Y, WpfKey.Z => CoreKey.Z,
            WpfKey.D0 => CoreKey.D0, WpfKey.D1 => CoreKey.D1, WpfKey.D2 => CoreKey.D2, WpfKey.D3 => CoreKey.D3,
            WpfKey.D4 => CoreKey.D4, WpfKey.D5 => CoreKey.D5, WpfKey.D6 => CoreKey.D6, WpfKey.D7 => CoreKey.D7,
            WpfKey.D8 => CoreKey.D8, WpfKey.D9 => CoreKey.D9,
            WpfKey.NumPad0 => CoreKey.NumPad0, WpfKey.NumPad1 => CoreKey.NumPad1, WpfKey.NumPad2 => CoreKey.NumPad2,
            WpfKey.NumPad3 => CoreKey.NumPad3, WpfKey.NumPad4 => CoreKey.NumPad4, WpfKey.NumPad5 => CoreKey.NumPad5,
            WpfKey.NumPad6 => CoreKey.NumPad6, WpfKey.NumPad7 => CoreKey.NumPad7, WpfKey.NumPad8 => CoreKey.NumPad8,
            WpfKey.NumPad9 => CoreKey.NumPad9,
            WpfKey.F1 => CoreKey.F1, WpfKey.F2 => CoreKey.F2, WpfKey.F3 => CoreKey.F3, WpfKey.F4 => CoreKey.F4,
            WpfKey.F5 => CoreKey.F5, WpfKey.F6 => CoreKey.F6, WpfKey.F7 => CoreKey.F7, WpfKey.F8 => CoreKey.F8,
            WpfKey.F9 => CoreKey.F9, WpfKey.F10 => CoreKey.F10, WpfKey.F11 => CoreKey.F11, WpfKey.F12 => CoreKey.F12,
            WpfKey.Space => CoreKey.Space,
            WpfKey.Enter => CoreKey.Enter,
            WpfKey.Tab => CoreKey.Tab,
            WpfKey.Escape => CoreKey.Escape,
            WpfKey.Insert => CoreKey.Insert,
            WpfKey.Delete => CoreKey.Delete,
            WpfKey.Home => CoreKey.Home,
            WpfKey.End => CoreKey.End,
            WpfKey.PageUp => CoreKey.PageUp,
            WpfKey.PageDown => CoreKey.PageDown,
            WpfKey.Up => CoreKey.Up,
            WpfKey.Down => CoreKey.Down,
            WpfKey.Left => CoreKey.Left,
            WpfKey.Right => CoreKey.Right,
            _ => CoreKey.A // Fallback
        };
    }

    private async Task SaveCurrentSettingsAsync()
    {
        try
        {
            if (_configurationService == null || _serviceContext?.Config == null) 
                return;

            var config = _serviceContext.Config;

            // Сохраняем конфигурацию
            await _configurationService.SaveConfigAsync(config);

            // Применяем к сервисам только геймпад, если это нужно
            if (_gamepadService != null)
            {
                await _gamepadService.StopMonitoringAsync();
                await _gamepadService.StartMonitoringAsync(config.Input);
            }

            Console.WriteLine("Настройки управления сохранены и применены");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
        }
    }
    
    private async Task CheckGamepadStatus()
    {
        try
        {
            if (_gamepadService == null)
            {
                Console.WriteLine("Сервис геймпада недоступен");
                return;
            }

            var gamepads = await _gamepadService.GetConnectedGamepadsAsync();
            int gamepadCount = gamepads.Count();

            if (gamepadCount > 0)
            {
                var gamepadNames = string.Join(", ", gamepads.Select(g => g.Name));
                Console.WriteLine($"Геймпад подключен: {gamepadNames}");
            }
            else
            {
                Console.WriteLine("Геймпад не найден");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка проверки геймпада: {ex.Message}");
        }
    }

    private void OnGamepadConnected(object? sender, GamepadConnectedEvent e)
    {
        Dispatcher.Invoke(() =>
        {
            Console.WriteLine($"Геймпад подключен: {e.GamepadInfo.Name}");
        });
    }

    private void OnGamepadDisconnected(object? sender, GamepadDisconnectedEvent e)
    {
        Dispatcher.Invoke(() =>
        {
            Console.WriteLine("Геймпад отключен");
        });
    }

    private void OnGamepadShortcutPressed(object? sender, GamepadShortcutPressedEvent e)
    {
        Dispatcher.Invoke(() =>
        {
            Console.WriteLine($"Тестовое нажатие геймпада: {FormatGamepadShortcut(e.Shortcut)}");
        });
    }

    private string FormatGamepadShortcut(GamepadShortcut shortcut)
    {
        if (shortcut.RequireBothButtons)
        {
            return $"{GetButtonDisplayName(shortcut.PrimaryButton)} + {GetButtonDisplayName(shortcut.SecondaryButton)}";
        }
        else
        {
            return $"{GetButtonDisplayName(shortcut.PrimaryButton)} или {GetButtonDisplayName(shortcut.SecondaryButton)}";
        }
    }

    private string FormatKeyboardShortcut(KeyboardShortcut shortcut)
    {
        var parts = new List<string>();
        
        if (shortcut.Modifiers.HasFlag(CoreModifierKeys.Control))
            parts.Add("Ctrl");
        if (shortcut.Modifiers.HasFlag(CoreModifierKeys.Shift))
            parts.Add("Shift");
        if (shortcut.Modifiers.HasFlag(CoreModifierKeys.Alt))
            parts.Add("Alt");
        if (shortcut.Modifiers.HasFlag(CoreModifierKeys.Windows))
            parts.Add("Win");
            
        parts.Add(GetKeyDisplayName(shortcut.Key));
        
        return string.Join(" + ", parts);
    }

    private string GetButtonDisplayName(GamepadButton button)
    {
        return button switch
        {
            GamepadButton.A => "A",
            GamepadButton.B => "B",
            GamepadButton.X => "X",
            GamepadButton.Y => "Y",
            GamepadButton.LeftBumper => "LB",
            GamepadButton.RightBumper => "RB",
            GamepadButton.LeftTrigger => "LT",
            GamepadButton.RightTrigger => "RT",
            GamepadButton.Back => "Back",
            GamepadButton.Start => "Start",
            GamepadButton.LeftStick => "LS",
            GamepadButton.RightStick => "RS",
            GamepadButton.DPadUp => "D-Pad ↑",
            GamepadButton.DPadDown => "D-Pad ↓",
            GamepadButton.DPadLeft => "D-Pad ←",
            GamepadButton.DPadRight => "D-Pad →",
            GamepadButton.Guide => "Guide",
            GamepadButton.Paddle1 => "Paddle1",
            GamepadButton.Paddle2 => "Paddle2",
            GamepadButton.Paddle3 => "Paddle3",
            GamepadButton.Paddle4 => "Paddle4",
            _ => button.ToString()
        };
    }

    private string GetKeyDisplayName(CoreKey key)
    {
        return key switch
        {
            CoreKey.D0 => "0", CoreKey.D1 => "1", CoreKey.D2 => "2", CoreKey.D3 => "3", CoreKey.D4 => "4",
            CoreKey.D5 => "5", CoreKey.D6 => "6", CoreKey.D7 => "7", CoreKey.D8 => "8", CoreKey.D9 => "9",
            CoreKey.NumPad0 => "NumPad0", CoreKey.NumPad1 => "NumPad1", CoreKey.NumPad2 => "NumPad2",
            CoreKey.NumPad3 => "NumPad3", CoreKey.NumPad4 => "NumPad4", CoreKey.NumPad5 => "NumPad5",
            CoreKey.NumPad6 => "NumPad6", CoreKey.NumPad7 => "NumPad7", CoreKey.NumPad8 => "NumPad8", CoreKey.NumPad9 => "NumPad9",
            CoreKey.Space => "Space",
            CoreKey.Enter => "Enter",
            CoreKey.Tab => "Tab",
            CoreKey.Escape => "Esc",
            _ => key.ToString()
        };
    }

    // Cleanup при выгрузке страницы
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Останавливаем захват если активен
            StopKeyboardCapture();
            StopGamepadCapture();
            
            // Отписываемся от событий геймпада
            if (_gamepadService != null)
            {
                _gamepadService.GamepadConnected -= OnGamepadConnected;
                _gamepadService.GamepadDisconnected -= OnGamepadDisconnected;
                _gamepadService.ShortcutPressed -= OnGamepadShortcutPressed;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при выгрузке ControlSettingsView: {ex.Message}");
        }
    }
}