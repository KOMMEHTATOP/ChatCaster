using System.Windows.Controls;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.Services.GamepadService;
using ChatCaster.Windows.Views.ViewSettings;
using ChatCaster.Windows.ViewModels.Settings.Speech;
using AudioSettingsViewModel = ChatCaster.Windows.ViewModels.Settings.AudioSettingsViewModel;  
using Serilog;

namespace ChatCaster.Windows.ViewModels.Navigation
{
    public class NavigationManager
    {
        private readonly Dictionary<string, Page> _cachedPages = new();
        
        // Singleton ViewModel для MainPage
        private MainPageViewModel? _mainPageViewModel;
        
        // Сервисы для создания страниц
        private readonly AudioCaptureService _audioService;
        private readonly SpeechRecognitionService _speechService;
        private readonly MainGamepadService _gamepadService;
        private readonly SystemIntegrationService _systemService;
        private readonly OverlayService _overlayService;
        private readonly ConfigurationService _configService;
        private readonly ServiceContext _serviceContext;

        public string CurrentPageTag { get; private set; } = NavigationConstants.MainPage;
        public Page? CurrentPage { get; private set; }

        // События для уведомления ViewModel
        public event EventHandler<NavigationChangedEventArgs>? NavigationChanged;

        public NavigationManager(
            AudioCaptureService audioService,
            SpeechRecognitionService speechService,
            MainGamepadService gamepadService,
            SystemIntegrationService systemService,
            OverlayService overlayService,
            ConfigurationService configService,
            ServiceContext serviceContext)
        {
            _audioService = audioService;
            _speechService = speechService;
            _gamepadService = gamepadService;
            _systemService = systemService;
            _overlayService = overlayService;
            _configService = configService;
            _serviceContext = serviceContext;

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
            // Создаем ViewModel только один раз
            _mainPageViewModel = new MainPageViewModel(_audioService, _serviceContext);
            
            var mainPage = new MainPageView
            {
                DataContext = _mainPageViewModel
            };

            _cachedPages[NavigationConstants.MainPage] = mainPage;
            CurrentPage = mainPage;
            CurrentPageTag = NavigationConstants.MainPage;
            
            Log.Debug("Главная страница загружена с Singleton ViewModel");
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
                _ => _cachedPages[NavigationConstants.MainPage] // Fallback к главной странице
            };

            _cachedPages[pageTag] = newPage;
            return newPage;
        }
        
        private Page CreateMainPage()
        {
            // Переиспользуем существующий ViewModel
            _mainPageViewModel ??= new ViewModels.MainPageViewModel(_audioService, _serviceContext);
            
            var mainPage = new MainPageView();
            mainPage.DataContext = _mainPageViewModel;
            
            Log.Debug("MainPage создана с переиспользованием ViewModel");
            return mainPage;
        }

        /// <summary>
        /// ✅ НОВЫЙ МЕТОД: Создает страницу Audio с правильной ViewModel
        /// </summary>
        private Page CreateAudioSettingsPage()
        {
            try
            {
                Log.Information("=== СОЗДАНИЕ AUDIO SETTINGS PAGE ===");

                // Создаем View только с нужными сервисами для кнопок
                var audioView = new AudioSettingsView(_audioService, _speechService);
                Log.Information("AudioSettingsView создан");

                // Создаем WhisperModelManager
                var whisperModelManager = new WhisperModelManager(_speechService);
                Log.Information("WhisperModelManager создан");

                // Создаем ViewModel с 3 параметрами
                var audioViewModel = new AudioSettingsViewModel(
                    _configService, 
                    _serviceContext, 
                    whisperModelManager);
                Log.Information("AudioSettingsViewModel создан");

                // ✅ ИСПРАВЛЕНО: Используем SetViewModel вместо прямой установки DataContext
                audioView.SetViewModel(audioViewModel);

                Log.Information("=== AUDIO SETTINGS PAGE ГОТОВА (инициализация через SetViewModel) ===");
                return audioView;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка создания AudioSettingsPage");
                // Возвращаем fallback страницу
                return _cachedPages[NavigationConstants.MainPage];
            }
        }        
        
        /// <summary>
        /// Создает страницу Interface Settings с ViewModel
        /// </summary>
        private Page CreateInterfaceSettingsPage()
        {
            try
            {
                var interfaceView = new InterfaceSettingsView(_overlayService, _configService, _serviceContext);
                
                // 3 параметра согласно реальному конструктору
                var interfaceViewModel = new InterfaceSettingsViewModel(_configService, _serviceContext, _overlayService);
                
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
        /// Создает страницу Control Settings с ViewModel
        /// </summary>
        private Page CreateControlSettingsPage()
        {
            try
            {
                var controlView = new ControlSettingsView(_gamepadService, _systemService, _configService, _serviceContext);
                
                // 4 параметра согласно реальному конструктору
                var controlViewModel = new ControlSettingsViewModel(_configService, _serviceContext, _gamepadService, _systemService);
                
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
            Log.Information("Очистка всех страниц NavigationManager завершена");
        }
    }
}