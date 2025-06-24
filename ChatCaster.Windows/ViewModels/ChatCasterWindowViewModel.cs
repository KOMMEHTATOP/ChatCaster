using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatCaster.Windows.Services;
using ChatCaster.Core.Models;
using ChatCaster.Windows.ViewModels.Base;
using ChatCaster.Windows.ViewModels.Navigation;

namespace ChatCaster.Windows.ViewModels
{
    public partial class ChatCasterWindowViewModel : ViewModelBase
    {

        #region Services

        private readonly AudioCaptureService _audioService;
        private readonly SpeechRecognitionService _speechService;
        private readonly Services.GamepadService.MainGamepadService _gamepadService;
        private readonly OverlayService _overlayService;
        private readonly SystemIntegrationService _systemService;
        private readonly ServiceContext _serviceContext;
        private readonly TrayService _trayService;
        private readonly NavigationManager _navigationManager;

        #endregion

        #region Observable Properties

        [ObservableProperty]
        private AppConfig _currentConfig;

        [ObservableProperty]
        private bool _isSidebarVisible = true;

        [ObservableProperty]
        private Page? _currentPage;

        [ObservableProperty]
        private WindowState _windowState = WindowState.Normal;

        [ObservableProperty]
        private string _statusText = NavigationConstants.StatusReady;

        [ObservableProperty]
        private string _currentPageTag = NavigationConstants.MainPage;

        #endregion

        #region Computed Properties for Navigation Buttons

        public SolidColorBrush MainButtonBackground
        {
            get => CurrentPageTag == NavigationConstants.MainPage
                ? NavigationConstants.ActiveButtonBrush
                : NavigationConstants.InactiveButtonBrush;
        }

        public SolidColorBrush AudioButtonBackground
        {
            get => CurrentPageTag == NavigationConstants.AudioPage
                ? NavigationConstants.ActiveButtonBrush
                : NavigationConstants.InactiveButtonBrush;
        }

        public SolidColorBrush InterfaceButtonBackground
        {
            get => CurrentPageTag == NavigationConstants.InterfacePage
                ? NavigationConstants.ActiveButtonBrush
                : NavigationConstants.InactiveButtonBrush;
        }

        public SolidColorBrush ControlButtonBackground
        {
            get => CurrentPageTag == NavigationConstants.ControlPage
                ? NavigationConstants.ActiveButtonBrush
                : NavigationConstants.InactiveButtonBrush;
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void NavigateToPage(string pageTag)
        {
            _navigationManager.NavigateToPage(pageTag);
        }

        [RelayCommand]
        private void ToggleMenu()
        {
            IsSidebarVisible = !IsSidebarVisible;
        }

        [RelayCommand]
        private void MinimizeWindow()
        {
            WindowState = WindowState.Minimized;
        }

        [RelayCommand]
        private async Task HandleVoiceRecording()
        {
            await HandleVoiceRecordingAsync();
        }

        #endregion

        #region Constructor

        public ChatCasterWindowViewModel(
            AudioCaptureService audioService,
            SpeechRecognitionService speechService,
            Services.GamepadService.MainGamepadService gamepadService,
            SystemIntegrationService systemService,
            OverlayService overlayService,
            ConfigurationService configService,
            ServiceContext serviceContext,
            TrayService trayService)
        {
            _audioService = audioService;
            _speechService = speechService;
            _gamepadService = gamepadService;
            _overlayService = overlayService;
            _systemService = systemService;
            _serviceContext = serviceContext;
            _trayService = trayService;
            _currentConfig = new AppConfig();

            // Создаем NavigationManager
            _navigationManager = new NavigationManager(
                audioService, speechService, gamepadService, systemService,
                overlayService, configService, serviceContext);

            // Подписываемся на события навигации
            _navigationManager.NavigationChanged += OnNavigationChanged;

            // Устанавливаем начальную страницу
            CurrentPage = _navigationManager.CurrentPage;
            CurrentPageTag = _navigationManager.CurrentPageTag;

            // Подписка на события
            _systemService.GlobalHotkeyPressed += OnGlobalHotkeyPressed;
        }

        #endregion

        #region Public Methods

        public async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine("🔥 [ViewModel] Инициализация начата");

                Console.WriteLine("🔧 [ViewModel] Загружаем конфигурацию...");
                CurrentConfig = await _serviceContext.ConfigurationService!.LoadConfigAsync();
                _serviceContext.Config = CurrentConfig;
                _trayService.SetConfig(CurrentConfig);
                Console.WriteLine("✅ [ViewModel] Конфигурация загружена");

                Console.WriteLine("🎤 [ViewModel] Инициализируем сервис распознавания речи...");
                await _speechService.InitializeAsync(CurrentConfig.Whisper);
                Console.WriteLine("✅ [ViewModel] Сервис распознавания речи инициализирован");

                if (CurrentConfig.Input.KeyboardShortcut != null)
                {
                    Console.WriteLine(
                        $"⌨️ [ViewModel] Регистрируем хоткей: {CurrentConfig.Input.KeyboardShortcut.Key} + {CurrentConfig.Input.KeyboardShortcut.Modifiers}");
                    bool registered = await _systemService.RegisterGlobalHotkeyAsync(CurrentConfig.Input.KeyboardShortcut);
                    Console.WriteLine($"📝 [ViewModel] Хоткей зарегистрирован: {registered}");
                }

                // Инициализируем геймпад координатор
                Console.WriteLine("🎮 [ViewModel] Проверяем GamepadVoiceCoordinator...");

                if (_serviceContext?.GamepadVoiceCoordinator != null)
                {
                    Console.WriteLine("🎮 [ViewModel] GamepadVoiceCoordinator найден, начинаем инициализацию...");
                    bool gamepadInitialized = await _serviceContext.GamepadVoiceCoordinator.InitializeAsync();
                    Console.WriteLine($"🎮 [ViewModel] Геймпад инициализирован: {gamepadInitialized}");
                }
                else
                {
                    Console.WriteLine("❌ [ViewModel] GamepadVoiceCoordinator НЕ НАЙДЕН в ServiceContext!");
                }

                if (CurrentConfig.System.StartMinimized)
                {
                    Console.WriteLine("🔽 [ViewModel] Запуск в свернутом виде");
                    WindowState = WindowState.Minimized;
                }

                StatusText = NavigationConstants.StatusReady;
                Console.WriteLine("🔥 [ViewModel] Инициализация завершена");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ViewModel] Ошибка инициализации: {ex.Message}");
                Console.WriteLine($"❌ [ViewModel] StackTrace: {ex.StackTrace}");
            }
        }
        
        public void NavigateToSettings()
        {
            _navigationManager.NavigateToSettings();
        }

        public void Cleanup()
        {
            Console.WriteLine("🔥 [ViewModel] Cleanup начат");

            _systemService.GlobalHotkeyPressed -= OnGlobalHotkeyPressed;
            _navigationManager.NavigationChanged -= OnNavigationChanged;

            if (_serviceContext.GamepadVoiceCoordinator != null)
            {
                Task.Run(async () => await _serviceContext.GamepadVoiceCoordinator.ShutdownAsync());
            }

            // Теперь можем вызывать Dispose напрямую
            _gamepadService?.Dispose();
            _systemService?.Dispose();
            _overlayService?.Dispose();
            _audioService?.Dispose();
            _speechService?.Dispose();
            _trayService?.Dispose();

            Console.WriteLine("🔥 [ViewModel] Cleanup завершен");
        }

        #endregion

        #region Event Handlers

        private void OnNavigationChanged(object? sender, NavigationChangedEventArgs e)
        {
            CurrentPage = e.Page;
            CurrentPageTag = e.PageTag;

            // Уведомляем об изменении всех button background свойств
            OnPropertyChanged(nameof(MainButtonBackground));
            OnPropertyChanged(nameof(AudioButtonBackground));
            OnPropertyChanged(nameof(InterfaceButtonBackground));
            OnPropertyChanged(nameof(ControlButtonBackground));
        }

        private async void OnGlobalHotkeyPressed(object? sender, KeyboardShortcut shortcut)
        {
            try
            {
                Console.WriteLine($"🎯 Глобальный хоткей: {FormatKeyboardShortcut(shortcut)}");
                await HandleVoiceRecordingAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки хоткея: {ex.Message}");
                _trayService.ShowNotification("Ошибка", "Произошла ошибка при обработке хоткея");
            }
        }

        #endregion

        #region Voice Recording Logic

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
                    StatusText = NavigationConstants.StatusProcessing;

                    var result = await voiceService.StopRecordingAsync();

                    if (result.Success && !string.IsNullOrEmpty(result.RecognizedText))
                    {
                        Console.WriteLine($"✅ Распознано: '{result.RecognizedText}'");
                        _trayService.ShowNotification("Распознано", result.RecognizedText);
                        StatusText = NavigationConstants.StatusReady;

                        await _systemService.SendTextAsync(result.RecognizedText);
                        UpdateMainPageIfVisible(result.RecognizedText, false);
                    }
                    else
                    {
                        Console.WriteLine($"❌ Распознавание не удалось: {result.ErrorMessage}");
                        _trayService.ShowNotification("Ошибка", result.ErrorMessage ?? "Не удалось распознать речь");
                        StatusText = NavigationConstants.StatusReady;
                    }
                }
                else
                {
                    Console.WriteLine("🎤 Начинаем запись...");

                    bool started = await voiceService.StartRecordingAsync();

                    if (started)
                    {
                        Console.WriteLine("✅ Запись началась");
                        StatusText = NavigationConstants.StatusRecording;
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
                StatusText = NavigationConstants.StatusReady;
            }
        }

        private void UpdateMainPageIfVisible(string recognizedText, bool isRecording)
        {
            try
            {
                var mainPage = _navigationManager.GetMainPageIfVisible();

                if (mainPage != null)
                {
                    Console.WriteLine($"📱 Обновляем UI MainPageView");

                    if (isRecording)
                    {
                        mainPage.UpdateRecordingStatus(NavigationConstants.StatusRecording, "#ff9800");
                        mainPage.UpdateRecordingButton("⏹️ Остановить", "RecordCircle24");
                    }
                    else
                    {
                        mainPage.UpdateRecordingStatus(NavigationConstants.StatusReady, "#4caf50");
                        mainPage.UpdateRecordingButton("🎙️ Записать", "Mic24");

                        if (!string.IsNullOrEmpty(recognizedText))
                        {
                            mainPage.ResultText.Text = recognizedText;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обновления UI: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private static string FormatKeyboardShortcut(KeyboardShortcut shortcut)
        {
            var parts = new List<string>();

            if (shortcut.Modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");
            if (shortcut.Modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");
            if (shortcut.Modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");
            if (shortcut.Modifiers.HasFlag(ModifierKeys.Windows))
                parts.Add("Win");

            parts.Add(shortcut.Key.ToString());

            return string.Join(" + ", parts);
        }

        #endregion

    }
}
