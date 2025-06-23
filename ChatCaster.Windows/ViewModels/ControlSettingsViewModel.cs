using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels.Base;
using NHotkey;
using NHotkey.Wpf;

// Алиасы для разделения WPF и Core моделей
using WpfKey = System.Windows.Input.Key;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;
using CoreKey = ChatCaster.Core.Models.Key;
using CoreModifierKeys = ChatCaster.Core.Models.ModifierKeys;

namespace ChatCaster.Windows.ViewModels.Settings
{
    public partial class ControlSettingsViewModel : BaseSettingsViewModel
    {
        #region Private Services
        private readonly GamepadService? _gamepadService;
        private readonly SystemIntegrationService? _systemService;
        #endregion

        #region Observable Properties

        [ObservableProperty]
        private string _gamepadComboText = "LB + RB";

        [ObservableProperty]
        private string _keyboardComboText = "Ctrl + Shift + R";

        [ObservableProperty]
        private bool _isWaitingForGamepadInput = false;

        [ObservableProperty]
        private bool _isWaitingForKeyboardInput = false;

        [ObservableProperty]
        private string _gamepadStatusText = "Геймпад не найден";

        [ObservableProperty]
        private string _gamepadStatusColor = "#f44336";

        #endregion

        #region Commands

        [RelayCommand]
        private async Task StartGamepadCapture()
        {
            if (IsWaitingForGamepadInput) return;

            try
            {
                await StartGamepadCaptureInternal();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка захвата геймпада: {ex.Message}";
                Console.WriteLine($"❌ [{GetType().Name}] Ошибка захвата геймпада: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task StartKeyboardCapture()
        {
            if (IsWaitingForKeyboardInput) return;

            try
            {
                await StartKeyboardCaptureInternal();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка захвата клавиатуры: {ex.Message}";
                Console.WriteLine($"❌ [{GetType().Name}] Ошибка захвата клавиатуры: {ex.Message}");
            }
        }

        #endregion

        #region Private Fields for Capture State
        private readonly Dictionary<string, (WpfKey Key, WpfModifierKeys Modifiers)> _registeredHotkeys = new();
        private readonly string _tempHotkeyName = "TempCapture";
        private DispatcherTimer? _holdTimer;
        private WpfModifierKeys _capturedModifiers;
        private WpfKey _capturedKey;
        private bool _captureCompleted = false;
        #endregion

        #region Constructor
        public ControlSettingsViewModel(
            ConfigurationService? configurationService,
            ServiceContext? serviceContext,
            GamepadService? gamepadService,
            SystemIntegrationService? systemService) : base(configurationService, serviceContext)
        {
            _gamepadService = gamepadService;
            _systemService = systemService;
        }
        #endregion

        #region BaseSettingsViewModel Implementation

        protected override async Task LoadPageSpecificSettingsAsync()
        {
            if (_serviceContext?.Config == null) return;

            var config = _serviceContext.Config;

            // Загружаем настройки геймпада
            var gamepadShortcut = config.Input.GamepadShortcut;
            GamepadComboText = FormatGamepadShortcut(gamepadShortcut);

            // Загружаем настройки клавиатуры
            var keyboardShortcut = config.Input.KeyboardShortcut;
            if (keyboardShortcut != null)
            {
                KeyboardComboText = FormatKeyboardShortcut(keyboardShortcut);
                Console.WriteLine($"Настройки загружены. Хоткей: {FormatKeyboardShortcut(keyboardShortcut)}");
            }

            await CheckGamepadStatus();
        }

        protected override async Task ApplySettingsToConfigAsync(AppConfig config)
        {
            // Настройки применяются в реальном времени через события захвата
            // Конфигурация уже обновлена в методах захвата
            await Task.CompletedTask;
        }

        protected override async Task ApplySettingsToServicesAsync()
        {
            if (_gamepadService != null && _serviceContext?.Config != null)
            {
                await _gamepadService.StopMonitoringAsync();
                await _gamepadService.StartMonitoringAsync(_serviceContext.Config.Input);
            }
        }

        protected override async Task InitializePageSpecificDataAsync()
        {
            await CheckGamepadStatus();
        }

        public override void SubscribeToUIEvents()
        {
            // Подписываемся на события геймпада для захвата ввода
            if (_gamepadService != null)
            {
                _gamepadService.GamepadConnected += OnGamepadConnected;
                _gamepadService.GamepadDisconnected += OnGamepadDisconnected;
                _gamepadService.ShortcutPressed += OnGamepadShortcutPressed;
            }
        }

        protected override void UnsubscribeFromUIEvents()
        {
            // Отписываемся от событий геймпада
            if (_gamepadService != null)
            {
                _gamepadService.GamepadConnected -= OnGamepadConnected;
                _gamepadService.GamepadDisconnected -= OnGamepadDisconnected;
                _gamepadService.ShortcutPressed -= OnGamepadShortcutPressed;
            }
        }

        protected override void CleanupPageSpecific()
        {
            // Останавливаем захват если активен
            StopKeyboardCapture();
            StopGamepadCapture();
        }

        #endregion

        #region Gamepad Event Handlers

        private void OnGamepadConnected(object? sender, GamepadConnectedEvent e)
        {
            GamepadStatusText = $"Геймпад подключен: {e.GamepadInfo.Name}";
            GamepadStatusColor = "#4caf50";
            Console.WriteLine($"Геймпад подключен: {e.GamepadInfo.Name}");
        }

        private void OnGamepadDisconnected(object? sender, GamepadDisconnectedEvent e)
        {
            GamepadStatusText = "Геймпад отключен";
            GamepadStatusColor = "#f44336";
            Console.WriteLine("Геймпад отключен");
        }

        private void OnGamepadShortcutPressed(object? sender, GamepadShortcutPressedEvent e)
        {
            Console.WriteLine($"Тестовое нажатие геймпада: {FormatGamepadShortcut(e.Shortcut)}");
        }

        #endregion

        #region Keyboard Capture Methods

        private async Task StartKeyboardCaptureInternal()
        {
            IsWaitingForKeyboardInput = true;
            KeyboardComboText = "Нажмите любую комбинацию клавиш...";
            StatusMessage = "Ожидание нажатия клавиш...";

            // Регистрируем временные обработчики для всех возможных комбинаций
            RegisterAllPossibleHotkeys();
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
                        _registeredHotkeys[hotkeyName] = (key, modifier);
                    }
                    catch
                    {
                        // Игнорируем конфликты с уже зарегистрированными хотkeys
                    }
                }
            }
        }

        private async void OnTempHotkeyPressed(object? sender, HotkeyEventArgs e)
        {
            if (!IsWaitingForKeyboardInput) return;

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
                    KeyboardComboText = comboText;

                    StopKeyboardCapture();

                    if (_serviceContext?.Config != null)
                    {
                        _serviceContext.Config.Input.KeyboardShortcut = keyboardShortcut;
                        await OnUISettingChangedAsync();

                        // Регистрируем хоткей глобально
                        if (_systemService != null)
                        {
                            Console.WriteLine($"Регистрируем глобальный хоткей: {comboText}");
                            bool registered = await _systemService.RegisterGlobalHotkeyAsync(keyboardShortcut);
                            StatusMessage = registered ? "Хоткей зарегистрирован успешно" : "Ошибка регистрации хоткея";
                        }

                        Console.WriteLine($"Сохранена комбинация: {comboText}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения комбинации: {ex.Message}");
                KeyboardComboText = "Ошибка сохранения";
                StopKeyboardCapture();
            }

            e.Handled = true;
        }

        private void StopKeyboardCapture()
        {
            IsWaitingForKeyboardInput = false;
            _captureCompleted = false;
            _holdTimer?.Stop();

            // Удаляем временные хотkeys
            foreach (var hotkeyName in _registeredHotkeys.Keys)
            {
                try
                {
                    HotkeyManager.Current.Remove(hotkeyName);
                }
                catch
                {
                    // Игнорируем ошибки при удалении
                }
            }
            _registeredHotkeys.Clear();
        }

        #endregion

        #region Gamepad Capture Methods

        private async Task StartGamepadCaptureInternal()
        {
            IsWaitingForGamepadInput = true;
            GamepadComboText = "Нажмите комбинацию на геймпаде и удерживайте 2 секунды...";
            StatusMessage = "Ожидание нажатия геймпада...";

            // Пока простой таймаут - можно расширить логику захвата геймпада
            await Task.Delay(10000);

            if (IsWaitingForGamepadInput)
            {
                StopGamepadCapture();
                GamepadComboText = "Время ожидания истекло";

                await Task.Delay(2000);

                // Возвращаем старое значение
                if (_serviceContext?.Config?.Input?.GamepadShortcut != null)
                {
                    GamepadComboText = FormatGamepadShortcut(_serviceContext.Config.Input.GamepadShortcut);
                }
            }
        }

        private void StopGamepadCapture()
        {
            IsWaitingForGamepadInput = false;
        }

        #endregion

        #region Helper Methods

        private async Task CheckGamepadStatus()
        {
            try
            {
                if (_gamepadService == null)
                {
                    GamepadStatusText = "Сервис геймпада недоступен";
                    GamepadStatusColor = "#f44336";
                    return;
                }

                var gamepads = await _gamepadService.GetConnectedGamepadsAsync();
                int gamepadCount = gamepads.Count();

                if (gamepadCount > 0)
                {
                    var gamepadNames = string.Join(", ", gamepads.Select(g => g.Name));
                    GamepadStatusText = $"Геймпад подключен: {gamepadNames}";
                    GamepadStatusColor = "#4caf50";
                }
                else
                {
                    GamepadStatusText = "Геймпад не найден";
                    GamepadStatusColor = "#f44336";
                }
            }
            catch (Exception ex)
            {
                GamepadStatusText = $"Ошибка проверки геймпада: {ex.Message}";
                GamepadStatusColor = "#f44336";
            }
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

        /// <summary>
        /// Обертка для автоприменения настроек
        /// </summary>
        private async Task OnUISettingChangedAsync()
        {
            if (IsLoadingUI) return;

            HasUnsavedChanges = true;
            await ApplySettingsAsync();
        }

        #endregion
    }
}