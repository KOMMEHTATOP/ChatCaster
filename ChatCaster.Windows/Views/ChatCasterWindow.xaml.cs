using System.ComponentModel;
using System.Windows;
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
        private readonly IConfigurationService _configurationService;
        
        // Флаг для принудительного закрытия из трея
        private bool _isForceExitFromTray = false;

        public ChatCasterWindow(ChatCasterWindowViewModel viewModel, ITrayService trayService,
            IConfigurationService configurationService)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

            // Установка DataContext
            DataContext = _viewModel;

            // Подписка на события окна
            Closing += ChatCasterWindow_Closing;
            Loaded += ChatCasterWindow_Loaded;
            
            // Подписка на события трея
            SubscribeToTrayEvents();

            Log.Information("ChatCaster окно создано через DI с ITrayService");
        }

        #region Tray Events Subscription

        private void SubscribeToTrayEvents()
        {
            try
            {
                _trayService.ShowMainWindowRequested += OnShowMainWindowRequested;
                _trayService.ShowSettingsRequested += OnShowSettingsRequested;
                _trayService.ExitApplicationRequested += OnExitApplicationRequested;
                
                Log.Debug("Подписка на события ITrayService выполнена");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при подписке на события трея");
            }
        }

        private void UnsubscribeFromTrayEvents()
        {
            try
            {
                _trayService.ShowMainWindowRequested -= OnShowMainWindowRequested;
                _trayService.ShowSettingsRequested -= OnShowSettingsRequested;
                _trayService.ExitApplicationRequested -= OnExitApplicationRequested;
                
                Log.Debug("Отписка от событий ITrayService выполнена");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка при отписке от событий трея");
            }
        }

        #endregion

        #region Tray Event Handlers

        private void OnShowMainWindowRequested(object? sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    Show();
                    ShowInTaskbar = true;
                    WindowState = WindowState.Normal;
                    Activate();
                    Focus();
                });
                
                Log.Debug("Главное окно показано по запросу из трея");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при показе главного окна из трея");
            }
        }

        private void OnShowSettingsRequested(object? sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Сначала показываем окно
                    Show();
                    ShowInTaskbar = true;
                    WindowState = WindowState.Normal;
                    Activate();
                    
                    // Потом переходим к настройкам
                    NavigateToSettings();
                });
                
                Log.Debug("Окно настроек показано по запросу из трея");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при показе настроек из трея");
            }
        }

        private void OnExitApplicationRequested(object? sender, EventArgs e)
        {
            try
            {
                Log.Information("Получен запрос на принудительный выход из системного трея");
                
                // Устанавливаем флаг принудительного закрытия
                _isForceExitFromTray = true;
                
                // Запускаем процесс закрытия окна
                Dispatcher.Invoke(Close);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обработке запроса выхода из трея");
                
                // Fallback: принудительное завершение при критической ошибке
                _ = Task.Run(() => Environment.Exit(1));
            }
        }

        #endregion

        #region Window Events

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

            var currentConfig = _configurationService.CurrentConfig;

            Log.Debug("[CLOSE] ConfigService HashCode: {HashCode}, AllowCompleteExit: {AllowCompleteExit}, ForceExitFromTray: {ForceExitFromTray}",
                currentConfig?.GetHashCode(), currentConfig?.System?.AllowCompleteExit, _isForceExitFromTray);

            // Если это принудительное закрытие из трея - всегда закрываем полностью
            if (_isForceExitFromTray)
            {
                Log.Information("Принудительное полное завершение работы ChatCaster (запрос из системного трея)");
                PerformCompleteExit();
                return;
            }

            // Обычная логика закрытия через кнопку X
            if (currentConfig?.System?.AllowCompleteExit != true)
            {
                // AllowCompleteExit = false --> сворачиваем в трей
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;

                _trayService.ShowFirstTimeNotification();
                Log.Information("Окно скрыто в трей (настройка сворачивания включена)");
            }
            else
            {
                // AllowCompleteExit = true --> полное закрытие
                Log.Information("Полное завершение работы ChatCaster (настройка полного закрытия включена)");
                PerformCompleteExit();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Log.Information("Окно ChatCaster закрыто");

            // Отписка от событий трея
            UnsubscribeFromTrayEvents();

            // Гарантированная очистка при любом закрытии окна
            _viewModel.Cleanup();

            base.OnClosed(e);
        }

        #endregion

        #region Exit Logic

        private void PerformCompleteExit()
        {
            try
            {
                Log.Information("Начало процедуры полного закрытия приложения");

                // Отписка от событий трея
                UnsubscribeFromTrayEvents();

                // Запускаем cleanup асинхронно БЕЗ ожидания
                _ = Task.Run(() => _viewModel.Cleanup());

                // Освобождаем TrayService синхронно (быстрая операция)
                _trayService.Dispose();
                Log.Debug("TrayService освобожден при закрытии");

                // Принудительное завершение через 200ms для гарантии
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

                Log.Information("Процедура полного закрытия настроена успешно");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при полном закрытии приложения");

                // Fallback: немедленное завершение при критической ошибке
                _ = Task.Run(() => Environment.Exit(1));
            }
        }

        #endregion

        #region UI Event Handlers

        public void NavigateToSettings()
        {
            _viewModel.NavigateToSettings();
        }

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

            ContentFrame.BeginAnimation(OpacityProperty, fadeIn);
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

            SidebarBorder.BeginAnimation(WidthProperty, animation);
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

        #endregion
    }
}