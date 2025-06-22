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

            // ДОБАВИТЬ создание VoiceRecordingService:
            var voiceRecordingService = new VoiceRecordingService(
                _audioService,
                _speechService,
                _configService
            );

            _overlayService.SubscribeToVoiceService(voiceRecordingService, _configService);

            // Создаем дефолтную конфигурацию (потом загрузим из файла)
            _currentConfig = new AppConfig();

            // Создаем ServiceContext для передачи в страницы настроек
            _serviceContext = new ServiceContext(_currentConfig)
            {
                GamepadService = _gamepadService,
                AudioService = _audioService,
                SpeechService = _speechService,
                SystemService = _systemService,
                OverlayService = _overlayService,
                ConfigurationService = _configService,
                VoiceRecordingService = voiceRecordingService
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
                _trayService.SetConfig(_currentConfig);
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
                Console.WriteLine($"🎯 Глобальный хоткей: {FormatKeyboardShortcut(shortcut)}");

                // НОВОЕ: Работаем напрямую через VoiceRecordingService
                await HandleVoiceRecordingAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки хоткея: {ex.Message}");
                _trayService.ShowNotification("Ошибка", "Произошла ошибка при обработке хоткея");
            }
        }

        private async Task HandleVoiceRecordingAsync()
        {
            try
            {
                var voiceService = _serviceContext.VoiceRecordingService;

                if (voiceService == null)
                {
                    Console.WriteLine("❌ VoiceRecordingService не инициализирован");
                    _trayService.ShowNotification("Ошибка", "Сервис записи не готов");
                    return;
                }

                if (voiceService.IsRecording)
                {
                    Console.WriteLine("🛑 Останавливаем запись...");
                    _trayService.UpdateStatus("Обработка...");

                    // Останавливаем запись и получаем результат
                    var result = await voiceService.StopRecordingAsync();

                    if (result.Success && !string.IsNullOrEmpty(result.RecognizedText))
                    {
                        Console.WriteLine($"✅ Распознано: '{result.RecognizedText}'");
                        _trayService.ShowNotification("Распознано", result.RecognizedText);
                        _trayService.UpdateStatus("Готов к записи");

                        // Отправляем текст в активное окно
                        await _systemService.SendTextAsync(result.RecognizedText);

                        // Обновляем UI только если MainPageView открыта
                        UpdateMainPageIfVisible(result.RecognizedText, false);
                    }
                    else
                    {
                        Console.WriteLine($"❌ Распознавание не удалось: {result.ErrorMessage}");
                        _trayService.ShowNotification("Ошибка", result.ErrorMessage ?? "Не удалось распознать речь");
                        _trayService.UpdateStatus("Готов к записи");
                    }
                }
                else
                {
                    Console.WriteLine("🎤 Начинаем запись...");

                    // Запускаем запись
                    bool started = await voiceService.StartRecordingAsync();

                    if (started)
                    {
                        Console.WriteLine("✅ Запись началась");
                        _trayService.UpdateStatus("Запись...");

                        // Обновляем UI только если MainPageView открыта
                        UpdateMainPageIfVisible("", true);
                    }
                    else
                    {
                        Console.WriteLine("❌ Не удалось начать запись");
                        _trayService.ShowNotification("Ошибка", "Не удалось начать запись");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка в HandleVoiceRecordingAsync: {ex.Message}");
                _trayService.ShowNotification("Ошибка", "Произошла ошибка при записи");
                _trayService.UpdateStatus("Готов к записи");
            }
        }

        private void UpdateMainPageIfVisible(string recognizedText, bool isRecording)
        {
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    // Проверяем, открыта ли MainPageView
                    if (ContentFrame.Content is MainPageView mainPageView)
                    {
                        Console.WriteLine($"📱 Обновляем UI MainPageView");

                        if (isRecording)
                        {
                            // НАЧАЛИ запись - кнопка "Остановить"
                            mainPageView.UpdateRecordingStatus("Запись...", "#ff9800");
                            mainPageView.UpdateRecordingButton("⏹️ Остановить", "RecordCircle24");
                        }
                        else
                        {
                            // ЗАКОНЧИЛИ запись - кнопка "Записать"
                            mainPageView.UpdateRecordingStatus("Готов к записи", "#4caf50");
                            mainPageView.UpdateRecordingButton("🎙️ Записать", "Mic24");

                            if (!string.IsNullOrEmpty(recognizedText))
                            {
                                mainPageView.ResultText.Text = recognizedText; // И нет метода UpdateRecognizedText
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"📱 MainPageView не открыта, UI не обновляем");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обновления UI: {ex.Message}");
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
            // ✅ ИСПРАВЛЕНИЕ: Логика должна быть ОБРАТНОЙ
            // MinimizeToTrayCheckBox.IsChecked = true -> сворачивать в трей
            // MinimizeToTrayCheckBox.IsChecked = false -> полное закрытие
    
            // Если чекбокс "сворачивать в трей" ВКЛЮЧЕН - НЕ разрешаем полное закрытие
            if (_currentConfig?.System?.AllowCompleteExit != true) // т.е. чекбокс "сворачивать в трей" включен
            {
                // Сворачиваем в трей
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
                _trayService.ShowFirstTimeNotification(_currentConfig);
            }
            else
            {
                // Полное закрытие приложения (чекбокс "сворачивать в трей" выключен)
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
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            ContentFrame.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            await Task.Delay(150);

            // ИСПРАВЛЕНИЕ: Используем кешированные страницы
            Page targetPage = pageTag switch
            {
                "Main" => _cachedMainPage ??=
                    new MainPageView(_audioService, _speechService, _serviceContext, _overlayService),
                "Audio" => _cachedAudioPage ??=
                    new AudioSettingsView(_audioService, _speechService, _configService, _serviceContext),
                "Interface" => _cachedInterfacePage ??=
                    new InterfaceSettingsView(_overlayService, _configService, _serviceContext),
                "Control" => _cachedControlPage ??=
                    new ControlSettingsView(_gamepadService, _systemService, _configService, _serviceContext),
                _ => _cachedMainPage ??= new MainPageView(_audioService, _speechService, _serviceContext, _overlayService)
            };

            ContentFrame.Navigate(targetPage);

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
            Console.WriteLine("🔥 OnClosed начат - завершаем сервисы...");
    
            try
            {
                // НОВОЕ: Отписываемся от событий
                if (_systemService != null)
                {
                    Console.WriteLine("📝 Отписываемся от GlobalHotkeyPressed");
                    _systemService.GlobalHotkeyPressed -= OnGlobalHotkeyPressed;
                }

                // Останавливаем сервисы при закрытии окна
                Console.WriteLine("🎮 Закрываем GamepadService...");
                _gamepadService?.Dispose();
        
                Console.WriteLine("⚙️ Закрываем SystemService...");
                _systemService?.Dispose();
        
                Console.WriteLine("🖥️ Закрываем OverlayService...");
                _overlayService?.Dispose();
        
                Console.WriteLine("🎤 Закрываем AudioService...");
                _audioService?.Dispose();
        
                Console.WriteLine("🗣️ Закрываем SpeechService...");
                _speechService?.Dispose();

                Console.WriteLine("📱 Закрываем TrayService...");
                _trayService?.Dispose();
        
                Console.WriteLine("✅ Все сервисы закрыты");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при закрытии сервисов: {ex.Message}");
            }

            Console.WriteLine("🔚 OnClosed завершен");
            base.OnClosed(e);
        }    }
}
