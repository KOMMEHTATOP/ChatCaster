using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatCaster.Windows.Services;
using ChatCaster.Core.Models;
using ChatCaster.Windows.ViewModels.Base;
using ChatCaster.Windows.ViewModels.Navigation;
using Serilog;

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
                Log.Information("Инициализация ChatCasterWindowViewModel начата");

                Log.Debug("Загружаем конфигурацию...");
                CurrentConfig = await _serviceContext.ConfigurationService!.LoadConfigAsync();
                _serviceContext.Config = CurrentConfig;
                _trayService.SetConfig(CurrentConfig);
                Log.Information("Конфигурация загружена");

                Log.Debug("Инициализируем сервис распознавания речи...");
                await _speechService.InitializeAsync(CurrentConfig.Whisper);
                Log.Information("Сервис распознавания речи инициализирован");

                Log.Debug("Применяем конфигурацию к OverlayService...");
                await _overlayService.ApplyConfigAsync(CurrentConfig.Overlay);
                Log.Information("Конфигурация OverlayService применена: Position={Position}, Opacity={Opacity}", 
                    CurrentConfig.Overlay.Position, CurrentConfig.Overlay.Opacity);

                if (CurrentConfig.Input.KeyboardShortcut != null)
                {
                    Log.Debug("Регистрируем хоткей: {Key} + {Modifiers}", 
                        CurrentConfig.Input.KeyboardShortcut.Key, CurrentConfig.Input.KeyboardShortcut.Modifiers);
                    
                    var registered = await _systemService.RegisterGlobalHotkeyAsync(CurrentConfig.Input.KeyboardShortcut);
                    Log.Information("Хоткей зарегистрирован: {Registered}", registered);
                }

                // Инициализируем геймпад координатор
                Log.Debug("Проверяем GamepadVoiceCoordinator...");

                if (_serviceContext?.GamepadVoiceCoordinator != null)
                {
                    Log.Debug("GamepadVoiceCoordinator найден, начинаем инициализацию...");
                    var gamepadInitialized = await _serviceContext.GamepadVoiceCoordinator.InitializeAsync();
                    Log.Information("Геймпад инициализирован: {Initialized}", gamepadInitialized);
                }
                else
                {
                    Log.Warning("GamepadVoiceCoordinator НЕ НАЙДЕН в ServiceContext");
                }

                if (CurrentConfig.System.StartMinimized)
                {
                    Log.Debug("Запуск в свернутом виде");
                    WindowState = WindowState.Minimized;
                }

                StatusText = NavigationConstants.StatusReady;
                Log.Information("Инициализация ChatCasterWindowViewModel завершена");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка инициализации ChatCasterWindowViewModel");
            }
        }
        
        public void NavigateToSettings()
        {
            _navigationManager.NavigateToSettings();
        }

        public void Cleanup()
        {
            Log.Information("Cleanup ChatCasterWindowViewModel начат");

            _systemService.GlobalHotkeyPressed -= OnGlobalHotkeyPressed;
            _navigationManager.NavigationChanged -= OnNavigationChanged;

            if (_serviceContext.GamepadVoiceCoordinator != null)
            {
                Task.Run(async () => await _serviceContext.GamepadVoiceCoordinator.ShutdownAsync());
            }

            // Очищаем все страницы через NavigationManager
            Log.Debug("Очищаем все кешированные страницы...");
            _navigationManager.CleanupAllPages();

            // Теперь можем вызывать Dispose напрямую
            _gamepadService?.Dispose();
            _systemService?.Dispose();
            _overlayService?.Dispose();
            _audioService?.Dispose();
            _speechService?.Dispose();
            _trayService?.Dispose();

            Log.Information("Cleanup ChatCasterWindowViewModel завершен");
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
                Log.Debug("Глобальный хоткей нажат: {Shortcut}", FormatKeyboardShortcut(shortcut));
                await HandleVoiceRecordingAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки хоткея");
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
                    Log.Error("VoiceRecordingService не инициализирован");
                    _trayService.ShowNotification("Ошибка", "Сервис записи не готов");
                    return;
                }

                if (voiceService.IsRecording)
                {
                    Log.Debug("Останавливаем запись через VoiceRecordingService...");
                    StatusText = NavigationConstants.StatusProcessing;

                    // Просто останавливаем запись - VoiceRecordingService уведомит всех подписчиков
                    await voiceService.StopRecordingAsync();
                }
                else
                {
                    Log.Debug("Начинаем запись через VoiceRecordingService...");
                    
                    // Просто начинаем запись - VoiceRecordingService уведомит всех подписчиков
                    await voiceService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка в HandleVoiceRecordingAsync");
                _trayService.ShowNotification("Ошибка", "Произошла ошибка при записи");
                StatusText = NavigationConstants.StatusReady;
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