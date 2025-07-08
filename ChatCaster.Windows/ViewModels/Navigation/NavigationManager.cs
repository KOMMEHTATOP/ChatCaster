using System.Windows.Controls;
using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Views.ViewSettings;
using ChatCaster.Windows.Services.GamepadService;
using Serilog;

namespace ChatCaster.Windows.ViewModels.Navigation
{
    public class NavigationManager
    {
        private readonly Dictionary<string, Page> _cachedPages = new();

        // Singleton ViewModel для MainPage
        private MainPageViewModel? _mainPageViewModel;

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

        public string CurrentPageTag { get; private set; } = NavigationConstants.MainPage;
        public Page? CurrentPage { get; private set; }

        // События для уведомления ViewModel
        public event EventHandler<NavigationChangedEventArgs>? NavigationChanged;

        public NavigationManager(
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
            _audioService = audioService;
            _speechService = speechService;
            _gamepadService = gamepadService;
            _systemService = systemService;
            _overlayService = overlayService;
            _configService = configService;
            _currentConfig = currentConfig;
            _voiceRecordingService = voiceRecordingService;
            _gamepadVoiceCoordinator = gamepadVoiceCoordinator;
            _notificationService = notificationService; 

            // Загружаем главную страницу по умолчанию
            LoadMainPage();
        }

        public void NavigateToPage(string pageTag)
        {
            if (string.IsNullOrEmpty(pageTag) || pageTag == CurrentPageTag)
                return;

            var page = GetOrCreatePage(pageTag);
            CurrentPage = page;
            CurrentPageTag = pageTag;

            Log.Debug("Навигация на страницу: {PageTag}", pageTag);

            // Уведомляем ViewModel об изменении
            NavigationChanged?.Invoke(this, new NavigationChangedEventArgs(pageTag, page));
        }

        public void NavigateToSettings()
        {
            NavigateToPage(NavigationConstants.InterfacePage);
        }

        private void LoadMainPage()
        {
            _mainPageViewModel =
                new MainPageViewModel(_audioService, _voiceRecordingService, _currentConfig, _notificationService, _configService); // ✅ ДОБАВЛЕН _configService

            var mainPage = new MainPageView
            {
                DataContext = _mainPageViewModel
            };

            _cachedPages[NavigationConstants.MainPage] = mainPage;
            CurrentPage = mainPage;
            CurrentPageTag = NavigationConstants.MainPage;

            Log.Debug("Главная страница загружена с Singleton ViewModel и IConfigurationService");
        }

        private Page GetOrCreatePage(string pageTag)
        {
            if (_cachedPages.TryGetValue(pageTag, out var cachedPage))
            {
                Log.Debug("Используем кешированную страницу: {PageTag}", pageTag);
                return cachedPage;
            }

            Log.Debug("Создаем новую страницу: {PageTag}", pageTag);

            Page newPage = pageTag switch
            {
                NavigationConstants.MainPage => CreateMainPage(),
                NavigationConstants.AudioPage => CreateAudioSettingsPage(),
                NavigationConstants.InterfacePage => CreateInterfaceSettingsPage(),
                NavigationConstants.ControlPage => CreateControlSettingsPage(),
                _ => _cachedPages[NavigationConstants.MainPage]
            };

            _cachedPages[pageTag] = newPage;
            return newPage;
        }

        private Page CreateMainPage()
        {
            _mainPageViewModel ??=
                new ViewModels.MainPageViewModel(_audioService, _voiceRecordingService, _currentConfig, _notificationService, _configService); // ✅ ДОБАВЛЕН _configService

            var mainPage = new MainPageView();
            mainPage.DataContext = _mainPageViewModel;

            Log.Debug("MainPage создана с переиспользованием ViewModel и IConfigurationService");
            return mainPage;
        }
        
        /// <summary>
        /// Создает страницу Audio без ServiceContext
        /// </summary>
        private Page CreateAudioSettingsPage()
        {
            try
            {
                Log.Information("=== СОЗДАНИЕ AUDIO SETTINGS PAGE ===");

                var audioView = new AudioSettingsView(_audioService);
                Log.Information("AudioSettingsView создан");

                var audioViewModel = new AudioSettingsViewModel(
                    _configService, 
                    _configService.CurrentConfig, 
                    _speechService,
                    _audioService); 
                Log.Information("AudioSettingsViewModel создан");

                audioView.SetViewModel(audioViewModel);

                Log.Information("=== AUDIO SETTINGS PAGE ГОТОВА (новый Whisper модуль) ===");
                return audioView;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка создания AudioSettingsPage");
                return _cachedPages[NavigationConstants.MainPage];
            }
        }
        
        /// <summary>
        /// Создает страницу Interface Settings
        /// </summary>
        private Page CreateInterfaceSettingsPage()
        {
            try
            {
                var interfaceView = new InterfaceSettingsView(_overlayService, _configService, _configService.CurrentConfig);
        
                var interfaceViewModel = new InterfaceSettingsViewModel(_configService, _configService.CurrentConfig, _overlayService);
        
                interfaceView.DataContext = interfaceViewModel;
        
                // Инициализируем ViewModel
                _ = interfaceViewModel.InitializeAsync();
        
                return interfaceView;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка создания InterfaceSettingsPage");
                return _cachedPages[NavigationConstants.MainPage];
            }
        }

        /// <summary>
        /// Создает страницу Control Settings
        /// </summary>
        private Page CreateControlSettingsPage()
        {
            try
            {
                var controlView = new ControlSettingsView(_gamepadService, _systemService, _configService, _configService.CurrentConfig, _gamepadVoiceCoordinator);
        
                var controlViewModel = new ControlSettingsViewModel(_configService, _configService.CurrentConfig, _gamepadService, _systemService, _gamepadVoiceCoordinator);

                controlView.DataContext = controlViewModel;

                // Инициализируем ViewModel
                _ = controlViewModel.InitializeAsync();

                return controlView;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка создания ControlSettingsPage");
                return _cachedPages[NavigationConstants.MainPage];
            }
        }

        public void CleanupAllPages()
        {
            Log.Information("Очистка всех страниц NavigationManager...");

            try
            {
                // Выполняем cleanup в UI потоке
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    // Сначала очищаем Singleton MainPageViewModel
                    if (_mainPageViewModel != null)
                    {
                        Log.Debug("Вызываем Cleanup для Singleton MainPageViewModel");
                        _mainPageViewModel.Cleanup();
                        _mainPageViewModel = null;
                    }

                    foreach (var kvp in _cachedPages)
                    {
                        var pageTag = kvp.Key;
                        var page = kvp.Value;

                        try
                        {
                            Log.Debug("Очищаем страницу: {PageTag}", pageTag);

                            // Очищаем ViewModels страниц (кроме MainPage - уже очищен выше)
                            switch (page)
                            {
                                case MainPageView:
                                    // MainPageViewModel уже очищен выше как Singleton
                                    Log.Debug("MainPageView - ViewModel уже очищен");
                                    break;

                                case ControlSettingsView controlPage:
                                    if (controlPage.DataContext is ControlSettingsViewModel controlVM)
                                    {
                                        Log.Debug("Вызываем Cleanup для ControlSettingsViewModel");
                                        controlVM.Cleanup();
                                    }

                                    break;

                                case AudioSettingsView audioPage:
                                    if (audioPage.DataContext is AudioSettingsViewModel audioVM)
                                    {
                                        Log.Debug("Вызываем Cleanup для AudioSettingsViewModel");
                                        audioVM.Cleanup();
                                    }

                                    break;

                                case InterfaceSettingsView interfacePage:
                                    if (interfacePage.DataContext is InterfaceSettingsViewModel interfaceVM)
                                    {
                                        Log.Debug("Вызываем Cleanup для InterfaceSettingsViewModel");
                                        interfaceVM.Cleanup();
                                    }

                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Ошибка очистки страницы {PageTag}", pageTag);
                        }
                    }

                    _cachedPages.Clear();
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Критическая ошибка при очистке NavigationManager");

                // ✅ Fallback: Принудительно очищаем коллекцию
                try
                {
                    _cachedPages.Clear();
                    _mainPageViewModel = null;
                }
                catch (Exception fallbackEx)
                {
                    Log.Fatal(fallbackEx, "Не удалось выполнить fallback очистку NavigationManager");
                }
            }

            Log.Information("Очистка всех страниц NavigationManager завершена");
        }
    }
}