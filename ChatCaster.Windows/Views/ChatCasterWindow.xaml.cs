using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.ComponentModel;
using Wpf.Ui.Controls;
using ChatCaster.Windows.Views.ViewSettings;
using ChatCaster.Windows.Services;
using ChatCaster.Core.Models;
using WpfKey = System.Windows.Input.Key;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;

namespace ChatCaster.Windows.Views
{
    public partial class ChatCasterWindow : FluentWindow
    {
        private readonly AudioCaptureService _audioService;
        private readonly SpeechRecognitionService _speechService;
        private readonly GamepadService _gamepadService;
        private readonly SystemIntegrationService _systemService;
        private readonly OverlayService _overlayService;
        private readonly ConfigurationService _configService;
        private readonly ServiceContext _serviceContext;
        private readonly TrayService _trayService;

        // Добавить поля для кеширования страниц
        private MainPageView? _cachedMainPage;
        private AudioSettingsView? _cachedAudioPage;
        private InterfaceSettingsView? _cachedInterfacePage;
        private ControlSettingsView? _cachedControlPage;

        // Конфигурация приложения
        private AppConfig _currentConfig;

        private bool _isSidebarVisible = true;

        public ChatCasterWindow()
        {
            InitializeComponent();

            // Настройка кодировки консоли для правильного отображения русских символов
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;

            // Создаем сервисы точно так же как в MainWindow
            _audioService = new AudioCaptureService();
            _speechService = new SpeechRecognitionService();
            _gamepadService = new GamepadService();
            _systemService = new SystemIntegrationService();
            _overlayService = new OverlayService();
            _configService = new ConfigurationService();

            // Создаем дефолтную конфигурацию (потом загрузим из файла)
            _currentConfig = new AppConfig();

            // Создаем ServiceContext для передачи в страницы настроек
            _serviceContext = new ServiceContext(_currentConfig)
            {
                GamepadService = _gamepadService,
                AudioService = _audioService,
                SpeechService = _speechService,
                SystemService = _systemService,
                OverlayService = _overlayService
            };

            // Создаем TrayService
            _trayService = new TrayService(this);
            _trayService.Initialize();

            // Подписываемся на событие закрытия
            Closing += ChatCasterWindow_Closing;

            LoadMainPage();

            // Загружаем конфигурацию асинхронно после создания UI
            Loaded += ChatCasterWindow_Loaded;
        }

        private async void ChatCasterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine($"🔥 [ChatCasterWindow] ChatCasterWindow_Loaded начат");

                // Загружаем конфигурацию
                _currentConfig = await _configService.LoadConfigAsync();
                _serviceContext.Config = _currentConfig;
                Console.WriteLine($"📝 [ChatCasterWindow] Конфигурация загружена");

                // Инициализируем Whisper с настройками из конфигурации
                Console.WriteLine("Инициализация Whisper...");
                await _speechService.InitializeAsync(_currentConfig.Whisper);
                Console.WriteLine("Whisper инициализирован");

                // Подписываемся на глобальные хоткеи
                Console.WriteLine($"📝 [ChatCasterWindow] Подписываемся на GlobalHotkeyPressed");
                _systemService.GlobalHotkeyPressed += OnGlobalHotkeyPressed;
                Console.WriteLine($"📝 [ChatCasterWindow] Подписка выполнена");

                // ИСПРАВЛЕНИЕ: Регистрируем хоткей ТОЛЬКО один раз при запуске приложения
                if (_currentConfig.Input.KeyboardShortcut != null)
                {
                    Console.WriteLine(
                        $"📝 [ChatCasterWindow] Найден сохраненный хоткей: {FormatKeyboardShortcut(_currentConfig.Input.KeyboardShortcut)}");
                    Console.WriteLine($"📝 [ChatCasterWindow] Регистрируем ЕДИНСТВЕННЫЙ раз при запуске приложения");
                    bool registered = await _systemService.RegisterGlobalHotkeyAsync(_currentConfig.Input.KeyboardShortcut);
                    Console.WriteLine($"📝 [ChatCasterWindow] Результат регистрации: {registered}");
                }
                else
                {
                    Console.WriteLine($"📝 [ChatCasterWindow] Сохраненный хоткей не найден");
                }

                // Сворачиваем в трей при запуске если включена настройка
                if (_currentConfig.System.StartMinimized)
                {
                    this.WindowState = WindowState.Minimized;
                    this.ShowInTaskbar = false;
                    this.Hide();
                }

                _trayService.UpdateStatus("Готов к записи");
                Console.WriteLine($"🔥 [ChatCasterWindow] ChatCasterWindow_Loaded завершен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ChatCasterWindow] Ошибка в ChatCasterWindow_Loaded: {ex.Message}");
            }
        }
        public void NavigateToSettings()
        {
            try
            {
                NavigateToPageAsync("Interface");
                UpdateNavigationButtons("Interface");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка навигации к настройкам: {ex.Message}");
            }
        }
        private async void OnGlobalHotkeyPressed(object? sender, KeyboardShortcut shortcut)
        {
            try
            {
                Console.WriteLine($"🎯 [ChatCasterWindow] OnGlobalHotkeyPressed вызван!");
                Console.WriteLine($"📝 [ChatCasterWindow] Sender: {sender?.GetType().Name}");
                Console.WriteLine($"📝 [ChatCasterWindow] Shortcut: {FormatKeyboardShortcut(shortcut)}");

                // Находим MainPageView и запускаем запись
                if (ContentFrame.Content is MainPageView mainPage)
                {
                    Console.WriteLine($"📝 [ChatCasterWindow] MainPageView найден, запускаем запись");
                    await mainPage.TriggerRecordingFromHotkey();
                }
                else
                {
                    Console.WriteLine(
                        $"📝 [ChatCasterWindow] MainPageView НЕ найден, текущий контент: {ContentFrame.Content?.GetType().Name}");
                    Console.WriteLine($"📝 [ChatCasterWindow] Переходим на главную страницу");
                    await NavigateToPageAsync("Main");

                    if (ContentFrame.Content is MainPageView newMainPage)
                    {
                        Console.WriteLine($"📝 [ChatCasterWindow] Новый MainPageView создан, запускаем запись");
                        await newMainPage.TriggerRecordingFromHotkey();
                    }
                    else
                    {
                        Console.WriteLine($"❌ [ChatCasterWindow] Не удалось создать MainPageView");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ChatCasterWindow] Ошибка в OnGlobalHotkeyPressed: {ex.Message}");
            }
        }

// НОВОЕ: Добавить вспомогательный метод для форматирования
        private string FormatKeyboardShortcut(KeyboardShortcut shortcut)
        {
            var parts = new List<string>();

            if (shortcut.Modifiers.HasFlag(Core.Models.ModifierKeys.Control))
                parts.Add("Ctrl");
            if (shortcut.Modifiers.HasFlag(Core.Models.ModifierKeys.Shift))
                parts.Add("Shift");
            if (shortcut.Modifiers.HasFlag(Core.Models.ModifierKeys.Alt))
                parts.Add("Alt");
            if (shortcut.Modifiers.HasFlag(Core.Models.ModifierKeys.Windows))
                parts.Add("Win");

            parts.Add(shortcut.Key.ToString());

            return string.Join(" + ", parts);
        }

        private void ChatCasterWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_currentConfig?.System?.AllowCompleteExit != true)
            {
                // Сворачиваем в трей
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
                _trayService.ShowFirstTimeNotification(_currentConfig);
            }
            else
            {
                // Полное закрытие
                _trayService.Dispose();
            }
        }

        private void LoadMainPage()
        {
            _cachedMainPage ??= new MainPageView(_audioService, _speechService, _serviceContext, _overlayService);
            ContentFrame.Navigate(_cachedMainPage);
            UpdateNavigationButtons("Main");
        }

        private async void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button button && button.Tag is string pageTag)
            {
                await NavigateToPageAsync(pageTag);
                UpdateNavigationButtons(pageTag);
            }
        }

        private async Task NavigateToPageAsync(string pageTag)
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

            // ИСПРАВЛЕНИЕ: Используем кешированные страницы
            Page targetPage = pageTag switch
            {
                "Main" => _cachedMainPage ??= new MainPageView(_audioService, _speechService, _serviceContext, _overlayService),
                "Audio" => _cachedAudioPage ??= new AudioSettingsView(_audioService, _speechService, _configService, _serviceContext),
                "Interface" => _cachedInterfacePage ??= new InterfaceSettingsView(_overlayService, _configService, _serviceContext),
                "Control" => _cachedControlPage ??= new ControlSettingsView(_gamepadService, _systemService, _configService, _serviceContext),
                _ => _cachedMainPage ??= new MainPageView(_audioService, _speechService, _serviceContext, _overlayService)
            };

            ContentFrame.Navigate(targetPage);

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

        
        private void NavigateToPage(string pageTag)
        {
            Page targetPage = pageTag switch
            {
                "Main" => new MainPageView(_audioService, _speechService, _serviceContext, _overlayService),
                "Audio" => new AudioSettingsView(_audioService, _speechService, _configService, _serviceContext),
                "Interface" => new InterfaceSettingsView(_overlayService, _configService, _serviceContext),
                "Control" => new ControlSettingsView(_gamepadService, _systemService, _configService, _serviceContext),
                _ => new MainPageView(_audioService, _speechService, _serviceContext, _overlayService)
            };

            ContentFrame.Navigate(targetPage);
        }

        private void UpdateNavigationButtons(string activePageTag)
        {
            // Сброс всех кнопок
            MainPageButton.Background = Brushes.Transparent;
            AudioPageButton.Background = Brushes.Transparent;
            InterfacePageButton.Background = Brushes.Transparent;
            ControlPageButton.Background = Brushes.Transparent;

            // Установка активной кнопки
            var activeBrush = new SolidColorBrush(Color.FromRgb(0x0e, 0x63, 0x9c));

            switch (activePageTag)
            {
                case "Main":
                    MainPageButton.Background = activeBrush;
                    break;
                case "Audio":
                    AudioPageButton.Background = activeBrush;
                    break;
                case "Interface":
                    InterfacePageButton.Background = activeBrush;
                    break;
                case "Control":
                    ControlPageButton.Background = activeBrush;
                    break;
            }
        }

        private void ToggleMenu_Click(object sender, RoutedEventArgs e)
        {
            _isSidebarVisible = !_isSidebarVisible;

            var animation = new DoubleAnimation
            {
                To = _isSidebarVisible ? 280 : 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            SidebarBorder.BeginAnimation(Border.WidthProperty, animation);
        }

        public class GridLengthAnimation : AnimationTimeline
        {
            public GridLength From { get; set; }
            public GridLength To { get; set; }

            public override Type TargetPropertyType => typeof(GridLength);

            protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

            public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue,
                AnimationClock animationClock)
            {
                if (animationClock.CurrentProgress == null) return From;

                double progress = animationClock.CurrentProgress.Value;
                double fromValue = From.Value;
                double toValue = To.Value;

                double currentValue = fromValue + (toValue - fromValue) * progress;
                return new GridLength(currentValue);
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        //область перетаскивания
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // Геймпад навигация (опционально)
        protected override async void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            switch (e.Key)
            {
                case WpfKey.F1:
                    await NavigateToPageAsync("Main");
                    UpdateNavigationButtons("Main");
                    break;
                case WpfKey.F2:
                    await NavigateToPageAsync("Audio");
                    UpdateNavigationButtons("Audio");
                    break;
                case WpfKey.F3:
                    await NavigateToPageAsync("Interface");
                    UpdateNavigationButtons("Interface");
                    break;
                case WpfKey.F4:
                    await NavigateToPageAsync("Control");
                    UpdateNavigationButtons("Control");
                    break;
                case WpfKey.Tab when Keyboard.Modifiers == WpfModifierKeys.Control:
                    ToggleMenu_Click(this, new RoutedEventArgs());
                    break;
            }
        }

        // Cleanup при закрытии окна
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // НОВОЕ: Отписываемся от событий
                if (_systemService != null)
                {
                    _systemService.GlobalHotkeyPressed -= OnGlobalHotkeyPressed;
                }

                // Останавливаем сервисы при закрытии окна
                _gamepadService?.Dispose();
                _systemService?.Dispose();
                _overlayService?.Dispose();
                _audioService?.Dispose();
                _speechService?.Dispose();

                // Dispose TrayService
                _trayService?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при закрытии сервисов: {ex.Message}");
            }

            base.OnClosed(e);
        }
    }
}
