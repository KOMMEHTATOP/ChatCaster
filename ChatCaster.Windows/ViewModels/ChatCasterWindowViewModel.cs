using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatCaster.Windows.Services;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.Windows.ViewModels.Base;
using ChatCaster.Windows.ViewModels.Navigation;
using ChatCaster.Windows.Services.GamepadService;
using Serilog;

namespace ChatCaster.Windows.ViewModels
{
    public partial class ChatCasterWindowViewModel : ViewModelBase
    {
        #region Services

        private readonly IAudioCaptureService _audioService;
        private readonly ISpeechRecognitionService _speechService;
        private readonly IGamepadService _gamepadService;
        private readonly IOverlayService _overlayService;
        private readonly ISystemIntegrationService _systemService;
        private readonly IConfigurationService _configurationService;
        private readonly IVoiceRecordingService _voiceRecordingService;
        private readonly GamepadVoiceCoordinator _gamepadVoiceCoordinator;
        private readonly NavigationManager _navigationManager;
        private readonly ITrayService _trayService;

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
            IAudioCaptureService audioService,
            ISpeechRecognitionService speechService,
            IGamepadService gamepadService,
            ISystemIntegrationService systemService,
            IOverlayService overlayService,
            IConfigurationService configurationService,
            IVoiceRecordingService voiceRecordingService,
            GamepadVoiceCoordinator gamepadVoiceCoordinator,
            AppConfig currentConfig,
            ITrayService trayService,
            TrayNotificationCoordinator trayCoordinator) 
        {
            _audioService = audioService;
            _speechService = speechService;
            _gamepadService = gamepadService;
            _overlayService = overlayService;
            _systemService = systemService;
            _configurationService = configurationService;
            _voiceRecordingService = voiceRecordingService;
            _gamepadVoiceCoordinator = gamepadVoiceCoordinator;
            _currentConfig = currentConfig;
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));

            // Создаем NavigationManager с TrayNotificationCoordinator
            _navigationManager = new NavigationManager(
                audioService, speechService, gamepadService, systemService,
                overlayService, configurationService, currentConfig, 
                voiceRecordingService, gamepadVoiceCoordinator, trayCoordinator); 

            // Подписываемся на события навигации
            _navigationManager.NavigationChanged += OnNavigationChanged;

            // Устанавливаем начальную страницу
            CurrentPage = _navigationManager.CurrentPage;
            CurrentPageTag = _navigationManager.CurrentPageTag;

            // Подписка на события
            _systemService.GlobalHotkeyPressed += OnGlobalHotkeyPressed;

            Log.Debug("ChatCasterWindowViewModel создан с ITrayService и TrayNotificationCoordinator из DI");
        }

        #endregion

        #region Public Methods

        public async Task InitializeAsync()
        {
            try
            {
                Log.Information("Инициализация ChatCasterWindowViewModel начата");

                Log.Debug("Загружаем конфигурацию...");
                CurrentConfig = await _configurationService.LoadConfigAsync();
                
                if (_trayService is TrayService trayServiceImpl)
                {
                    trayServiceImpl.SetConfig(CurrentConfig);
                }
                
                Log.Information("Конфигурация загружена и передана в TrayService");

                if (string.IsNullOrEmpty(CurrentConfig.Audio.SelectedDeviceId))
                {
                    Log.Information("Новая установка - применяем дефолтные настройки");
                    
                    // Устанавливаем дефолтную модель в EngineSettings
                    CurrentConfig.SpeechRecognition.EngineSettings["ModelSize"] = "tiny";
                    
                    // Сохраняем обновленный конфиг
                    await _configurationService.SaveConfigAsync(CurrentConfig);
                    Log.Information("Дефолтная модель Whisper установлена: tiny");
                }

                // Инициализируем новый Whisper модуль
                Log.Information("Инициализируем Whisper модуль...");
                var speechInitialized = await _speechService.InitializeAsync(CurrentConfig.SpeechRecognition);
                Log.Information("Сервис распознавания речи инициализирован: {Success}", speechInitialized);

                Log.Debug("Применяем аудио настройки...");
                if (!string.IsNullOrEmpty(CurrentConfig.Audio.SelectedDeviceId))
                {
                    await _audioService.SetActiveDeviceAsync(CurrentConfig.Audio.SelectedDeviceId);
                    Log.Information("Активное аудио устройство установлено: {DeviceId}", CurrentConfig.Audio.SelectedDeviceId);
                }
                else
                {
                    Log.Warning("В конфигурации не указано аудио устройство");
                }

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
                Log.Debug("GamepadVoiceCoordinator найден, начинаем инициализацию...");
                var gamepadInitialized = await _gamepadVoiceCoordinator.InitializeAsync();
                Log.Information("Геймпад инициализирован: {Initialized}", gamepadInitialized);

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

        private bool _isCleanedUp;

        public void Cleanup()
        {
            if (_isCleanedUp)
            {
                Log.Debug("Cleanup уже выполнен, пропускаем");
                return;
            }

            Log.Information("Cleanup ChatCasterWindowViewModel начат");
            _isCleanedUp = true;

            try
            {
                // 1. Отписываемся от событий
                _systemService.GlobalHotkeyPressed -= OnGlobalHotkeyPressed;
                _navigationManager.NavigationChanged -= OnNavigationChanged;

                // 2. Очищаем страницы
                _navigationManager.CleanupAllPages();

                // 3. Останавливаем геймпад (обычно быстро)
                try
                {
                    _gamepadVoiceCoordinator.ShutdownAsync().Wait(1000); // Максимум 1 секунда
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Геймпад не остановился быстро, пропускаем");
                }

                // 4. Снимаем хоткеи (обычно быстро)
                try
                {
                    _systemService.UnregisterGlobalHotkeyAsync().Wait(500); // Максимум 0.5 секунды
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Хоткеи не сняты быстро, пропускаем");
                }

                // 5. Быстрые сервисы
                try { if (_audioService is IDisposable da) da.Dispose(); }
                catch
                {
                    // ignored
                }

                try { if (_overlayService is IDisposable do_) do_.Dispose(); }
                catch
                {
                    // ignored
                }
                
                Log.Information("Cleanup ChatCasterWindowViewModel завершен");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка в Cleanup, но продолжаем");
            }
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
                _trayService.ShowNotification("Ошибка", "Произошла ошибка при обработке хоткея", NotificationType.Error);
            }
        }

        #endregion

        #region Voice Recording Logic

        private async Task HandleVoiceRecordingAsync()
        {
            try
            {
                if (_voiceRecordingService.IsRecording)
                {
                    Log.Debug("Останавливаем запись через VoiceRecordingService...");
                    StatusText = NavigationConstants.StatusProcessing;

                    // Просто останавливаем запись - VoiceRecordingService уведомит всех подписчиков
                    await _voiceRecordingService.StopRecordingAsync();
                }
                else
                {
                    Log.Debug("Начинаем запись через VoiceRecordingService...");
                    
                    // Просто начинаем запись - VoiceRecordingService уведомит всех подписчиков
                    await _voiceRecordingService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка в HandleVoiceRecordingAsync");
                _trayService.ShowNotification("Ошибка", "Произошла ошибка при записи", NotificationType.Error);
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