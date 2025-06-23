using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using ChatCaster.Windows.Services;
using ChatCaster.Core.Models;
using ChatCaster.Windows.ViewModels;
using WpfKey = System.Windows.Input.Key;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;

namespace ChatCaster.Windows.Views
{
    public partial class ChatCasterWindow : FluentWindow
    {
        private readonly ChatCasterWindowViewModel _viewModel;

        public ChatCasterWindow()
        {
            InitializeComponent();

            // Настройка кодировки консоли
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;

            // Создание сервисов
            var audioService = new AudioCaptureService();
            var speechService = new SpeechRecognitionService();
            var gamepadService = new GamepadService();
            var systemService = new SystemIntegrationService();
            var overlayService = new OverlayService();
            var configService = new ConfigurationService();

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
        }

        private async void ChatCasterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Сначала устанавливаем начальную страницу БЕЗ анимации
            if (_viewModel.CurrentPage != null)
            {
                ContentFrame.Navigate(_viewModel.CurrentPage);
            }
            
            // Затем инициализируем ViewModel
            await _viewModel.InitializeAsync();
        }

        private void ChatCasterWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Проверяем конфигурацию через ViewModel
            if (_viewModel.CurrentConfig?.System?.AllowCompleteExit != true)
            {
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
            }
            else
            {
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
            if (sender is not Wpf.Ui.Controls.Button { Tag: string pageTag }) return;

            // Выполняем навигацию С анимацией
            await NavigateToPageWithAnimation(pageTag);
        }

        private async Task NavigateToPageWithAnimation(string pageTag)
        {
            // Fade out текущей страницы
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            ContentFrame.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            await Task.Delay(150);

            // ТЕПЕРЬ выполняем команду ViewModel (обновляет CurrentPage и кнопки)
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
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
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
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
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
                this.DragMove();
            }
        }

        protected override async void OnKeyDown(KeyEventArgs e)
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

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.Cleanup();
            base.OnClosed(e);
        }
    }
}