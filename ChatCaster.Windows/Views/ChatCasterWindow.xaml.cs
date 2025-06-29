using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using ChatCaster.Windows.Services;
using ChatCaster.Core.Models;
using ChatCaster.Windows.ViewModels;
using System.Windows.Threading;
using WpfKey = System.Windows.Input.Key;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;
using ChatCaster.Core.Logging;
using Serilog;

namespace ChatCaster.Windows.Views
{
    public partial class ChatCasterWindow
    {
        private readonly ChatCasterWindowViewModel _viewModel;

        public ChatCasterWindow()
        {
            InitializeComponent();

            // Настройка кодировки консоли
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;

            // Инициализация логирования
            InitializeLogging();

            Log.Information("ChatCaster запускается...");
            Log.Debug("Инициализация сервисов");

            // Создание сервисов
            var audioService = new AudioCaptureService();
            var speechService = new SpeechRecognitionService();
            var gamepadService = new Services.GamepadService.MainGamepadService();
            var systemService = new SystemIntegrationService();
            var overlayService = new OverlayService();
            var configService = new ConfigurationService();

            Log.Debug("Основные сервисы созданы");

            // Создание VoiceRecordingService
            var voiceRecordingService = new VoiceRecordingService(
                audioService,
                speechService,
                configService
            );

            overlayService.SubscribeToVoiceService(voiceRecordingService, configService);

            // Создание ServiceContext
            var serviceContext = new ServiceContext(new AppConfig())
            {
                GamepadService = gamepadService,
                AudioService = audioService,
                SpeechService = speechService,
                SystemService = systemService,
                OverlayService = overlayService,
                ConfigurationService = configService,
                VoiceRecordingService = voiceRecordingService
            };

            // Создание TrayService
            var trayService = new TrayService(this);
            trayService.Initialize();

            Log.Debug("TrayService инициализирован");

            var gamepadVoiceCoordinator = new Services.GamepadService.GamepadVoiceCoordinator(
                gamepadService,
                voiceRecordingService,
                systemService,
                configService,
                trayService);


            // Добавляем координатор в ServiceContext
            serviceContext.GamepadVoiceCoordinator = gamepadVoiceCoordinator;

            // Создание ViewModel
            _viewModel = new ChatCasterWindowViewModel(
                audioService,
                speechService,
                gamepadService,
                systemService,
                overlayService,
                configService,
                serviceContext,
                trayService
            );

            // Установка DataContext
            DataContext = _viewModel;

            // Подписка на события (убираем PropertyChanged для навигации)
            Closing += ChatCasterWindow_Closing;
            Loaded += ChatCasterWindow_Loaded;

            Log.Information("ChatCaster успешно инициализирован");
        }

        private void InitializeLogging()
        {
            try
            {
                // Создаем дефолтную конфигурацию логирования
                var loggingConfig = new LoggingConfig();

                // В Debug режиме включаем консольный вывод
#if DEBUG
                loggingConfig.EnableConsoleLogging = true;
                loggingConfig.MinimumLevel = LogLevel.Debug;
#else
               loggingConfig.EnableConsoleLogging = false;
               loggingConfig.MinimumLevel = LogLevel.Information;
#endif

                // Инициализируем глобальный логгер Serilog
                Log.Logger = LoggingConfiguration.CreateLogger(loggingConfig);

                Log.Information("Система логирования инициализирована");
            }
            catch (Exception ex)
            {
                // Если логирование не удалось инициализировать, выводим в консоль
                Console.WriteLine($"Ошибка инициализации логирования: {ex.Message}");
            }
        }

        private async void ChatCasterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Debug("Загрузка главного окна");

                // Сначала устанавливаем начальную страницу БЕЗ анимации
                if (_viewModel.CurrentPage != null)
                {
                    ContentFrame.Navigate(_viewModel.CurrentPage);
                }

                // Затем инициализируем ViewModel
                await _viewModel.InitializeAsync();

                // Устанавливаем фокус через Dispatcher чтобы дождаться полной загрузки
                await Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    Activate();
                    Focus();
                    Keyboard.Focus(this);
                }));

                Log.Information("Главное окно загружено и готово к работе");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при загрузке главного окна");
            }
        }

        private void ChatCasterWindow_Closing(object? sender, CancelEventArgs e)
        {
            Log.Debug("Попытка закрытия окна");

            // Проверяем конфигурацию через ViewModel
            if (_viewModel.CurrentConfig?.System?.AllowCompleteExit != true)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                Log.Information("Окно скрыто в трей");
            }
            else
            {
                // Всегда вызываем Cleanup при реальном закрытии
                Log.Information("Завершение работы ChatCaster");
                _viewModel.Cleanup();
            }
        }

        public void NavigateToSettings()
        {
            _viewModel.NavigateToSettings();
        }

        // Обработчики событий UI (только анимации и UI-специфичные вещи)
        private async void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not Wpf.Ui.Controls.Button { Tag: string pageTag }) return;

                // Выполняем навигацию С анимацией
                await NavigateToPageWithAnimation(pageTag);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при навигации на страницу");
            }
        }
        
        private async Task NavigateToPageWithAnimation(string pageTag)
        {
            Log.Debug("Навигация на страницу: {PageTag}", pageTag);

            // Fade out текущей страницы
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            ContentFrame.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            await Task.Delay(150);

            // обновляет CurrentPage и кнопки
            _viewModel.NavigateToPageCommand.Execute(pageTag);

            // И навигацию в Frame
            if (_viewModel.CurrentPage != null)
            {
                ContentFrame.Navigate(_viewModel.CurrentPage);
            }

            // Fade in новой страницы
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            ContentFrame.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void ToggleMenu_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleMenuCommand.Execute(null);

            // Анимация (UI-специфичная логика остается в code-behind)
            var animation = new DoubleAnimation
            {
                To = _viewModel.IsSidebarVisible ? 280 : 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            SidebarBorder.BeginAnimation(Border.WidthProperty, animation);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.MinimizeWindowCommand.Execute(null);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            try
            {
                base.OnKeyDown(e);

                switch (e.Key)
                {
                    case WpfKey.F1:
                        await NavigateToPageWithAnimation("Main");
                        break;
                    case WpfKey.F2:
                        await NavigateToPageWithAnimation("Audio");
                        break;
                    case WpfKey.F3:
                        await NavigateToPageWithAnimation("Interface");
                        break;
                    case WpfKey.F4:
                        await NavigateToPageWithAnimation("Control");
                        break;
                    case WpfKey.Tab when Keyboard.Modifiers == WpfModifierKeys.Control:
                        _viewModel.ToggleMenuCommand.Execute(null);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке клавиши: {Key}", e.Key);
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            Log.Information("Окно ChatCaster закрыто");

            // Гарантированная очистка при любом закрытии окна
            _viewModel.Cleanup();

            // Закрываем логгер
            Log.CloseAndFlush();

            base.OnClosed(e);
        }
    }
}
