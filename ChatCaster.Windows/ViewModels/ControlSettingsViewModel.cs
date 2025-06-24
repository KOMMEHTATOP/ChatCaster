using ChatCaster.Core.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.Services.GamepadService;
using ChatCaster.Windows.ViewModels.Base;
using NHotkey;
using NHotkey.Wpf;
using System.Windows;

// Алиасы для разделения WPF и Core моделей
using WpfKey = System.Windows.Input.Key;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;
using CoreKey = ChatCaster.Core.Models.Key;
using CoreModifierKeys = ChatCaster.Core.Models.ModifierKeys;

namespace ChatCaster.Windows.ViewModels
{
    public partial class ControlSettingsViewModel : BaseSettingsViewModel
    {
        #region Private Services

        private readonly MainGamepadService _gamepadService;
        private readonly SystemIntegrationService _systemService;
        private GamepadCaptureService? _gamepadCaptureService;

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
        
        [ObservableProperty]
        private string _gamepadComboTextColor = "White";

        [ObservableProperty]
        private string _keyboardComboTextColor = "White"; 

        [ObservableProperty]
        private int _gamepadCaptureTimeLeft = 0; 

        [ObservableProperty]
        private bool _showGamepadTimer = false; 
        
        [ObservableProperty]
        private int _keyboardCaptureTimeLeft = 0; 

        [ObservableProperty]
        private bool _showKeyboardTimer = false;

        private Timer? _keyboardCaptureTimer;
        private string _originalKeyboardComboText = "";

        private Timer? _gamepadCaptureTimer;
        private string _originalGamepadComboText = "";
        private const int CAPTURE_TIMEOUT_SECONDS = 5;

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
            }
        }

        #endregion

        #region Private Fields for Capture State

        private readonly Dictionary<string, (WpfKey Key, WpfModifierKeys Modifiers)> _registeredHotkeys = new();
        private readonly string _tempHotkeyName = "TempCapture";

        #endregion

        #region Constructor

        public ControlSettingsViewModel(
            ConfigurationService configurationService,
            ServiceContext serviceContext,
            MainGamepadService gamepadService,
            SystemIntegrationService systemService) : base(configurationService, serviceContext)
        {
            _gamepadService = gamepadService;
            _systemService = systemService;
        }

        #endregion

        #region BaseSettingsViewModel Implementation

        protected override async Task LoadPageSpecificSettingsAsync()
        {
            await CheckGamepadStatus();
        }

        protected override async Task ApplySettingsToConfigAsync(AppConfig config)
        {
            await Task.CompletedTask;
        }

        protected override async Task ApplySettingsToServicesAsync()
        {
            await _gamepadService.StopMonitoringAsync();
            await _gamepadService.StartMonitoringAsync(_serviceContext.Config.Input.GamepadShortcut);

            // Обновляем координатор геймпада
            await _serviceContext.GamepadVoiceCoordinator.UpdateGamepadSettingsAsync(
                _serviceContext.Config.Input.GamepadShortcut);
        }
        
        protected override async Task InitializePageSpecificDataAsync()
        {
            await CheckGamepadStatus();
        }

        public override void SubscribeToUIEvents()
        {
            _gamepadService.GamepadConnected += OnGamepadConnected;
            _gamepadService.GamepadDisconnected += OnGamepadDisconnected;
            _gamepadService.ShortcutPressed += OnGamepadShortcutPressed;
        }

        protected override void UnsubscribeFromUIEvents()
        {
            _gamepadService.GamepadConnected -= OnGamepadConnected;
            _gamepadService.GamepadDisconnected -= OnGamepadDisconnected;
            _gamepadService.ShortcutPressed -= OnGamepadShortcutPressed;
        }

        protected override void CleanupPageSpecific()
        {
            // Очищаем таймеры
            _gamepadCaptureTimer?.Dispose();
            _gamepadCaptureTimer = null;

            _keyboardCaptureTimer?.Dispose();
            _keyboardCaptureTimer = null;

            // Останавливаем захват
            StopKeyboardCapture();
            StopGamepadCapture();

            // Освобождаем сервис захвата геймпада
            if (_gamepadCaptureService != null)
            {
                _gamepadCaptureService.ShortcutCaptured -= OnGamepadShortcutCaptured;
                _gamepadCaptureService.CaptureStatusChanged -= OnGamepadCaptureStatusChanged;
                _gamepadCaptureService.Dispose();
                _gamepadCaptureService = null;
            }
        }

        #endregion

        #region Gamepad Event Handlers

        private void OnGamepadConnected(object? sender, GamepadConnectedEvent e)
        {
            GamepadStatusText = $"Геймпад подключен: {e.GamepadInfo.Name}";
            GamepadStatusColor = "#4caf50";
        }

        private void OnGamepadDisconnected(object? sender, GamepadDisconnectedEvent e)
        {
            GamepadStatusText = "Геймпад отключен";
            GamepadStatusColor = "#f44336";
        }

        private void OnGamepadShortcutPressed(object? sender, GamepadShortcutPressedEvent e)
        {
            // Пустой обработчик - логика в координаторе
        }

        #endregion

        #region Keyboard Capture Methods

        private async Task StartKeyboardCaptureInternal()
        {
            // Сохраняем оригинальную комбинацию для возврата
            _originalKeyboardComboText = KeyboardComboText;

            IsWaitingForKeyboardInput = true;
            ShowKeyboardTimer = true;
            KeyboardCaptureTimeLeft = AppConstants.CaptureTimeoutSeconds;

            // Меняем цвет на красноватый во время ожидания
            KeyboardComboTextColor = "#ff6b6b";
            KeyboardComboText = "Нажмите любую комбинацию клавиш...";
            StatusMessage = "Ожидание нажатия клавиш...";

            // Запускаем таймер обратного отсчета
            StartKeyboardCaptureTimer();

            // Регистрируем временные обработчики
            RegisterAllPossibleHotkeys();
        }

        private void StartKeyboardCaptureTimer()
        {
            _keyboardCaptureTimer?.Dispose();
            _keyboardCaptureTimer = new Timer(OnKeyboardCaptureTimerTick, null, 1000, 1000);
        }

        private void OnKeyboardCaptureTimerTick(object? state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                KeyboardCaptureTimeLeft--;

                if (KeyboardCaptureTimeLeft <= 0)
                {
                    StopKeyboardCaptureWithTimeout();
                }
            });
        }

        private void StopKeyboardCaptureWithTimeout()
        {
            _keyboardCaptureTimer?.Dispose();
            _keyboardCaptureTimer = null;

            IsWaitingForKeyboardInput = false;
            ShowKeyboardTimer = false;

            // Возвращаем оригинальную комбинацию
            KeyboardComboText = _originalKeyboardComboText;
            KeyboardComboTextColor = "White";

            StatusMessage = "Время ожидания истекло";

            // Удаляем временные хотkeys
            ClearTempHotkeys();

            // Очищаем сообщение через 2 секунды
            Task.Delay(2000).ContinueWith(_ => { 
                Application.Current.Dispatcher.Invoke(() => StatusMessage = ""); 
            });
        }

        private void RegisterAllPossibleHotkeys()
        {
            _registeredHotkeys.Clear();

            // Список часто используемых клавиш
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
                WpfModifierKeys.None, WpfModifierKeys.Control, WpfModifierKeys.Shift, 
                WpfModifierKeys.Alt, WpfModifierKeys.Control | WpfModifierKeys.Shift, 
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
                        // Игнорируем конфликты
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

                    // Используем DisplayText из Core модели
                    KeyboardComboText = keyboardShortcut.DisplayText;
                    KeyboardComboTextColor = "#4caf50"; 

                    // Останавливаем захват
                    _keyboardCaptureTimer?.Dispose();
                    ShowKeyboardTimer = false;
                    IsWaitingForKeyboardInput = false;

                    StatusMessage = "Комбинация сохранена!";
                    ClearTempHotkeys();

                    // Сохраняем в конфигурацию
                    _serviceContext.Config.Input.KeyboardShortcut = keyboardShortcut;
                    await OnUISettingChangedAsync();

                    // Регистрируем хоткей глобально
                    bool registered = await _systemService.RegisterGlobalHotkeyAsync(keyboardShortcut);
                    if (!registered)
                    {
                        StatusMessage = "Ошибка регистрации хоткея";
                        KeyboardComboTextColor = "#f44336"; 
                    }

                    // Возвращаем белый цвет через 2 секунды
                    await Task.Delay(2000);
                    KeyboardComboTextColor = "White";
                    StatusMessage = "";
                }
            }
            catch (Exception ex)
            {
                KeyboardComboText = "Ошибка сохранения";
                KeyboardComboTextColor = "#f44336"; 
                StopKeyboardCaptureWithTimeout();
            }

            e.Handled = true;
        }

        private void StopKeyboardCapture()
        {
            IsWaitingForKeyboardInput = false;
            ShowKeyboardTimer = false;

            _keyboardCaptureTimer?.Dispose();
            _keyboardCaptureTimer = null;

            ClearTempHotkeys();
        }

        private void ClearTempHotkeys()
        {
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
            if (_gamepadCaptureService == null)
            {
                _gamepadCaptureService = new GamepadCaptureService(_gamepadService);
                _gamepadCaptureService.ShortcutCaptured += OnGamepadShortcutCaptured;
                _gamepadCaptureService.CaptureStatusChanged += OnGamepadCaptureStatusChanged;
            }

            // Сохраняем оригинальную комбинацию
            _originalGamepadComboText = GamepadComboText;

            IsWaitingForGamepadInput = true;
            ShowGamepadTimer = true;
            GamepadCaptureTimeLeft = CAPTURE_TIMEOUT_SECONDS;

            GamepadComboTextColor = "#ff6b6b";
            StartCaptureTimer();

            await _gamepadCaptureService.StartCaptureAsync(CAPTURE_TIMEOUT_SECONDS);
        }

        private void StartCaptureTimer()
        {
            _gamepadCaptureTimer?.Dispose();
            _gamepadCaptureTimer = new Timer(OnCaptureTimerTick, null, 1000, 1000);
        }

        private void OnCaptureTimerTick(object? state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                GamepadCaptureTimeLeft--;

                if (GamepadCaptureTimeLeft <= 0)
                {
                    StopGamepadCaptureWithTimeout();
                }
            });
        }

        private void StopGamepadCaptureWithTimeout()
        {
            _gamepadCaptureTimer?.Dispose();
            _gamepadCaptureTimer = null;

            IsWaitingForGamepadInput = false;
            ShowGamepadTimer = false;

            GamepadComboText = _originalGamepadComboText;
            GamepadComboTextColor = "White";
            StatusMessage = "Время ожидания истекло";

            _gamepadCaptureService?.StopCapture();

            Task.Delay(2000).ContinueWith(_ => { 
                Application.Current.Dispatcher.Invoke(() => StatusMessage = ""); 
            });
        }

        private void StopGamepadCapture()
        {
            IsWaitingForGamepadInput = false; 
            _gamepadCaptureService?.StopCapture(); 
        }

        private async void OnGamepadShortcutCaptured(object? sender, GamepadShortcut capturedShortcut)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Используем DisplayText из Core модели
                    GamepadComboText = capturedShortcut.DisplayText;
                    GamepadComboTextColor = "#4caf50"; 
                    
                    _gamepadCaptureTimer?.Dispose();
                    ShowGamepadTimer = false;
                    IsWaitingForGamepadInput = false;

                    StatusMessage = "Комбинация сохранена!";
                });

                // Сохраняем в конфигурацию
                _serviceContext.Config.Input.GamepadShortcut = capturedShortcut;
                await OnUISettingChangedAsync();

                // Возвращаем белый цвет через 2 секунды
                await Task.Delay(2000);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    GamepadComboTextColor = "White";
                    StatusMessage = "";
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Ошибка сохранения: {ex.Message}";
                    GamepadComboTextColor = "#f44336";
                    StopGamepadCaptureWithTimeout();
                });
            }
        }

        private void OnGamepadCaptureStatusChanged(object? sender, string status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (IsWaitingForGamepadInput && !status.Contains("Захват остановлен"))
                {
                    GamepadComboText = status;
                    GamepadComboTextColor = status.Contains("кнопок") || status.Contains("комбинация") 
                        ? "#81c784" : "#ff6b6b";
                }
            });
        }

        #endregion

        #region Helper Methods

        private async Task CheckGamepadStatus()
        {
            try
            {
                var gamepad = await _gamepadService.GetConnectedGamepadAsync();

                if (gamepad != null)
                {
                    GamepadStatusText = $"Геймпад подключен: {gamepad.Name}";
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
            // Упрощенная версия - можно создать статический Dictionary для производительности
            return wpfKey switch
            {
                WpfKey.A => CoreKey.A, WpfKey.B => CoreKey.B, WpfKey.C => CoreKey.C, 
                WpfKey.D => CoreKey.D, WpfKey.E => CoreKey.E, WpfKey.F => CoreKey.F, 
                WpfKey.G => CoreKey.G, WpfKey.H => CoreKey.H, WpfKey.I => CoreKey.I, 
                WpfKey.J => CoreKey.J, WpfKey.K => CoreKey.K, WpfKey.L => CoreKey.L,
                WpfKey.M => CoreKey.M, WpfKey.N => CoreKey.N, WpfKey.O => CoreKey.O, 
                WpfKey.P => CoreKey.P, WpfKey.Q => CoreKey.Q, WpfKey.R => CoreKey.R, 
                WpfKey.S => CoreKey.S, WpfKey.T => CoreKey.T, WpfKey.U => CoreKey.U, 
                WpfKey.V => CoreKey.V, WpfKey.W => CoreKey.W, WpfKey.X => CoreKey.X,
                WpfKey.Y => CoreKey.Y, WpfKey.Z => CoreKey.Z,
                WpfKey.F1 => CoreKey.F1, WpfKey.F2 => CoreKey.F2, WpfKey.F3 => CoreKey.F3, 
                WpfKey.F4 => CoreKey.F4, WpfKey.F5 => CoreKey.F5, WpfKey.F6 => CoreKey.F6, 
                WpfKey.F7 => CoreKey.F7, WpfKey.F8 => CoreKey.F8, WpfKey.F9 => CoreKey.F9, 
                WpfKey.F10 => CoreKey.F10, WpfKey.F11 => CoreKey.F11, WpfKey.F12 => CoreKey.F12,
                WpfKey.NumPad0 => CoreKey.NumPad0, WpfKey.NumPad1 => CoreKey.NumPad1, 
                WpfKey.NumPad2 => CoreKey.NumPad2, WpfKey.NumPad3 => CoreKey.NumPad3,
                WpfKey.Insert => CoreKey.Insert, WpfKey.Delete => CoreKey.Delete, 
                WpfKey.Home => CoreKey.Home, WpfKey.End => CoreKey.End, 
                WpfKey.PageUp => CoreKey.PageUp, WpfKey.PageDown => CoreKey.PageDown,
                _ => CoreKey.A // Fallback
            };
        }

        private async Task OnUISettingChangedAsync()
        {
            if (IsLoadingUI) return;

            HasUnsavedChanges = true;
            await ApplySettingsAsync();
        }

        #endregion
    }
}