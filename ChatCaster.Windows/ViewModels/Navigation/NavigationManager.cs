using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels.Settings;
using ChatCaster.Windows.Views.ViewSettings;

namespace ChatCaster.Windows.ViewModels.Navigation
{
    public class NavigationManager
    {
        private readonly Dictionary<string, Page> _cachedPages = new();
        
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

            // Уведомляем ViewModel об изменении
            NavigationChanged?.Invoke(this, new NavigationChangedEventArgs(pageTag, page));
        }

        public void NavigateToSettings()
        {
            NavigateToPage(NavigationConstants.InterfacePage);
        }

        public MainPageView? GetMainPageIfVisible()
        {
            return CurrentPageTag == NavigationConstants.MainPage && 
                   _cachedPages.TryGetValue(NavigationConstants.MainPage, out var page) && 
                   page is MainPageView mainPage ? mainPage : null;
        }

        private void LoadMainPage()
        {
            var mainPage = new MainPageView(_audioService, _serviceContext);
            _cachedPages[NavigationConstants.MainPage] = mainPage;
            CurrentPage = mainPage;
            CurrentPageTag = NavigationConstants.MainPage;
        }

        private Page GetOrCreatePage(string pageTag)
        {
            if (_cachedPages.TryGetValue(pageTag, out var cachedPage))
                return cachedPage;

            Page newPage = pageTag switch
            {
                NavigationConstants.MainPage => new MainPageView(_audioService, _serviceContext),
                NavigationConstants.AudioPage => new AudioSettingsView(_audioService, _speechService, _configService, _serviceContext),
                NavigationConstants.InterfacePage => new InterfaceSettingsView(_overlayService, _configService, _serviceContext),
                NavigationConstants.ControlPage => new ControlSettingsView(_gamepadService, _systemService, _configService, _serviceContext),
                _ => _cachedPages[NavigationConstants.MainPage] // Fallback к главной странице
            };

            _cachedPages[pageTag] = newPage;
            return newPage;
        }
        
        // В NavigationManager.cs добавить в конец класса:

public void CleanupAllPages()
{
    Console.WriteLine("🧹 [NavigationManager] Очистка всех страниц...");
    
    foreach (var kvp in _cachedPages)
    {
        var pageTag = kvp.Key;
        var page = kvp.Value;
        
        try
        {
            Console.WriteLine($"🧹 [NavigationManager] Очищаем страницу: {pageTag}");
            
            // Очищаем ViewModels страниц
            switch (page)
            {
                case ControlSettingsView controlPage:
                    // Получаем ViewModel из DataContext
                    if (controlPage.DataContext is ControlSettingsViewModel controlVM)
                    {
                        Console.WriteLine("🧹 [NavigationManager] Вызываем Cleanup для ControlSettingsViewModel");
                        controlVM.Cleanup();
                    }
                    break;
                    
                case AudioSettingsView audioPage:
                    if (audioPage.DataContext is AudioSettingsViewModel audioVM)
                    {
                        Console.WriteLine("🧹 [NavigationManager] Вызываем Cleanup для AudioSettingsViewModel");
                        audioVM.Cleanup();
                    }
                    break;
                    
                case InterfaceSettingsView interfacePage:
                    if (interfacePage.DataContext is InterfaceSettingsViewModel interfaceVM)
                    {
                        Console.WriteLine("🧹 [NavigationManager] Вызываем Cleanup для InterfaceSettingsViewModel");
                        interfaceVM.Cleanup();
                    }
                    break;
                    
                // MainPageView не имеет Cleanup, пропускаем
                case MainPageView:
                    Console.WriteLine("🧹 [NavigationManager] MainPageView не требует Cleanup");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [NavigationManager] Ошибка очистки страницы {pageTag}: {ex.Message}");
        }
    }
    
    _cachedPages.Clear();
    Console.WriteLine("✅ [NavigationManager] Очистка всех страниц завершена");
}
    }
}