using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using ChatCaster.Core.Services;
using ChatCaster.Windows.ViewModels;
using System.Windows.Threading;
using WpfKey = System.Windows.Input.Key;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;
using Serilog;

namespace ChatCaster.Windows.Views
{
    public partial class ChatCasterWindow
    {
        private readonly ChatCasterWindowViewModel _viewModel;
        private readonly ITrayService _trayService;

        public ChatCasterWindow(ChatCasterWindowViewModel viewModel, ITrayService trayService)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));

            // Установка DataContext
            DataContext = _viewModel;

            // Подписка на события
            Closing += ChatCasterWindow_Closing;
            Loaded += ChatCasterWindow_Loaded;

            Log.Information("ChatCaster окно создано через DI с ITrayService");
        }

        private async void ChatCasterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Debug("Загрузка главного окна");

                if (_viewModel.CurrentPage != null)
                {
                    ContentFrame.Navigate(_viewModel.CurrentPage);
                }

                await _viewModel.InitializeAsync();

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

            if (_viewModel.CurrentConfig?.System?.AllowCompleteExit != true)
            {
                // AllowCompleteExit = false --> MinimizeToTray = true --> сворачиваем в трей
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
        
                _trayService.ShowFirstTimeNotification();
                Log.Information("Окно скрыто в трей (настройка сворачивания включена)");
            }
            else
            {
                // AllowCompleteExit = true --> MinimizeToTray = false --> полное закрытие
                Log.Information("Полное завершение работы ChatCaster (настройка полного закрытия включена)");
        
                try
                {
                    // ✅ Запускаем cleanup асинхронно БЕЗ ожидания
                    _ = Task.Run(() => _viewModel.Cleanup());
            
                    // ✅ Освобождаем TrayService синхронно (быстрая операция)
                    _trayService.Dispose();
                    Log.Debug("TrayService освобожден при закрытии");
            
                    // ✅ НОВОЕ: Принудительное завершение через 200ms для гарантии
                    _ = Task.Delay(200).ContinueWith(_ =>
                    {
                        try
                        {
                            Log.Information("Принудительное завершение процесса (Whisper cleanup timeout)");
                            Environment.Exit(0);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Ошибка принудительного завершения");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка при очистке ресурсов");
            
                    // ✅ Fallback: немедленное завершение при критической ошибке
                    _ = Task.Run(() => Environment.Exit(1));
                }
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

            ContentFrame.BeginAnimation(OpacityProperty, fadeOut);
            await Task.Delay(150);

            // обновляет CurrentPage и кнопки
            _viewModel.NavigateToPageCommand.Execute(pageTag);

            if (_viewModel.CurrentPage != null)
            {
                ContentFrame.Navigate(_viewModel.CurrentPage);
            }

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

            // TrayService освобождается в Program.cs, не здесь
            // Это важно, так как он теперь управляется DI контейнером

            base.OnClosed(e);
        }
    }
}