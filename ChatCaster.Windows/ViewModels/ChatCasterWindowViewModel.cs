using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.System;
using ChatCaster.Core.Services.UI;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels.Base;
using ChatCaster.Windows.ViewModels.Navigation;
using Serilog;

namespace ChatCaster.Windows.ViewModels
{
    /// <summary>
    /// ViewModel главного окна приложения
    /// Ответственности: навигация, управление жизненным циклом, координация, обработка глобальных хоткеев
    /// </summary>
    public partial class ChatCasterWindowViewModel : ViewModelBase
    {
        #region Services

        private readonly ApplicationInitializationService _initializationService;
        private readonly ISystemIntegrationService _systemService;
        private readonly NavigationManager _navigationManager;
        private readonly ITrayService _trayService;
        private readonly IVoiceRecordingService _voiceRecordingService;

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

        #endregion

        #region Constructor

        public ChatCasterWindowViewModel(
            ApplicationInitializationService initializationService,
            ISystemIntegrationService systemService,
            NavigationManager navigationManager,
            ITrayService trayService,
            IVoiceRecordingService voiceRecordingService,
            AppConfig currentConfig)
        {
            _initializationService = initializationService ?? throw new ArgumentNullException(nameof(initializationService));
            _systemService = systemService ?? throw new ArgumentNullException(nameof(systemService));
            _navigationManager = navigationManager ?? throw new ArgumentNullException(nameof(navigationManager));
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));
            _voiceRecordingService = voiceRecordingService ?? throw new ArgumentNullException(nameof(voiceRecordingService));
            _currentConfig = currentConfig ?? throw new ArgumentNullException(nameof(currentConfig));

            // Подписываемся на события навигации
            _navigationManager.NavigationChanged += OnNavigationChanged;

            // Устанавливаем начальную страницу
            CurrentPage = _navigationManager.CurrentPage;
            CurrentPageTag = _navigationManager.CurrentPageTag;

            // Подписка на глобальные хоткеи
            _systemService.GlobalHotkeyPressed += OnGlobalHotkeyPressed;

            Log.Debug("ChatCasterWindowViewModel создан с поддержкой голосовой записи");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Инициализирует приложение при запуске
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                Log.Information("ChatCasterWindowViewModel: начинаем инициализацию приложения");

                // Делегируем всю инициализацию сервису
                CurrentConfig = await _initializationService.InitializeApplicationAsync();

                // Регистрируем глобальный хоткей если настроен
                if (CurrentConfig.Input.KeyboardShortcut != null)
                {
                    var registered = await _systemService.RegisterGlobalHotkeyAsync(CurrentConfig.Input.KeyboardShortcut);
                    Log.Information("ChatCasterWindowViewModel: хоткей зарегистрирован: {Registered}", registered);
                }

                // Применяем настройки окна
                if (CurrentConfig.System.StartMinimized)
                {
                    Log.Debug("ChatCasterWindowViewModel: запуск в свернутом виде");
                    WindowState = WindowState.Minimized;
                }

                StatusText = NavigationConstants.StatusReady;
                Log.Information("ChatCasterWindowViewModel: инициализация завершена");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ChatCasterWindowViewModel: ошибка инициализации");
                StatusText = "Ошибка инициализации";
                _trayService.ShowNotification("Ошибка", "Ошибка инициализации приложения", NotificationType.Error);
            }
        }

        /// <summary>
        /// Переходит к настройкам
        /// </summary>
        public void NavigateToSettings()
        {
            _navigationManager.NavigateToSettings();
        }

        /// <summary>
        /// Очистка ресурсов при закрытии
        /// </summary>
        public void Cleanup()
        {
            if (_isCleanedUp)
            {
                Log.Debug("ChatCasterWindowViewModel: Cleanup уже выполнен, пропускаем");
                return;
            }

            Log.Information("ChatCasterWindowViewModel: Cleanup начат");
            _isCleanedUp = true;

            try
            {
                // Отписываемся от событий
                _systemService.GlobalHotkeyPressed -= OnGlobalHotkeyPressed;
                _navigationManager.NavigationChanged -= OnNavigationChanged;

                // Очищаем страницы
                _navigationManager.CleanupAllPages();

                // Снимаем хоткеи
                try
                {
                    _systemService.UnregisterGlobalHotkeyAsync().Wait(500);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "ChatCasterWindowViewModel: хоткеи не сняты быстро, пропускаем");
                }

                Log.Information("ChatCasterWindowViewModel: Cleanup завершен");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ChatCasterWindowViewModel: ошибка в Cleanup");
            }
        }

        #endregion

        #region Event Handlers

        private void OnNavigationChanged(object? sender, NavigationChangedEventArgs e)
        {
            CurrentPage = e.Page;
            CurrentPageTag = e.PageTag;

            // Уведомляем об изменении всех navigation button свойств
            OnPropertyChanged(nameof(MainButtonBackground));
            OnPropertyChanged(nameof(AudioButtonBackground));
            OnPropertyChanged(nameof(InterfaceButtonBackground));
            OnPropertyChanged(nameof(ControlButtonBackground));
        }

        private async void OnGlobalHotkeyPressed(object? sender, KeyboardShortcut shortcut)
        {
            try
            {
                Log.Debug("ChatCasterWindowViewModel: глобальный хоткей нажат: {Shortcut}", FormatKeyboardShortcut(shortcut));
                
                // Прямая обработка хоткея согласно архитектуре
                await HandleVoiceRecordingAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ChatCasterWindowViewModel: ошибка обработки хоткея");
                _trayService.ShowNotification("Ошибка", "Произошла ошибка при обработке хоткея", NotificationType.Error);
            }
        }

        #endregion

        #region Voice Recording

        /// <summary>
        /// Обрабатывает toggle записи голоса при нажатии глобального хоткея
        /// </summary>
        private async Task HandleVoiceRecordingAsync()
        {
            try
            {
                if (_voiceRecordingService.IsRecording)
                {
                    Log.Debug("ChatCasterWindowViewModel: останавливаем запись голоса");
                    StatusText = NavigationConstants.StatusProcessing;
                    await _voiceRecordingService.StopRecordingAsync();
                }
                else
                {
                    Log.Debug("ChatCasterWindowViewModel: начинаем запись голоса");
                    StatusText = NavigationConstants.StatusRecording;
                    await _voiceRecordingService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ChatCasterWindowViewModel: ошибка обработки записи голоса");
                StatusText = "Ошибка записи";
                _trayService.ShowNotification("Ошибка", "Произошла ошибка при записи голоса", NotificationType.Error);
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

        #region Private Fields

        private bool _isCleanedUp;

        #endregion
    }
}