using System.Windows.Controls;
using ChatCaster.Windows.ViewModels;
using ChatCaster.Windows.Views.ViewSettings;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ChatCaster.Windows.Services.Navigation
{
    /// <summary>
    /// Фабрика для создания страниц приложения
    /// Отвечает только за создание и инициализацию страниц с их ViewModels
    /// </summary>
    public class PageFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public PageFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            Log.Debug("PageFactory инициализирован с IServiceProvider");
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
                var audioViewModel = _serviceProvider.GetRequiredService<AudioSettingsViewModel>();

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

                var interfaceViewModel = _serviceProvider.GetRequiredService<InterfaceSettingsViewModel>();
                var interfaceView = new InterfaceSettingsView
                {
                    DataContext = interfaceViewModel
                };

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

                var controlViewModel = _serviceProvider.GetRequiredService<ControlSettingsViewModel>();
                var controlView = new ControlSettingsView
                {
                    DataContext = controlViewModel
                };

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