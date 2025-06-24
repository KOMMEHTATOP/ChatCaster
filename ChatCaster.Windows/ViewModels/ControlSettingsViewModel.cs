using ChatCaster.Core.Constants;
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

        private readonly MainGamepadService? _gamepadService;
        private readonly SystemIntegrationService? _systemService;
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
        private string _gamepadComboTextColor = "White"; // Цвет текста комбинации

        [ObservableProperty]
        private string _keyboardComboTextColor = "White"; // Цвет текста клавиатуры

        [ObservableProperty]
        private int _gamepadCaptureTimeLeft = 0; // Оставшееся время захвата

        [ObservableProperty]
        private bool _showGamepadTimer = false; // Показывать ли таймер
        [ObservableProperty]
        private int _keyboardCaptureTimeLeft = 0; // Оставшееся время захвата клавиатуры

        [ObservableProperty]
        private bool _showKeyboardTimer = false; // Показывать ли таймер клавиатуры

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
            Services.GamepadService.MainGamepadService? gamepadService,
            SystemIntegrationService? systemService) : base(configurationService, serviceContext)
        {
            _gamepadService = gamepadService;
            _systemService = systemService;
        }

        #endregion

        #region BaseSettingsViewModel Implementation

        protected override async Task LoadPageSpecificSettingsAsync()
        {
            Console.WriteLine("🔄 [ControlSettings] Начинаем загрузку настроек...");

            if (_serviceContext?.Config == null)
            {
                Console.WriteLine("❌ [ControlSettings] ServiceContext или Config = null");
                return;
            }

            if (_serviceContext.Config.Input == null)
            {
                Console.WriteLine("❌ [ControlSettings] Input config = null");
                return;
            }

            var inputConfig = _serviceContext.Config.Input;

            // === ДИАГНОСТИКА ГЕЙМПАДА ===
            Console.WriteLine(
                $"🎮 [ControlSettings] GamepadShortcut = {(inputConfig.GamepadShortcut != null ? "НЕ NULL" : "NULL")}");

            if (inputConfig.GamepadShortcut != null)
            {
                var gamepadShortcut = inputConfig.GamepadShortcut;
                Console.WriteLine($"🎮 [ControlSettings] Primary: {gamepadShortcut.PrimaryButton}");
                Console.WriteLine($"🎮 [ControlSettings] Secondary: {gamepadShortcut.SecondaryButton}");
                Console.WriteLine($"🎮 [ControlSettings] RequireBoth: {gamepadShortcut.RequireBothButtons}");
                Console.WriteLine($"🎮 [ControlSettings] DisplayText: '{gamepadShortcut.DisplayText}'");

                // Используем DisplayText из Core модели вместо форматирования
                var newGamepadText = gamepadShortcut.DisplayText;
                Console.WriteLine($"🎮 [ControlSettings] Устанавливаем GamepadComboText: '{newGamepadText}'");

                GamepadComboText = newGamepadText;

                Console.WriteLine($"🎮 [ControlSettings] ПОСЛЕ установки GamepadComboText = '{GamepadComboText}'");
            }
            else
            {
                Console.WriteLine("🎮 [ControlSettings] GamepadShortcut is NULL - устанавливаем значение по умолчанию");
                GamepadComboText = "LB + RB";
            }

            // === ДИАГНОСТИКА КЛАВИАТУРЫ ===
            Console.WriteLine(
                $"⌨️ [ControlSettings] KeyboardShortcut = {(inputConfig.KeyboardShortcut != null ? "НЕ NULL" : "NULL")}");

            if (inputConfig.KeyboardShortcut != null)
            {
                var keyboardShortcut = inputConfig.KeyboardShortcut;
                Console.WriteLine($"⌨️ [ControlSettings] Modifiers: {keyboardShortcut.Modifiers}");
                Console.WriteLine($"⌨️ [ControlSettings] Key: {keyboardShortcut.Key}");

                var newKeyboardText = keyboardShortcut.DisplayText; 
                Console.WriteLine($"⌨️ [ControlSettings] Устанавливаем KeyboardComboText: '{newKeyboardText}'");

                KeyboardComboText = newKeyboardText;

                Console.WriteLine($"⌨️ [ControlSettings] ПОСЛЕ установки KeyboardComboText = '{KeyboardComboText}'");
            }
            else
            {
                Console.WriteLine("⌨️ [ControlSettings] KeyboardShortcut is NULL - устанавливаем значение по умолчанию");
                KeyboardComboText = "Ctrl + Shift + R";
            }

            await CheckGamepadStatus();

            // Финальная проверка
            Console.WriteLine($"✅ [ControlSettings] ИТОГОВЫЕ значения:");
            Console.WriteLine($"✅ [ControlSettings] GamepadComboText = '{GamepadComboText}'");
            Console.WriteLine($"✅ [ControlSettings] KeyboardComboText = '{KeyboardComboText}'");
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

                // Передаем GamepadShortcut вместо InputConfig
                await _gamepadService.StartMonitoringAsync(_serviceContext.Config.Input.GamepadShortcut);
            }

            // Обновляем координатор геймпада
            if (_serviceContext?.GamepadVoiceCoordinator != null && _serviceContext.Config != null)
            {
                await _serviceContext.GamepadVoiceCoordinator.UpdateGamepadSettingsAsync(
                    _serviceContext.Config.Input.GamepadShortcut);
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
            // Очищаем таймеры при закрытии
            _gamepadCaptureTimer?.Dispose();
            _gamepadCaptureTimer = null;

            _keyboardCaptureTimer?.Dispose();
            _keyboardCaptureTimer = null;

            // Останавливаем захват если активен
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
            if (IsWaitingForKeyboardInput) return;

            try
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

                // Регистрируем временные обработчики для всех возможных комбинаций
                RegisterAllPossibleHotkeys();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка захвата клавиатуры: {ex.Message}";
                Console.WriteLine($"❌ [{GetType().Name}] Ошибка захвата клавиатуры: {ex.Message}");
                StopKeyboardCaptureWithTimeout();
            }
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
                    // Время истекло - возвращаем старую комбинацию
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
            Task.Delay(2000).ContinueWith(_ => { Application.Current.Dispatcher.Invoke(() => StatusMessage = ""); });
        }


        private async void OnKeyboardShortcutCaptured(object? sender, KeyboardShortcut capturedShortcut)
        {
            try
            {
                Console.WriteLine($"⌨️ [Capture] Комбинация захвачена: {capturedShortcut.DisplayText}");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Console.WriteLine($"⌨️ [Capture] Старое значение UI: '{KeyboardComboText}'");

                    // Устанавливаем новую комбинацию с зеленым цветом успеха
                    KeyboardComboText = capturedShortcut.DisplayText;
                    KeyboardComboTextColor = "#4caf50"; // Зеленый для успеха

                    Console.WriteLine($"⌨️ [Capture] Новое значение UI: '{KeyboardComboText}'");

                    // Останавливаем таймер
                    _keyboardCaptureTimer?.Dispose();
                    ShowKeyboardTimer = false;
                    IsWaitingForKeyboardInput = false;

                    StatusMessage = "Комбинация сохранена!";
                });

                // Сохраняем в конфигурацию
                if (_serviceContext?.Config?.Input != null)
                {
                    _serviceContext.Config.Input.KeyboardShortcut = capturedShortcut;
                    await OnUISettingChangedAsync();
                    Console.WriteLine($"⌨️ [Capture] Конфигурация обновлена");
                }

                // Возвращаем белый цвет через 2 секунды
                await Task.Delay(2000);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    KeyboardComboTextColor = "White";
                    StatusMessage = "";
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [Capture] Ошибка сохранения: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Ошибка сохранения: {ex.Message}";
                    KeyboardComboTextColor = "#f44336"; // Красный для ошибки
                    StopKeyboardCaptureWithTimeout();
                });
            }
        }
        private void OnKeyboardCaptureStatusChanged(object? sender, string status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (IsWaitingForKeyboardInput && !status.Contains("Захват остановлен"))
                {
                    // Показываем промежуточный статус зеленым если это новая комбинация
                    if (status.Contains("клавиш") || status.Contains("комбинация") || status.Contains("+"))
                    {
                        KeyboardComboText = status;
                        KeyboardComboTextColor = "#81c784"; // Светло-зеленый для промежуточного состояния
                    }
                    else
                    {
                        KeyboardComboText = status;
                        KeyboardComboTextColor = "#ff6b6b"; // Красноватый для обычного статуса
                    }

                    Console.WriteLine($"⌨️ [Status] Обновляем статус: '{status}'");
                }
                else
                {
                    Console.WriteLine($"⌨️ [Status] Игнорируем статус (захват завершен): '{status}'");
                }
            });
        }


        private void RegisterAllPossibleHotkeys()
        {
            _registeredHotkeys.Clear();

            // Список часто используемых клавиш для быстрой настройки
            var commonKeys = new[]
            {
                WpfKey.F1, WpfKey.F2, WpfKey.F3, WpfKey.F4, WpfKey.F5, WpfKey.F6, WpfKey.F7, WpfKey.F8, WpfKey.F9,
                WpfKey.F10, WpfKey.F11, WpfKey.F12, WpfKey.NumPad0, WpfKey.NumPad1, WpfKey.NumPad2, WpfKey.NumPad3,
                WpfKey.Insert, WpfKey.Delete, WpfKey.Home, WpfKey.End, WpfKey.PageUp, WpfKey.PageDown
            };

            var modifiers = new[]
            {
                WpfModifierKeys.None, WpfModifierKeys.Control, WpfModifierKeys.Shift, WpfModifierKeys.Alt,
                WpfModifierKeys.Control | WpfModifierKeys.Shift, WpfModifierKeys.Control | WpfModifierKeys.Alt,
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
                        Modifiers = ConvertToCore(hotkeyInfo.Modifiers), Key = ConvertToCore(hotkeyInfo.Key)
                    };

                    var comboText = keyboardShortcut.DisplayText; // Используем DisplayText из Core

                    Console.WriteLine($"⌨️ [Capture] Комбинация захвачена: {comboText}");

                    // Показываем зеленый цвет успеха
                    KeyboardComboText = comboText;
                    KeyboardComboTextColor = "#4caf50"; // Зеленый для успеха

                    // Останавливаем таймер
                    _keyboardCaptureTimer?.Dispose();
                    ShowKeyboardTimer = false;
                    IsWaitingForKeyboardInput = false;

                    StatusMessage = "Комбинация сохранена!";

                    // Очищаем временные хотkeys
                    ClearTempHotkeys();

                    if (_serviceContext?.Config != null)
                    {
                        _serviceContext.Config.Input.KeyboardShortcut = keyboardShortcut;
                        await OnUISettingChangedAsync();

                        // Регистрируем хоткей глобально
                        if (_systemService != null)
                        {
                            Console.WriteLine($"Регистрируем глобальный хоткей: {comboText}");
                            bool registered = await _systemService.RegisterGlobalHotkeyAsync(keyboardShortcut);

                            if (!registered)
                            {
                                StatusMessage = "Ошибка регистрации хоткея";
                                KeyboardComboTextColor = "#f44336"; // Красный для ошибки
                            }
                        }

                        Console.WriteLine($"Сохранена комбинация: {comboText}");
                    }

                    // Возвращаем белый цвет через 2 секунды
                    await Task.Delay(2000);
                    KeyboardComboTextColor = "White";
                    StatusMessage = "";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения комбинации: {ex.Message}");
                KeyboardComboText = "Ошибка сохранения";
                KeyboardComboTextColor = "#f44336"; // Красный для ошибки
                StopKeyboardCaptureWithTimeout();
            }

            e.Handled = true;
        }

        private void StopKeyboardCapture()
        {
            IsWaitingForKeyboardInput = false;
            ShowKeyboardTimer = false;
            _captureCompleted = false;
            _holdTimer?.Stop();

            // Останавливаем таймер
            _keyboardCaptureTimer?.Dispose();
            _keyboardCaptureTimer = null;

            ClearTempHotkeys();
        }

        private void ClearTempHotkeys()
        {
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
            try
            {
                if (_gamepadCaptureService == null && _gamepadService != null)
                {
                    _gamepadCaptureService = new GamepadCaptureService(_gamepadService);
                    _gamepadCaptureService.ShortcutCaptured += OnGamepadShortcutCaptured;
                    _gamepadCaptureService.CaptureStatusChanged += OnGamepadCaptureStatusChanged;
                }

                if (_gamepadCaptureService == null)
                {
                    StatusMessage = "Сервис геймпада недоступен";
                    return;
                }

                // Сохраняем оригинальную комбинацию для возврата
                _originalGamepadComboText = GamepadComboText;

                IsWaitingForGamepadInput = true;
                ShowGamepadTimer = true;
                GamepadCaptureTimeLeft = CAPTURE_TIMEOUT_SECONDS;

                // Меняем цвет на красноватый во время ожидания
                GamepadComboTextColor = "#ff6b6b";

                // Запускаем таймер обратного отсчета
                StartCaptureTimer();

                await _gamepadCaptureService.StartCaptureAsync(CAPTURE_TIMEOUT_SECONDS);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ViewModel] Ошибка захвата геймпада: {ex.Message}");
                StatusMessage = $"Ошибка захвата: {ex.Message}";
                StopGamepadCaptureWithTimeout();
            }
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
                    // Время истекло - возвращаем старую комбинацию
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

            // Возвращаем оригинальную комбинацию
            GamepadComboText = _originalGamepadComboText;
            GamepadComboTextColor = "White";

            StatusMessage = "Время ожидания истекло";

            _gamepadCaptureService?.StopCapture();

            // Очищаем сообщение через 2 секунды
            Task.Delay(2000).ContinueWith(_ => { Application.Current.Dispatcher.Invoke(() => StatusMessage = ""); });
        }


        private void StopGamepadCapture()
        {
            Console.WriteLine("🎮 [Capture] Останавливаем захват геймпада");

            IsWaitingForGamepadInput = false; // Сначала меняем флаг
            _gamepadCaptureService?.StopCapture(); // Потом останавливаем сервис

            Console.WriteLine($"🎮 [Capture] Захват остановлен. Текущий текст: '{GamepadComboText}'");
        }
        private async void OnGamepadShortcutCaptured(object? sender, GamepadShortcut capturedShortcut)
        {
            try
            {
                Console.WriteLine($"🎮 [Capture] Комбинация захвачена: {capturedShortcut.DisplayText}");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Console.WriteLine($"🎮 [Capture] Старое значение UI: '{GamepadComboText}'");

                    // Устанавливаем новую комбинацию с зеленым цветом успеха
                    GamepadComboText = capturedShortcut.DisplayText;
                    GamepadComboTextColor = "#4caf50"; // Зеленый для успеха

                    Console.WriteLine($"🎮 [Capture] Новое значение UI: '{GamepadComboText}'");

                    // Останавливаем таймер
                    _gamepadCaptureTimer?.Dispose();
                    ShowGamepadTimer = false;
                    IsWaitingForGamepadInput = false;

                    StatusMessage = "Комбинация сохранена!";
                });

                // Сохраняем в конфигурацию
                if (_serviceContext?.Config?.Input != null)
                {
                    _serviceContext.Config.Input.GamepadShortcut = capturedShortcut;
                    await OnUISettingChangedAsync();
                    Console.WriteLine($"🎮 [Capture] Конфигурация обновлена");
                }

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
                Console.WriteLine($"❌ [Capture] Ошибка сохранения: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Ошибка сохранения: {ex.Message}";
                    GamepadComboTextColor = "#f44336"; // Красный для ошибки
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
                    // Показываем промежуточный статус зеленым если это новая комбинация
                    if (status.Contains("кнопок") || status.Contains("комбинация"))
                    {
                        GamepadComboText = status;
                        GamepadComboTextColor = "#81c784"; // Светло-зеленый для промежуточного состояния
                    }
                    else
                    {
                        GamepadComboText = status;
                        GamepadComboTextColor = "#ff6b6b"; // Красноватый для обычного статуса
                    }

                    Console.WriteLine($"🎮 [Status] Обновляем статус: '{status}'");
                }
                else
                {
                    Console.WriteLine($"🎮 [Status] Игнорируем статус (захват завершен): '{status}'");
                }
            });
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

                // ✅ ИСПРАВЛЕНО: Используем GetConnectedGamepads() без Async
                var gamepad = await _gamepadService.GetConnectedGamepadAsync();

                if (gamepad != null)
                {
                    // ✅ ИСПРАВЛЕНО: Один геймпад найден
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

        private string FormatGamepadShortcut(GamepadShortcut shortcut)
        {
            if (shortcut.RequireBothButtons)
            {
                return $"{GetButtonDisplayName(shortcut.PrimaryButton)} + {GetButtonDisplayName(shortcut.SecondaryButton)}";
            }
            else
            {
                return
                    $"{GetButtonDisplayName(shortcut.PrimaryButton)} или {GetButtonDisplayName(shortcut.SecondaryButton)}";
            }
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
                _ => button.ToString()
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
