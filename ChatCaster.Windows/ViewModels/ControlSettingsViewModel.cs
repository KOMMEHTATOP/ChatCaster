using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.Input;
using ChatCaster.Core.Services.System;
using ChatCaster.Windows.Services.GamepadService;
using ChatCaster.Windows.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ChatCaster.Windows.ViewModels
{
    /// <summary>
    /// Упрощенный ViewModel для настроек управления
    /// Координирует компоненты геймпада и клавиатуры
    /// </summary>
    public partial class ControlSettingsViewModel : BaseSettingsViewModel
    {
        private readonly ILocalizationService _localizationService;

        // Локализованные свойства
        [ObservableProperty]
        private string _pageTitle = "Управление";

        [ObservableProperty]
        private string _pageDescription = "Кликните на поле комбинации и нажмите нужные кнопки";

        [ObservableProperty]
        private string _gamepadLabel = "Геймпад:";

        [ObservableProperty]
        private string _keyboardLabel = "Клавиатура:";

        
        #region Components

        // Компоненты
        public GamepadCaptureComponentViewModel GamepadComponent { get; }
        public KeyboardCaptureComponentViewModel KeyboardComponent { get; }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task StartKeyboardCapture()
        {
            Log.Debug("Команда StartKeyboardCapture выполняется");
            await KeyboardComponent.StartCaptureAsync();
        }

        [RelayCommand]
        private async Task StartGamepadCapture()
        {
            Log.Debug("Команда StartGamepadCapture выполняется");
            await GamepadComponent.StartCaptureAsync();
        }

        [RelayCommand]
        private async Task PageUnloaded()
        {
            Log.Debug("Команда PageUnloaded выполняется");
            
            try
            {
                // Cleanup обрабатывается в ViewModel
                await Task.Run(CleanupPageSpecific);
                Log.Debug("ControlSettingsView выгружен через команду");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при выгрузке ControlSettingsView через команду");
            }
        }

        #endregion

        #region Constructor

        public ControlSettingsViewModel(
            IConfigurationService configurationService,
            AppConfig currentConfig,
            IGamepadService gamepadService,
            ISystemIntegrationService systemService,
            GamepadVoiceCoordinator gamepadVoiceCoordinator,
            ILocalizationService localizationService) : base(configurationService, currentConfig)
        {
            _localizationService = localizationService;
            _localizationService.LanguageChanged += OnLanguageChanged;
            UpdateLocalizedStrings();


            Log.Debug("Инициализация ControlSettingsViewModel начата");
            Log.Information("🔍 AudioSettingsViewModel создан с AppConfig HashCode: {HashCode}, SelectedLanguage: {Language}", 
                currentConfig.GetHashCode(), currentConfig.System.SelectedLanguage);

            try
            {
                GamepadComponent = new GamepadCaptureComponentViewModel(gamepadService, currentConfig, gamepadVoiceCoordinator);
                KeyboardComponent = new KeyboardCaptureComponentViewModel(systemService, currentConfig, configurationService);

                // Подписываемся на события компонентов
                SubscribeToComponentEvents();
                
                Log.Information("ControlSettingsViewModel инициализирован успешно");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка инициализации ControlSettingsViewModel");
                throw;
            }
        }

        #endregion

        #region BaseSettingsViewModel Implementation

        protected override async Task LoadPageSpecificSettingsAsync()
        {
            try
            {
                await GamepadComponent.LoadSettingsAsync();
                await KeyboardComponent.LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки настроек управления");
            }
        }

        protected override Task ApplySettingsToConfigAsync(AppConfig config)
        {
            config.System.SelectedLanguage = _currentConfig.System.SelectedLanguage;
            return Task.CompletedTask;
        }

        protected override async Task ApplySettingsToServicesAsync()
        {
            try
            {
                await GamepadComponent.ApplySettingsAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка применения настроек управления к сервисам");
            }
        }

        protected override async Task InitializePageSpecificDataAsync()
        {
            await LoadPageSpecificSettingsAsync();
        }

        public override void SubscribeToUIEvents()
        {
        }

        protected override void UnsubscribeFromUIEvents()
        {
            UnsubscribeFromComponentEvents();
        }

        protected override void CleanupPageSpecific()
        {
            try
            {
                // Отписываемся от событий
                UnsubscribeFromComponentEvents();
                _localizationService.LanguageChanged -= OnLanguageChanged;
                base.CleanupPageSpecific();

                // Освобождаем компоненты
                GamepadComponent?.Dispose();
                KeyboardComponent?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при cleanup ControlSettings");
            }
        }

        #endregion

        #region Component Events Management

        private void SubscribeToComponentEvents()
        {
            try
            {
                // Подписываемся на статусные сообщения от компонентов
                GamepadComponent.StatusMessageChanged += OnComponentStatusMessageChanged;
                KeyboardComponent.StatusMessageChanged += OnComponentStatusMessageChanged;
                
                // Подписываемся на изменения настроек
                GamepadComponent.SettingChanged += OnComponentSettingChangedAsync;
                KeyboardComponent.SettingChanged += OnComponentSettingChangedAsync;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка подписки на события компонентов");
            }
        }

        private void UnsubscribeFromComponentEvents()
        {
            try
            {
                // Отписываемся от статусных сообщений
                GamepadComponent.StatusMessageChanged -= OnComponentStatusMessageChanged;
                KeyboardComponent.StatusMessageChanged -= OnComponentStatusMessageChanged;
                
                // Отписываемся от изменений настроек
                GamepadComponent.SettingChanged -= OnComponentSettingChangedAsync;
                KeyboardComponent.SettingChanged -= OnComponentSettingChangedAsync;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка отписки от событий компонентов");
            }
        }

        #endregion

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLocalizedStrings();
        }

        private void UpdateLocalizedStrings()
        {
            PageTitle = _localizationService.GetString("Control_PageTitle");
            PageDescription = _localizationService.GetString("Control_PageDescription");
            GamepadLabel = _localizationService.GetString("Control_Gamepad");
            KeyboardLabel = _localizationService.GetString("Control_Keyboard");
        }
        
        

        #region Event Handlers

        private void OnComponentStatusMessageChanged(string message)
        {
            try
            {
                StatusMessage = message;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки статусного сообщения от компонента");
            }
        }

        private async Task OnComponentSettingChangedAsync()
        {
            try
            {
                await OnUISettingChangedAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки изменения настройки компонента");
            }
        }

        #endregion
    }
}