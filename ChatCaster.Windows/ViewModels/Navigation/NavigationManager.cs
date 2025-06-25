using System.Windows.Controls;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.Views.ViewSettings;
using Serilog;

namespace ChatCaster.Windows.ViewModels.Navigation
{
    public class NavigationManager
    {
        private readonly Dictionary<string, Page> _cachedPages = new();
        
        // Singleton ViewModel для MainPage
        private ViewModels.MainPageViewModel? _mainPageViewModel;
        
        // Сервисы для создания страниц
        private readonly AudioCaptureService _audioService;
        private readonly SpeechRecognitionService _speechService;
        private readonly Services.GamepadService.MainGamepadService _gamepadService;
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
            Services.GamepadService.MainGamepadService gamepadService,
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
            _mainPageViewModel = new ViewModels.MainPageViewModel(_audioService, _serviceContext);
            
            var mainPage = new MainPageView();
            mainPage.DataContext = _mainPageViewModel;
            
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
                NavigationConstants.AudioPage => new AudioSettingsView(_audioService, _speechService, _configService, _serviceContext),
                NavigationConstants.InterfacePage => new InterfaceSettingsView(_overlayService, _configService, _serviceContext),
                NavigationConstants.ControlPage => new ControlSettingsView(_gamepadService, _systemService, _configService, _serviceContext),
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