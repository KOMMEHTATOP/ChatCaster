using System.Windows.Controls;
using ChatCaster.Windows.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ChatCaster.Windows.ViewModels.Navigation
{
    /// <summary>
    /// Менеджер навигации между страницами
    /// Ответственности: только навигация между страницами и управление их жизненным циклом
    /// </summary>
    public class NavigationManager
    {
        private readonly PageFactory _pageFactory;
        private readonly PageCacheManager _pageCacheManager;
        private readonly ViewModelCleanupService _cleanupService;
        private readonly IServiceProvider _serviceProvider;

        // Singleton ViewModel для MainPage
        private MainPageViewModel? _mainPageViewModel;

        public string CurrentPageTag { get; private set; } = NavigationConstants.MainPage;
        public Page? CurrentPage { get; private set; }

        // События для уведомления родительской ViewModel
        public event EventHandler<NavigationChangedEventArgs>? NavigationChanged;

        public NavigationManager(
            PageFactory pageFactory,
            PageCacheManager pageCacheManager,
            ViewModelCleanupService cleanupService,
            IServiceProvider serviceProvider)
        {
            _pageFactory = pageFactory ?? throw new ArgumentNullException(nameof(pageFactory));
            _pageCacheManager = pageCacheManager ?? throw new ArgumentNullException(nameof(pageCacheManager));
            _cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // Загружаем главную страницу по умолчанию
            LoadMainPage();

            Log.Debug("NavigationManager инициализирован с IServiceProvider");
        }

        /// <summary>
        /// Навигация на указанную страницу
        /// </summary>
        public void NavigateToPage(string pageTag)
        {
            if (string.IsNullOrEmpty(pageTag) || pageTag == CurrentPageTag)
                return;

            try
            {
                var page = _pageCacheManager.GetOrCreatePage(pageTag, CreatePageByTag);
                CurrentPage = page;
                CurrentPageTag = pageTag;

                Log.Debug("NavigationManager: навигация на страницу: {PageTag}", pageTag);

                // Уведомляем родительскую ViewModel об изменении
                NavigationChanged?.Invoke(this, new NavigationChangedEventArgs(pageTag, page));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "NavigationManager: ошибка навигации на страницу {PageTag}", pageTag);
            }
        }

        /// <summary>
        /// Переход к настройкам интерфейса
        /// </summary>
        public void NavigateToSettings()
        {
            NavigateToPage(NavigationConstants.InterfacePage);
        }

        /// <summary>
        /// Очистка всех ресурсов при закрытии приложения
        /// </summary>
        public void CleanupAllPages()
        {
            Log.Information("NavigationManager: начинаем cleanup всех страниц");

            try
            {
                // Делегируем cleanup сервису
                var allPages = _pageCacheManager.GetAllCachedPages();
                _cleanupService.CleanupAllViewModels(allPages, _mainPageViewModel);

                // Очищаем кеш
                _pageCacheManager.ClearCache();

                // Сбрасываем singleton
                _mainPageViewModel = null;

                Log.Information("NavigationManager: cleanup всех страниц завершен");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "NavigationManager: критическая ошибка при cleanup");
            }
        }

        private void LoadMainPage()
        {
            try
            {
                // Получаем Singleton MainPageViewModel из DI
                _mainPageViewModel = _serviceProvider.GetRequiredService<MainPageViewModel>();

                // Создаем страницу через фабрику
                var mainPage = _pageFactory.CreateMainPage(_mainPageViewModel);

                // Кешируем страницу
                _pageCacheManager.CachePage(NavigationConstants.MainPage, mainPage);

                CurrentPage = mainPage;
                CurrentPageTag = NavigationConstants.MainPage;

                Log.Debug("NavigationManager: главная страница загружена из DI");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "NavigationManager: ошибка загрузки главной страницы");
                throw;
            }
        }

        private Page CreatePageByTag(string pageTag)
        {
            return pageTag switch
            {
                NavigationConstants.MainPage => GetMainPageFromCache(),
                NavigationConstants.AudioPage => _pageFactory.CreateAudioSettingsPage(),
                NavigationConstants.InterfacePage => _pageFactory.CreateInterfaceSettingsPage(),
                NavigationConstants.ControlPage => _pageFactory.CreateControlSettingsPage(),
                _ => GetMainPageFromCache()
            };
        }

        private Page GetMainPageFromCache()
        {
            var mainPage = _pageCacheManager.GetCachedPage(NavigationConstants.MainPage);
            if (mainPage == null)
            {
                Log.Error("NavigationManager: MainPage не найдена в кеше, это критическая ошибка");
                throw new InvalidOperationException("MainPage должна быть всегда доступна");
            }
            return mainPage;
        }
    }
}