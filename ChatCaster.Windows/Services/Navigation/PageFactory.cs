using System.Windows.Controls;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.Input;
using ChatCaster.Core.Services.Overlay;
using ChatCaster.Core.Services.System;
using ChatCaster.Core.Services.UI;
using ChatCaster.Windows.Services.GamepadService;
using ChatCaster.Windows.ViewModels;
using ChatCaster.Windows.Views.ViewSettings;
using Serilog;

namespace ChatCaster.Windows.Services.Navigation
{
    /// <summary>
    /// Фабрика для создания страниц приложения
    /// Отвечает только за создание и инициализацию страниц с их ViewModels
    /// </summary>
    public class PageFactory
    {
        private readonly IAudioCaptureService _audioService;
        private readonly ISpeechRecognitionService _speechService;
        private readonly IGamepadService _gamepadService;
        private readonly ISystemIntegrationService _systemService;
        private readonly IOverlayService _overlayService;
        private readonly IConfigurationService _configService;
        private readonly AppConfig _currentConfig;
        private readonly IVoiceRecordingService _voiceRecordingService;
        private readonly GamepadVoiceCoordinator _gamepadVoiceCoordinator;
        private readonly INotificationService _notificationService;

        public PageFactory(
            IAudioCaptureService audioService,
            ISpeechRecognitionService speechService,
            IGamepadService gamepadService,
            ISystemIntegrationService systemService,
            IOverlayService overlayService,
            IConfigurationService configService,
            AppConfig currentConfig,
            IVoiceRecordingService voiceRecordingService,
            GamepadVoiceCoordinator gamepadVoiceCoordinator,
            INotificationService notificationService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _speechService = speechService ?? throw new ArgumentNullException(nameof(speechService));
            _gamepadService = gamepadService ?? throw new ArgumentNullException(nameof(gamepadService));
            _systemService = systemService ?? throw new ArgumentNullException(nameof(systemService));
            _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _currentConfig = currentConfig ?? throw new ArgumentNullException(nameof(currentConfig));
            _voiceRecordingService = voiceRecordingService ?? throw new ArgumentNullException(nameof(voiceRecordingService));
            _gamepadVoiceCoordinator = gamepadVoiceCoordinator ?? throw new ArgumentNullException(nameof(gamepadVoiceCoordinator));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

            Log.Debug("PageFactory инициализирован");
        }

        /// <summary>
        /// Создает главную страницу с переданной ViewModel
        /// </summary>
        public Page CreateMainPage(MainPageViewModel mainPageViewModel)
        {
            try
            {
                var mainPage = new MainPageView
                {
                    DataContext = mainPageViewModel
                };

                Log.Debug("PageFactory: MainPage создана");
                return mainPage;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PageFactory: ошибка создания MainPage");
                throw;
            }
        }

        /// <summary>
        /// Создает страницу настроек аудио
        /// </summary>
        public Page CreateAudioSettingsPage()
        {
            try
            {
                Log.Information("PageFactory: создание AudioSettingsPage");

                var audioView = new AudioSettingsView();
                var audioViewModel = new AudioSettingsViewModel(
                    _configService,
                    _configService.CurrentConfig,
                    _speechService,
                    _audioService,
                    _notificationService);

                audioView.SetViewModel(audioViewModel);

                Log.Information("PageFactory: AudioSettingsPage создана");
                return audioView;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PageFactory: ошибка создания AudioSettingsPage");
                throw;
            }
        }

        /// <summary>
        /// Создает страницу настроек интерфейса
        /// </summary>
        public Page CreateInterfaceSettingsPage()
        {
            try
            {
                Log.Debug("PageFactory: создание InterfaceSettingsPage");

                var interfaceView = new InterfaceSettingsView(_overlayService, _configService, _currentConfig);
                var interfaceViewModel = new InterfaceSettingsViewModel(_configService, _currentConfig, _overlayService);

                interfaceView.DataContext = interfaceViewModel;

                // Инициализируем ViewModel
                _ = interfaceViewModel.InitializeAsync();

                Log.Debug("PageFactory: InterfaceSettingsPage создана");
                return interfaceView;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PageFactory: ошибка создания InterfaceSettingsPage");
                throw;
            }
        }

        /// <summary>
        /// Создает страницу настроек управления
        /// </summary>
        public Page CreateControlSettingsPage()
        {
            try
            {
                Log.Debug("PageFactory: создание ControlSettingsPage");

                var controlView = new ControlSettingsView(
                    _gamepadService, _systemService, _configService, _currentConfig, _gamepadVoiceCoordinator);
                var controlViewModel = new ControlSettingsViewModel(
                    _configService, _currentConfig, _gamepadService, _systemService, _gamepadVoiceCoordinator);

                controlView.DataContext = controlViewModel;

                // Инициализируем ViewModel
                _ = controlViewModel.InitializeAsync();

                Log.Debug("PageFactory: ControlSettingsPage создана");
                return controlView;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PageFactory: ошибка создания ControlSettingsPage");
                throw;
            }
        }
    }
}