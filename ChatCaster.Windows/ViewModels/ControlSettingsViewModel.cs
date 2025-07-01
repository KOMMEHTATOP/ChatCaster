using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.Services.GamepadService;
using ChatCaster.Windows.ViewModels.Base;
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
        #region Additional Services

        private readonly IGamepadService _gamepadService;
        private readonly ISystemIntegrationService _systemService;
        private readonly GamepadVoiceCoordinator _gamepadVoiceCoordinator;

        #endregion

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

        // ✅ ИСПРАВЛЕНО: Конструктор без ServiceContext
        public ControlSettingsViewModel(
            IConfigurationService configurationService,
            AppConfig currentConfig,
            IGamepadService gamepadService,
            ISystemIntegrationService systemService,
            GamepadVoiceCoordinator gamepadVoiceCoordinator) : base(configurationService, currentConfig)
        {
            _gamepadService = gamepadService ?? throw new ArgumentNullException(nameof(gamepadService));
            _systemService = systemService ?? throw new ArgumentNullException(nameof(systemService));
            _gamepadVoiceCoordinator = gamepadVoiceCoordinator ?? throw new ArgumentNullException(nameof(gamepadVoiceCoordinator));

            Log.Debug("Инициализация ControlSettingsViewModel начата");
            
            try
            {
                // ✅ ИСПРАВЛЕНО: Создаем компоненты без ServiceContext
                GamepadComponent = new GamepadCaptureComponentViewModel(gamepadService, currentConfig, gamepadVoiceCoordinator);
                KeyboardComponent = new KeyboardCaptureComponentViewModel(systemService, currentConfig);

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
                Log.Debug("Загружаем настройки компонентов...");
                
                await GamepadComponent.LoadSettingsAsync();
                await KeyboardComponent.LoadSettingsAsync();
                
                Log.Information("Настройки управления загружены успешно");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки настроек управления");
            }
        }

        protected override Task ApplySettingsToConfigAsync(AppConfig config)
        {
            // Настройки уже сохранены в компонентах при их изменении
            Log.Debug("Применение настроек к конфигурации (выполнено компонентами)");
            return Task.CompletedTask;
        }

        protected override async Task ApplySettingsToServicesAsync()
        {
            try
            {
                Log.Debug("Применяем настройки к сервисам...");
                
                // Применяем настройки геймпада к сервисам
                await GamepadComponent.ApplySettingsAsync();
                
                Log.Information("Настройки управления применены к сервисам");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка применения настроек управления к сервисам");
            }
        }

        protected override async Task InitializePageSpecificDataAsync()
        {
            Log.Debug("Специальная инициализация ControlSettings начата");
            await LoadPageSpecificSettingsAsync();
        }

        public override void SubscribeToUIEvents()
        {
            // События обрабатываются компонентами автоматически
            Log.Debug("UI события подписаны через компоненты");
        }

        protected override void UnsubscribeFromUIEvents()
        {
            Log.Debug("Отписываемся от событий компонентов");
            UnsubscribeFromComponentEvents();
        }

        protected override void CleanupPageSpecific()
        {
            try
            {
                Log.Debug("Cleanup ControlSettings начат");
                
                // Отписываемся от событий
                UnsubscribeFromComponentEvents();
                
                // Освобождаем компоненты
                GamepadComponent?.Dispose();
                KeyboardComponent?.Dispose();
                
                Log.Information("Cleanup ControlSettings завершен успешно");
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
                
                Log.Debug("События компонентов подписаны");
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
                
                Log.Debug("События компонентов отписаны");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка отписки от событий компонентов");
            }
        }

        #endregion

        #region Event Handlers

        private void OnComponentStatusMessageChanged(string message)
        {
            try
            {
                StatusMessage = message;
                Log.Debug("Статусное сообщение от компонента: {Message}", message);
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
                Log.Debug("Настройка изменена в компоненте, уведомляем базовый класс");
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