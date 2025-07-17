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
                var audioView = new AudioSettingsView();
                var audioViewModel = _serviceProvider.GetRequiredService<AudioSettingsViewModel>();

                audioView.SetViewModel(audioViewModel);

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
                var interfaceViewModel = _serviceProvider.GetRequiredService<InterfaceSettingsViewModel>();
                var interfaceView = new InterfaceSettingsView
                {
                    DataContext = interfaceViewModel
                };

                _ = interfaceViewModel.InitializeAsync();

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
                var controlViewModel = _serviceProvider.GetRequiredService<ControlSettingsViewModel>();
                var controlView = new ControlSettingsView
                {
                    DataContext = controlViewModel
                };

                _ = controlViewModel.InitializeAsync();

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