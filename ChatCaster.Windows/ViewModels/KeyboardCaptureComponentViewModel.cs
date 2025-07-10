using ChatCaster.Core.Constants;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.System;
using ChatCaster.Windows.Managers;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace ChatCaster.Windows.ViewModels
{
    /// <summary>
    /// Компонент для управления настройками клавиатуры
    /// </summary>
    public partial class KeyboardCaptureComponentViewModel : BaseCaptureComponentViewModel
    {
        private readonly ISystemIntegrationService _systemService;
        private readonly AppConfig _currentConfig;
        private readonly IConfigurationService _configurationService;
        
        private KeyboardCaptureManager? _captureManager;

        // Статус клавиатуры
        [ObservableProperty]
        private string _statusText = "Клавиатура готова";

        [ObservableProperty]
        private string _statusColor = "#4caf50";

        public KeyboardCaptureComponentViewModel(
            ISystemIntegrationService systemService,
            AppConfig currentConfig,
            IConfigurationService configurationService) 
        {
            _systemService = systemService ?? throw new ArgumentNullException(nameof(systemService));
            _currentConfig = currentConfig ?? throw new ArgumentNullException(nameof(currentConfig));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService)); 
    
            InitializeManagers();
        }
        
        private void InitializeManagers()
        {
            try
            {
                _captureManager = new KeyboardCaptureManager();
                _uiManager = new CaptureUIStateManager();

                SubscribeToEvents();
                
                Log.Debug("KeyboardCaptureComponent инициализирован");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка инициализации KeyboardCaptureComponent");
                throw;
            }
        }

        public Task LoadSettingsAsync()
        {
            try
            {
                var shortcut = _currentConfig.Input.KeyboardShortcut;
                ComboText = shortcut?.DisplayText ?? "Ctrl + Shift + R";
                Log.Information("🔍 LoadSettingsAsync установил ComboText: {ComboText}", ComboText);
                _uiManager?.SetIdleState(ComboText);
                
                Log.Debug("Настройки клавиатуры загружены: {Combo}", ComboText);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки настроек клавиатуры");
                return Task.CompletedTask;
            }
        }

        public override async Task StartCaptureAsync()
        {
            if (IsWaitingForInput || _captureManager == null)
            {
                Log.Debug("Захват клавиатуры уже активен или менеджер недоступен");
                return;
            }

            try
            {
                Log.Debug("Запуск захвата клавиатуры");
                await _captureManager.StartCaptureAsync(AppConstants.CaptureTimeoutSeconds);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка запуска захвата клавиатуры");
                OnStatusMessageChanged($"Ошибка захвата клавиатуры: {ex.Message}");
            }
        }

        private void SubscribeToEvents()
        {
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

            _configurationService.ConfigurationChanged += OnConfigurationChanged;
        }

        private void UnsubscribeFromEvents()
        {
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
            _configurationService.ConfigurationChanged -= OnConfigurationChanged;  
        }
        
        private void OnConfigurationChanged(object? sender, ConfigurationChangedEvent e)
        {
            Log.Information("🔍 KeyboardComponent получил событие конфигурации: {SettingName}", e.SettingName);
            _ = LoadSettingsAsync();
        }

        // Event handlers
        private void OnCaptureCompleted(KeyboardShortcut capturedShortcut)
        {
            _ = HandleCaptureCompletedAsync(capturedShortcut);
        }

        private async Task HandleCaptureCompletedAsync(KeyboardShortcut capturedShortcut)
        {
            try
            {
                IsWaitingForInput = false;
        
                // обновляем только источник истины
                _currentConfig.Input.KeyboardShortcut = capturedShortcut;
                await OnSettingChangedAsync(); 

                // Регистрируем глобальный хоткей
                bool registered = await _systemService.RegisterGlobalHotkeyAsync(capturedShortcut);
                if (!registered)
                {
                    if (_uiManager != null)
                    {
                        await _uiManager.CompleteWithErrorAsync("Ошибка регистрации хоткея");
                    }
                    return;
                }

                if (_uiManager != null)
                {
                    await _uiManager.CompleteSuccessAsync(capturedShortcut.DisplayText);
                }
        
                Log.Information("Клавиатурная комбинация сохранена: {Shortcut}", capturedShortcut.DisplayText);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки захваченной клавиатурной комбинации");
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
            
            _captureManager?.Dispose();
            base.Dispose();
            
            _captureManager = null;
        }
    }
}