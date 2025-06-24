using ChatCaster.Core.Constants;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Managers;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.Services.GamepadService;
using ChatCaster.Windows.Utilities;
using ChatCaster.Windows.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChatCaster.Windows.ViewModels
{
    /// <summary>
    /// ViewModel для настроек управления (клавиатура и геймпад)
    /// Рефакторенная версия с разделением ответственности
    /// </summary>
    public partial class ControlSettingsViewModel : BaseSettingsViewModel
    {
        #region Private Services and Managers

        private readonly MainGamepadService _gamepadService;
        private readonly SystemIntegrationService _systemService;
        
        // Менеджеры для разделения ответственности - убираем nullable
        private GamepadStatusManager _gamepadStatusManager = null!;
        private GamepadCaptureManager _gamepadCaptureManager = null!;
        private KeyboardCaptureManager _keyboardCaptureManager = null!;
        private CaptureUIStateManager _gamepadUIManager = null!;
        private CaptureUIStateManager _keyboardUIManager = null!;

        #endregion

        #region Observable Properties

        // Геймпад статус
        [ObservableProperty]
        private string _gamepadStatusText = "Геймпад не найден";

        [ObservableProperty]
        private string _gamepadStatusColor = "#f44336";

        // Геймпад захват
        [ObservableProperty]
        private string _gamepadComboText = "LB + RB";

        [ObservableProperty]
        private string _gamepadComboTextColor = "White";

        [ObservableProperty]
        private bool _isWaitingForGamepadInput = false;

        [ObservableProperty]
        private int _gamepadCaptureTimeLeft = 0;

        [ObservableProperty]
        private bool _showGamepadTimer = false;

        // Клавиатура захват
        [ObservableProperty]
        private string _keyboardComboText = "Ctrl + Shift + R";

        [ObservableProperty]
        private string _keyboardComboTextColor = "White";

        [ObservableProperty]
        private bool _isWaitingForKeyboardInput = false;

        [ObservableProperty]
        private int _keyboardCaptureTimeLeft = 0;

        [ObservableProperty]
        private bool _showKeyboardTimer = false;

        // Клавиатура статус (для совместимости с XAML)
        [ObservableProperty]
        private string _keyboardStatusText = "Клавиатура готова";

        [ObservableProperty]
        private string _keyboardStatusColor = "#4caf50";

        #endregion

        #region Commands

        [RelayCommand]
        private async Task StartKeyboardCapture()
        {
            System.Diagnostics.Debug.WriteLine("[ControlSettingsViewModel] StartKeyboardCapture вызван");
            
            if (IsWaitingForKeyboardInput) 
            {
                System.Diagnostics.Debug.WriteLine("[ControlSettingsViewModel] Уже ждем ввод, выходим");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[ControlSettingsViewModel] Вызываем StartCaptureAsync...");
                await _keyboardCaptureManager.StartCaptureAsync(AppConstants.CaptureTimeoutSeconds);
                System.Diagnostics.Debug.WriteLine("[ControlSettingsViewModel] StartCaptureAsync завершен");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ControlSettingsViewModel] Ошибка: {ex.Message}");
                StatusMessage = $"Ошибка захвата клавиатуры: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task StartGamepadCapture()
        {
            if (IsWaitingForGamepadInput) return;

            try
            {
                await _gamepadCaptureManager.StartCaptureAsync(AppConstants.CaptureTimeoutSeconds);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка захвата геймпада: {ex.Message}";
            }
        }

        #endregion

        #region Constructor

        public ControlSettingsViewModel(
            ConfigurationService configurationService,
            ServiceContext serviceContext,
            MainGamepadService gamepadService,
            SystemIntegrationService systemService) : base(configurationService, serviceContext)
        {
            System.Diagnostics.Debug.WriteLine("🔥 [ControlSettingsViewModel] Конструктор начат");
            
            _gamepadService = gamepadService ?? throw new ArgumentNullException(nameof(gamepadService));
            _systemService = systemService ?? throw new ArgumentNullException(nameof(systemService));

            System.Diagnostics.Debug.WriteLine("🔥 [ControlSettingsViewModel] Создаем менеджеры...");
            
            try
            {
                // Инициализируем все менеджеры
                InitializeManagers();
                
                System.Diagnostics.Debug.WriteLine("🔥 [ControlSettingsViewModel] События подписаны");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [ControlSettingsViewModel] Ошибка создания менеджеров: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ [ControlSettingsViewModel] StackTrace: {ex.StackTrace}");
                throw;
            }
            
            System.Diagnostics.Debug.WriteLine("🔥 [ControlSettingsViewModel] Конструктор завершен");
        }

        #endregion

        #region Manager Initialization

        private void InitializeManagers()
        {
            // Создаем менеджеры
            _gamepadStatusManager = new GamepadStatusManager(_gamepadService);
            _gamepadCaptureManager = new GamepadCaptureManager(_gamepadService);
            _keyboardCaptureManager = new KeyboardCaptureManager();
            
            System.Diagnostics.Debug.WriteLine("🔥 [ControlSettingsViewModel] Менеджеры созданы");
            
            // Создаем UI менеджеры
            _gamepadUIManager = new CaptureUIStateManager();
            _keyboardUIManager = new CaptureUIStateManager();

            System.Diagnostics.Debug.WriteLine("🔥 [ControlSettingsViewModel] UI менеджеры созданы");

            // Подписываемся на события менеджеров
            SubscribeToManagerEvents();
        }

        #endregion

        #region BaseSettingsViewModel Implementation

        protected override async Task LoadPageSpecificSettingsAsync()
        {
            // Загружаем текущие комбинации из конфигурации
            var config = _serviceContext!.Config;
            
            GamepadComboText = config.Input.GamepadShortcut?.DisplayText ?? "LB + RB";
            KeyboardComboText = config.Input.KeyboardShortcut?.DisplayText ?? "Ctrl + Shift + R";

            // Устанавливаем базовые состояния UI
            _gamepadUIManager.SetIdleState(GamepadComboText);
            _keyboardUIManager.SetIdleState(KeyboardComboText);

            // Обновляем статус геймпада
            await _gamepadStatusManager.RefreshStatusAsync();
        }

        protected override async Task ApplySettingsToConfigAsync(AppConfig config)
        {
            // Настройки уже сохранены в обработчиках событий
            await Task.CompletedTask;
        }

        protected override async Task ApplySettingsToServicesAsync()
        {
            // Перезапускаем мониторинг геймпада с новыми настройками
            await _gamepadService.StopMonitoringAsync();
            await _gamepadService.StartMonitoringAsync(_serviceContext!.Config.Input.GamepadShortcut);

            // Обновляем координатор геймпада
            await _serviceContext.GamepadVoiceCoordinator!.UpdateGamepadSettingsAsync(
                _serviceContext.Config.Input.GamepadShortcut);
        }

        protected override async Task InitializePageSpecificDataAsync()
        {
            System.Diagnostics.Debug.WriteLine("🔥 [ControlSettingsViewModel] InitializePageSpecificDataAsync начат");
            
            await LoadPageSpecificSettingsAsync();
        }

        public override void SubscribeToUIEvents()
        {
            // События геймпада обрабатываются через GamepadStatusManager
        }

        protected override void UnsubscribeFromUIEvents()
        {
            // События отписываются в CleanupPageSpecific
        }

        protected override void CleanupPageSpecific()
        {
            // Отписываемся от событий менеджеров
            UnsubscribeFromManagerEvents();

            // Освобождаем менеджеры
            _gamepadStatusManager?.Dispose();
            _gamepadCaptureManager?.Dispose();
            _keyboardCaptureManager?.Dispose();
            _gamepadUIManager?.Dispose();
            _keyboardUIManager?.Dispose();
        }

        #endregion

        #region Manager Event Subscriptions

        private void SubscribeToManagerEvents()
        {
            // События статуса геймпада
            _gamepadStatusManager.StatusChanged += OnGamepadStatusChanged;

            // События захвата геймпада
            _gamepadCaptureManager.CaptureCompleted += OnGamepadCaptureCompleted;
            _gamepadCaptureManager.CaptureTimeout += OnGamepadCaptureTimeout;
            _gamepadCaptureManager.StatusChanged += OnGamepadCaptureStatusChanged;
            _gamepadCaptureManager.CaptureError += OnGamepadCaptureError;

            // События захвата клавиатуры
            _keyboardCaptureManager.CaptureCompleted += OnKeyboardCaptureCompleted;
            _keyboardCaptureManager.CaptureTimeout += OnKeyboardCaptureTimeout;
            _keyboardCaptureManager.StatusChanged += OnKeyboardCaptureStatusChanged;
            _keyboardCaptureManager.CaptureError += OnKeyboardCaptureError;

            // События UI менеджеров
            _gamepadUIManager.StateChanged += OnGamepadUIStateChanged;
            _keyboardUIManager.StateChanged += OnKeyboardUIStateChanged;
        }

        private void UnsubscribeFromManagerEvents()
        {
            // События статуса геймпада
            _gamepadStatusManager.StatusChanged -= OnGamepadStatusChanged;

            // События захвата геймпада
            _gamepadCaptureManager.CaptureCompleted -= OnGamepadCaptureCompleted;
            _gamepadCaptureManager.CaptureTimeout -= OnGamepadCaptureTimeout;
            _gamepadCaptureManager.StatusChanged -= OnGamepadCaptureStatusChanged;
            _gamepadCaptureManager.CaptureError -= OnGamepadCaptureError;

            // События захвата клавиатуры
            _keyboardCaptureManager.CaptureCompleted -= OnKeyboardCaptureCompleted;
            _keyboardCaptureManager.CaptureTimeout -= OnKeyboardCaptureTimeout;
            _keyboardCaptureManager.StatusChanged -= OnKeyboardCaptureStatusChanged;
            _keyboardCaptureManager.CaptureError -= OnKeyboardCaptureError;

            // События UI менеджеров
            _gamepadUIManager.StateChanged -= OnGamepadUIStateChanged;
            _keyboardUIManager.StateChanged -= OnKeyboardUIStateChanged;
        }

        #endregion

        #region Event Handlers

        // Геймпад статус
        private void OnGamepadStatusChanged(string statusText, string statusColor)
        {
            GamepadStatusText = statusText;
            GamepadStatusColor = statusColor;
        }

        // Геймпад захват
        private async void OnGamepadCaptureCompleted(GamepadShortcut capturedShortcut)
        {
            System.Diagnostics.Debug.WriteLine($"🔥 [VM] OnGamepadCaptureCompleted: {capturedShortcut.DisplayText}");
            System.Diagnostics.Debug.WriteLine($"🔥 [VM] IsWaitingForGamepadInput ДО: {IsWaitingForGamepadInput}");
            
            try
            {
                // Сбрасываем флаг
                IsWaitingForGamepadInput = false;
                System.Diagnostics.Debug.WriteLine($"🔥 [VM] IsWaitingForGamepadInput ПОСЛЕ сброса: {IsWaitingForGamepadInput}");
                
                // Сохраняем в конфигурацию
                _serviceContext!.Config.Input.GamepadShortcut = capturedShortcut;
                await OnUISettingChangedAsync();

                // Обновляем UI
                GamepadComboText = capturedShortcut.DisplayText;
                System.Diagnostics.Debug.WriteLine($"🔥 [VM] GamepadComboText установлен в: {GamepadComboText}");
                
                await _gamepadUIManager.CompleteSuccessAsync(capturedShortcut.DisplayText);
                _gamepadUIManager.SetIdleState(GamepadComboText);
                
                System.Diagnostics.Debug.WriteLine($"🔥 [VM] OnGamepadCaptureCompleted завершен успешно");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [VM] Ошибка в OnGamepadCaptureCompleted: {ex.Message}");
                IsWaitingForGamepadInput = false;
                await _gamepadUIManager.CompleteWithErrorAsync($"Ошибка сохранения: {ex.Message}");
            }
        }

        private async void OnGamepadCaptureTimeout()
        {
            IsWaitingForGamepadInput = false;
            await _gamepadUIManager.CompleteWithTimeoutAsync();
        }

        private void OnGamepadCaptureStatusChanged(string status)
        {
            System.Diagnostics.Debug.WriteLine($"🔥 [VM] OnGamepadCaptureStatusChanged: {status}");
            System.Diagnostics.Debug.WriteLine($"🔥 [VM] _gamepadCaptureManager.IsCapturing: {_gamepadCaptureManager.IsCapturing}");
            
            if (_gamepadCaptureManager.IsCapturing)
            {
                System.Diagnostics.Debug.WriteLine($"🔥 [VM] Начинаем захват, устанавливаем IsWaitingForGamepadInput = true");
                _gamepadUIManager.StartCapture(status, AppConstants.CaptureTimeoutSeconds);
                IsWaitingForGamepadInput = true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"🔥 [VM] Захват НЕ активен, устанавливаем IsWaitingForGamepadInput = false");
                IsWaitingForGamepadInput = false;
            }
            
            System.Diagnostics.Debug.WriteLine($"🔥 [VM] IsWaitingForGamepadInput итоговое: {IsWaitingForGamepadInput}");
        }

        private async void OnGamepadCaptureError(string error)
        {
            IsWaitingForGamepadInput = false;
            await _gamepadUIManager.CompleteWithErrorAsync(error);
        }

        // Клавиатура захват
        private async void OnKeyboardCaptureCompleted(KeyboardShortcut capturedShortcut)
        {
            System.Diagnostics.Debug.WriteLine($"🔥 [VM] OnKeyboardCaptureCompleted: {capturedShortcut.DisplayText}");
            System.Diagnostics.Debug.WriteLine($"🔥 [VM] IsWaitingForKeyboardInput ДО: {IsWaitingForKeyboardInput}");
            
            try
            {
                // Сбрасываем флаг
                IsWaitingForKeyboardInput = false;
                System.Diagnostics.Debug.WriteLine($"🔥 [VM] IsWaitingForKeyboardInput ПОСЛЕ сброса: {IsWaitingForKeyboardInput}");
                
                // Сохраняем в конфигурацию
                _serviceContext!.Config.Input.KeyboardShortcut = capturedShortcut;
                await OnUISettingChangedAsync();

                // Регистрируем глобальный хоткей
                bool registered = await _systemService.RegisterGlobalHotkeyAsync(capturedShortcut);
                if (!registered)
                {
                    System.Diagnostics.Debug.WriteLine("[VM] Ошибка регистрации хоткея");
                    await _keyboardUIManager.CompleteWithErrorAsync("Ошибка регистрации хоткея");
                    return;
                }

                // Обновляем UI
                KeyboardComboText = capturedShortcut.DisplayText;
                System.Diagnostics.Debug.WriteLine($"🔥 [VM] KeyboardComboText установлен в: {KeyboardComboText}");
                
                await _keyboardUIManager.CompleteSuccessAsync(capturedShortcut.DisplayText);
                _keyboardUIManager.SetIdleState(KeyboardComboText);
                
                System.Diagnostics.Debug.WriteLine($"🔥 [VM] OnKeyboardCaptureCompleted завершен успешно");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [VM] Ошибка в OnKeyboardCaptureCompleted: {ex.Message}");
                IsWaitingForKeyboardInput = false;
                await _keyboardUIManager.CompleteWithErrorAsync($"Ошибка сохранения: {ex.Message}");
            }
        }

        private async void OnKeyboardCaptureTimeout()
        {
            IsWaitingForKeyboardInput = false;
            await _keyboardUIManager.CompleteWithTimeoutAsync();
        }

        private void OnKeyboardCaptureStatusChanged(string status)
        {
            System.Diagnostics.Debug.WriteLine($"🔥 [VM] OnKeyboardCaptureStatusChanged: {status}");
            System.Diagnostics.Debug.WriteLine($"🔥 [VM] _keyboardCaptureManager.IsCapturing: {_keyboardCaptureManager.IsCapturing}");
            
            if (_keyboardCaptureManager.IsCapturing)
            {
                System.Diagnostics.Debug.WriteLine($"🔥 [VM] Начинаем захват клавиатуры, устанавливаем IsWaitingForKeyboardInput = true");
                _keyboardUIManager.StartCapture(status, AppConstants.CaptureTimeoutSeconds);
                IsWaitingForKeyboardInput = true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"🔥 [VM] Захват клавиатуры НЕ активен, устанавливаем IsWaitingForKeyboardInput = false");
                IsWaitingForKeyboardInput = false;
            }
            
            System.Diagnostics.Debug.WriteLine($"🔥 [VM] IsWaitingForKeyboardInput итоговое: {IsWaitingForKeyboardInput}");
        }

        private async void OnKeyboardCaptureError(string error)
        {
            IsWaitingForKeyboardInput = false;
            await _keyboardUIManager.CompleteWithErrorAsync(error);
        }

        // UI состояния
        private void OnGamepadUIStateChanged(CaptureUIState state)
        {
            GamepadComboText = state.Text;
            GamepadComboTextColor = state.TextColor;
            ShowGamepadTimer = state.ShowTimer;
            GamepadCaptureTimeLeft = state.TimeLeft;
            
            if (!string.IsNullOrEmpty(state.StatusMessage))
            {
                StatusMessage = state.StatusMessage;
            }
        }

        private void OnKeyboardUIStateChanged(CaptureUIState state)
        {
            KeyboardComboText = state.Text;
            KeyboardComboTextColor = state.TextColor;
            ShowKeyboardTimer = state.ShowTimer;
            KeyboardCaptureTimeLeft = state.TimeLeft;
            
            if (!string.IsNullOrEmpty(state.StatusMessage))
            {
                StatusMessage = state.StatusMessage;
            }
        }

        #endregion
    }
}