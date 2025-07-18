using System.Collections.ObjectModel;
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
using System.Windows.Media.Imaging;

namespace ChatCaster.Windows.ViewModels
{
    /// <summary>
    /// ViewModel главного окна приложения
    /// Ответственности: навигация, управление жизненным циклом, координация, обработка глобальных хоткеев
    /// </summary>
    public partial class ChatCasterWindowViewModel : ViewModelBase
    {
        private bool _isUpdatingLanguage;

        #region Services

        private readonly ApplicationInitializationService _initializationService;
        private readonly ISystemIntegrationService _systemService;
        private readonly NavigationManager _navigationManager;
        private readonly ITrayService _trayService;
        private readonly IVoiceRecordingService _voiceRecordingService;
        private readonly ILocalizationService _localizationService;
        private readonly IConfigurationService _configurationService;

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
        private string _statusText;

        [ObservableProperty]
        private string _currentPageTag = NavigationConstants.MainPage;

        [ObservableProperty]
        private ObservableCollection<LanguageItem> _availableLanguages;

        [ObservableProperty]
        private string _selectedLanguage;
        
        [ObservableProperty]
        private string _navigationText = "Навигация";

        [ObservableProperty] 
        private string _mainPageText = "Главное";

        [ObservableProperty]
        private string _audioPageText = "Аудио и распознавание";

        [ObservableProperty]
        private string _interfacePageText = "Интерфейс";

        [ObservableProperty]
        private string _controlPageText = "Управление";


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
            ILocalizationService localizationService,
            IConfigurationService configurationService,
            AppConfig currentConfig)
        {
            _initializationService = initializationService ?? throw new ArgumentNullException(nameof(initializationService));
            _systemService = systemService ?? throw new ArgumentNullException(nameof(systemService));
            _navigationManager = navigationManager ?? throw new ArgumentNullException(nameof(navigationManager));
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));
            _voiceRecordingService = voiceRecordingService ?? throw new ArgumentNullException(nameof(voiceRecordingService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

            _currentConfig = currentConfig ?? throw new ArgumentNullException(nameof(currentConfig));

            // Подписываемся на события навигации
            _navigationManager.NavigationChanged += OnNavigationChanged;

            // Подписываемся на изменение языка
            _localizationService.LanguageChanged += OnLanguageChanged;

            // Устанавливаем начальную страницу
            CurrentPage = _navigationManager.CurrentPage;
            CurrentPageTag = _navigationManager.CurrentPageTag;

            // Подписка на глобальные хоткеи
            _systemService.GlobalHotkeyPressed += OnGlobalHotkeyPressed;

            // Инициализация языков
            InitializeLanguages();
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
                // Делегируем всю инициализацию сервису
                CurrentConfig = await _initializationService.InitializeApplicationAsync();
                
                Log.Information("🔍 ДИАГНОСТИКА: После InitializeApplicationAsync, SelectedLanguage = {Lang}", 
                    CurrentConfig?.System?.SelectedLanguage);

                // Устанавливаем язык из конфигурации
                SelectedLanguage = CurrentConfig.System.SelectedLanguage;
                
                Log.Information("🔍 ДИАГНОСТИКА: После установки SelectedLanguage в UI = {Lang}", 
                    SelectedLanguage);

                UpdateLocalizedStrings();

                // Регистрируем глобальный хоткей если настроен
                if (CurrentConfig.Input.KeyboardShortcut != null)
                {
                    var registered = await _systemService.RegisterGlobalHotkeyAsync(CurrentConfig.Input.KeyboardShortcut);
                }

                // Применяем настройки окна
                if (CurrentConfig.System.StartMinimized)
                {
                    WindowState = WindowState.Minimized;
                }

                // Устанавливаем локализованный статус
                UpdateLocalizedStatus();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ChatCasterWindowViewModel: ошибка инициализации");
                StatusText = _localizationService.GetString("ErrorInitialization");
                _trayService.ShowNotification("Ошибка", _localizationService.GetString("ErrorInitialization"));
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
                return;
            }

            _isCleanedUp = true;

            try
            {
                // Отписываемся от событий
                _systemService.GlobalHotkeyPressed -= OnGlobalHotkeyPressed;
                _navigationManager.NavigationChanged -= OnNavigationChanged;
                _localizationService.LanguageChanged -= OnLanguageChanged;

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

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // Обновляем локализованные строки при смене языка
            UpdateLocalizedStatus();
            UpdateLocalizedStrings();
        }

        private async void OnGlobalHotkeyPressed(object? sender, KeyboardShortcut shortcut)
        {
            try
            {
                // Прямая обработка хоткея согласно архитектуре
                await HandleVoiceRecordingAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ChatCasterWindowViewModel: ошибка обработки хоткея");
                _trayService.ShowNotification(_localizationService.GetString("Error") ?? "Ошибка", _localizationService.GetString("HotkeyError") ?? "Произошла ошибка при обработке хоткея", NotificationType.Error);
            }
        }
        
        
            
        partial void OnSelectedLanguageChanged(string value)
        {
            if (_isUpdatingLanguage) return;

            _isUpdatingLanguage = true;
            try
            {
                Log.Information("🔄 OnSelectedLanguageChanged вызван: {Value}", value);
                Log.Information("🔍 ДИАГНОСТИКА: CurrentConfig.System.SelectedLanguage ДО изменения = {Before}", 
                    CurrentConfig?.System?.SelectedLanguage);

                if (!string.IsNullOrEmpty(value) && CurrentConfig?.System != null)
                {
                    CurrentConfig.System.SelectedLanguage = value;
                    Log.Information("🔍 ДИАГНОСТИКА: CurrentConfig.System.SelectedLanguage ПОСЛЕ изменения = {After}", 
                        CurrentConfig.System.SelectedLanguage);
                    _localizationService.SetLanguage(value);
                    _ = SaveConfigurationAsync();
                }
            }
            finally
            {
                _isUpdatingLanguage = false;
            }
        }
        
        private async Task SaveConfigurationAsync()
        {
            try
            {
                await _configurationService.SaveConfigAsync(CurrentConfig);
                Log.Debug("ChatCasterWindowViewModel: конфигурация сохранена после смены языка");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ChatCasterWindowViewModel: ошибка сохранения конфигурации");
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
                    StatusText = _localizationService.GetString("StatusProcessing");
                    await _voiceRecordingService.StopRecordingAsync();
                }
                else
                {
                    StatusText = _localizationService.GetString("StatusRecording");
                    await _voiceRecordingService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ChatCasterWindowViewModel: ошибка обработки записи голоса");
                StatusText = _localizationService.GetString("ErrorRecording");
                _trayService.ShowNotification(_localizationService.GetString("Error") ?? "Ошибка", _localizationService.GetString("RecordingError"));
            }
        }

        #endregion

        #region Helper Methods

        private void UpdateLocalizedStatus()
        {
            StatusText = _localizationService.GetString("StatusReady");
        }
        
        private void UpdateLocalizedStrings()
        {
            NavigationText = _localizationService.GetString("Navigation");
            MainPageText = _localizationService.GetString("Navigation_Main");
            AudioPageText = _localizationService.GetString("Navigation_Audio");
            InterfacePageText = _localizationService.GetString("Navigation_Interface");
            ControlPageText = _localizationService.GetString("Navigation_Control");
    
            // Обновляем названия языков в ComboBox
            UpdateLanguageDisplayNames();
        }

        private void UpdateLanguageDisplayNames()
        {
            foreach (var lang in AvailableLanguages)
            {
                if (lang.Culture == "ru-RU")
                    lang.DisplayName = _localizationService.GetString("LanguageName_ru-RU");
                else if (lang.Culture == "en-US")
                    lang.DisplayName = _localizationService.GetString("LanguageName_en-US");
            }
            OnPropertyChanged(nameof(AvailableLanguages));
        }


        private void InitializeLanguages()
        {
            AvailableLanguages = new ObservableCollection<LanguageItem>
            {
                new LanguageItem 
                { 
                    Culture = "ru-RU", 
                    DisplayName = _localizationService.GetString("LanguageName_ru-RU"), 
                    FlagImage = new BitmapImage(new Uri("pack://application:,,,/ChatCaster.Windows;component/Resources/russia-flag.png")) 
                },
                new LanguageItem 
                { 
                    Culture = "en-US",  
                    DisplayName = _localizationService.GetString("LanguageName_en-US"), 
                    FlagImage = new BitmapImage(new Uri("pack://application:,,,/ChatCaster.Windows;component/Resources/usa-flag.png")) 
                }
            };
            SelectedLanguage = CurrentConfig.System.SelectedLanguage;
        }
        
        #endregion

        #region Private Fields

        private bool _isCleanedUp;

        #endregion
    }

    // Класс для представления элемента языка в ComboBox
    public class LanguageItem
    {
        public string Culture { get; set; } = string.Empty; 
        public string DisplayName { get; set; } = string.Empty; 
        public BitmapImage? FlagImage { get; set; }
    }
}