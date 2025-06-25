using ChatCaster.Core.Constants;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Managers;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.Services.GamepadService;
using ChatCaster.Windows.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace ChatCaster.Windows.ViewModels
{
    /// <summary>
    /// Компонент для управления настройками геймпада
    /// </summary>
    public partial class GamepadCaptureComponentViewModel : BaseCaptureComponentViewModel
    {
        private readonly MainGamepadService _gamepadService;
        private readonly ServiceContext _serviceContext;
        
        private GamepadStatusManager? _statusManager;
        private GamepadCaptureManager? _captureManager;

        // Статус геймпада
        [ObservableProperty]
        private string _statusText = "Геймпад не найден";

        [ObservableProperty]
        private string _statusColor = "#f44336";

        public GamepadCaptureComponentViewModel(
            MainGamepadService gamepadService, 
            ServiceContext serviceContext)
        {
            _gamepadService = gamepadService ?? throw new ArgumentNullException(nameof(gamepadService));
            _serviceContext = serviceContext ?? throw new ArgumentNullException(nameof(serviceContext));
            
            InitializeManagers();
        }

        private void InitializeManagers()
        {
            try
            {
                _statusManager = new GamepadStatusManager(_gamepadService);
                _captureManager = new GamepadCaptureManager(_gamepadService);
                _uiManager = new CaptureUIStateManager();

                SubscribeToEvents();
                
                Log.Debug("GamepadCaptureComponent инициализирован");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка инициализации GamepadCaptureComponent");
                throw;
            }
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                var shortcut = _serviceContext.Config.Input.GamepadShortcut;
                ComboText = shortcut?.DisplayText ?? "LB + RB";
                
                _uiManager?.SetIdleState(ComboText);
                
                if (_statusManager != null)
                {
                    await _statusManager.RefreshStatusAsync();
                }
                
                Log.Debug("Настройки геймпада загружены: {Combo}", ComboText);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки настроек геймпада");
            }
        }

        public override async Task StartCaptureAsync()
        {
            if (IsWaitingForInput || _captureManager == null)
            {
                Log.Debug("Захват геймпада уже активен или менеджер недоступен");
                return;
            }

            try
            {
                Log.Debug("Запуск захвата геймпада");
                await _captureManager.StartCaptureAsync(AppConstants.CaptureTimeoutSeconds);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка запуска захвата геймпада");
                OnStatusMessageChanged($"Ошибка захвата геймпада: {ex.Message}");
            }
        }

        public async Task ApplySettingsAsync()
        {
            try
            {
                var shortcut = _serviceContext.Config.Input.GamepadShortcut;

                await _gamepadService.StopMonitoringAsync();
                await _gamepadService.StartMonitoringAsync(shortcut);

                if (_serviceContext.GamepadVoiceCoordinator != null)
                {
                    await _serviceContext.GamepadVoiceCoordinator.UpdateGamepadSettingsAsync(shortcut);
                }
                
                Log.Debug("Настройки геймпада применены");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка применения настроек геймпада");
            }
        }

        private void SubscribeToEvents()
        {
            if (_statusManager != null)
            {
                _statusManager.StatusChanged += OnStatusChanged;
            }

            if (_captureManager != null)
            {
                _captureManager.CaptureCompleted += OnCaptureCompleted;
                _captureManager.CaptureTimeout += OnCaptureTimeout;
                _captureManager.StatusChanged += OnCaptureStatusChanged;
                _captureManager.CaptureError += OnCaptureError;
            }

            if (_uiManager != null)
            {
                _uiManager.StateChanged += OnUIStateChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_statusManager != null)
            {
                _statusManager.StatusChanged -= OnStatusChanged;
            }

            if (_captureManager != null)
            {
                _captureManager.CaptureCompleted -= OnCaptureCompleted;
                _captureManager.CaptureTimeout -= OnCaptureTimeout;
                _captureManager.StatusChanged -= OnCaptureStatusChanged;
                _captureManager.CaptureError -= OnCaptureError;
            }

            if (_uiManager != null)
            {
                _uiManager.StateChanged -= OnUIStateChanged;
            }
        }

        // Event handlers
        private void OnStatusChanged(string statusText, string statusColor)
        {
            StatusText = statusText;
            StatusColor = statusColor;
        }

        private void OnCaptureCompleted(GamepadShortcut capturedShortcut)
        {
            _ = HandleCaptureCompletedAsync(capturedShortcut);
        }

        private async Task HandleCaptureCompletedAsync(GamepadShortcut capturedShortcut)
        {
            try
            {
                IsWaitingForInput = false;
                
                _serviceContext.Config.Input.GamepadShortcut = capturedShortcut;
                await OnSettingChangedAsync();

                ComboText = capturedShortcut.DisplayText;
                
                if (_uiManager != null)
                {
                    await _uiManager.CompleteSuccessAsync(capturedShortcut.DisplayText);
                    _uiManager.SetIdleState(ComboText);
                }
                
                Log.Information("Геймпад комбинация сохранена: {Shortcut}", capturedShortcut.DisplayText);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки захваченной геймпад комбинации");
                IsWaitingForInput = false;
                if (_uiManager != null)
                {
                    await _uiManager.CompleteWithErrorAsync($"Ошибка сохранения: {ex.Message}");
                }
            }
        }

        private void OnCaptureTimeout()
        {
            _ = HandleTimeoutAsync();
        }

        private async Task HandleTimeoutAsync()
        {
            IsWaitingForInput = false;
            if (_uiManager != null)
            {
                await _uiManager.CompleteWithTimeoutAsync();
            }
        }

        private void OnCaptureStatusChanged(string status)
        {
            if (_captureManager?.IsCapturing == true)
            {
                _uiManager?.StartCapture(status, AppConstants.CaptureTimeoutSeconds);
                IsWaitingForInput = true;
            }
            else
            {
                IsWaitingForInput = false;
            }
        }

        private void OnCaptureError(string error)
        {
            _ = HandleErrorAsync(error);
        }

        private async Task HandleErrorAsync(string error)
        {
            IsWaitingForInput = false;
            if (_uiManager != null)
            {
                await _uiManager.CompleteWithErrorAsync(error);
            }
        }

        public override void Dispose()
        {
            UnsubscribeFromEvents();
            
            _statusManager?.Dispose();
            _captureManager?.Dispose();
            base.Dispose();
            
            _statusManager = null;
            _captureManager = null;
        }
    }
}
