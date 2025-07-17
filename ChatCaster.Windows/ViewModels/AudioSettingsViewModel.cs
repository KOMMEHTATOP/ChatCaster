using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.System;
using ChatCaster.Core.Services.UI;
using ChatCaster.Windows.Managers.AudioSettings;
using ChatCaster.Windows.ViewModels.Base;
using ChatCaster.Windows.ViewModels.Components;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace ChatCaster.Windows.ViewModels
{
    public partial class AudioSettingsViewModel : BaseSettingsViewModel
    {
        #region Services
        
        private readonly ILocalizationService _localizationService;
        
        #endregion

        #region Components

        public AudioDeviceComponentViewModel AudioDeviceComponent { get; }
        public WhisperModelComponentViewModel WhisperModelComponent { get; }

        #endregion

        #region Observable Properties

        [ObservableProperty]
        private int _maxRecordingSeconds = 10;

        [ObservableProperty]
        private int _selectedSampleRate = 16000;

        [ObservableProperty]
        private string _microphoneStatusText = "Микрофон готов";

        [ObservableProperty]
        private string _microphoneStatusColor = "#4caf50";

        // ЛОКАЛИЗОВАННЫЕ СВОЙСТВА
        [ObservableProperty]
        private string _pageTitle = "Аудио и распознавание";

        [ObservableProperty]
        private string _pageDescription = "Настройка микрофона, модели Whisper и параметров записи";

        [ObservableProperty]
        private string _mainSettingsTitle = "Основные настройки";

        [ObservableProperty]
        private string _microphoneLabel = "Микрофон:";

        [ObservableProperty]
        private string _whisperModelLabel = "Модель Whisper:";

        [ObservableProperty]
        private string _recordingDurationLabel = "Длительность записи:";

        [ObservableProperty]
        private string _languageLabel = "Язык:";

        [ObservableProperty]
        private string _autoSaveText = "Автоматическое сохранение при изменении";

        /// <summary>
        /// Доступные частоты дискретизации
        /// </summary>
        public List<int> AvailableSampleRates { get; } = new()
        {
            8000, 16000, 22050, 44100, 48000
        };

        #endregion

        #region Constructor

        public AudioSettingsViewModel(
            IConfigurationService configurationService,
            AppConfig currentConfig,
            ISpeechRecognitionService speechRecognitionService,
            IAudioCaptureService audioService,
            INotificationService notificationService,
            ILocalizationService localizationService) 
            : base(configurationService, currentConfig)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            
            Log.Information("🔍 AudioSettingsViewModel создан с AppConfig HashCode: {HashCode}, SelectedLanguage: {Language}", 
                currentConfig.GetHashCode(), currentConfig.System.SelectedLanguage);

            // Создаем менеджеры (теперь передаем localizationService в WhisperModelManager)
            var audioDeviceManager = new AudioDeviceManager(audioService);
            var whisperModelManager = new WhisperModelManager(speechRecognitionService, currentConfig, localizationService);

            // Инициализируем компоненты (теперь передаем localizationService в WhisperModelComponent)
            AudioDeviceComponent = new AudioDeviceComponentViewModel(audioDeviceManager, notificationService);
            WhisperModelComponent = new WhisperModelComponentViewModel(whisperModelManager, localizationService);

            // Подписываемся на события компонентов
            SubscribeToComponentEvents();

            // ПОДПИСЫВАЕМСЯ НА ИЗМЕНЕНИЕ ЯЗЫКА И ИНИЦИАЛИЗИРУЕМ СТРОКИ
            _localizationService.LanguageChanged += OnLanguageChanged;
            UpdateLocalizedStrings();
        }

        #endregion

        #region Localization

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLocalizedStrings();
        }

        private void UpdateLocalizedStrings()
        {
            PageTitle = _localizationService.GetString("Audio_PageTitle");
            PageDescription = _localizationService.GetString("Audio_PageDescription");
            MainSettingsTitle = _localizationService.GetString("Audio_MainSettings");
            MicrophoneLabel = _localizationService.GetString("Audio_Microphone");
            WhisperModelLabel = _localizationService.GetString("Audio_WhisperModel");
            RecordingDurationLabel = _localizationService.GetString("Audio_RecordingDuration");
            LanguageLabel = _localizationService.GetString("Audio_Language");
            AutoSaveText = _localizationService.GetString("Audio_AutoSave");
        }

        #endregion

        #region Observable Property Changed Handlers - IMMEDIATE APPLY

        partial void OnMaxRecordingSecondsChanged(int value)
        {
            if (IsLoadingUI) return;
            _ = OnUISettingChangedAsync();
        }

        partial void OnSelectedSampleRateChanged(int value)
        {
            if (IsLoadingUI) return;
            _ = OnUISettingChangedAsync();
        }

        #endregion

        #region BaseSettingsViewModel Implementation

        protected override async Task LoadPageSpecificSettingsAsync()
        {
            try
            {
                // Загружаем аудио настройки
                MaxRecordingSeconds = _currentConfig.Audio.MaxRecordingSeconds;
                SelectedSampleRate = _currentConfig.Audio.SampleRate;

                // Загружаем устройства и устанавливаем выбранное
                await AudioDeviceComponent.LoadDevicesAsync();
                AudioDeviceComponent.SetSelectedDeviceFromConfig(_currentConfig.Audio.SelectedDeviceId);

                // Сначала загружаем модели со статусами
                await WhisperModelComponent.LoadModelsWithStatusAsync();
        
                // устанавливаем выбранную модель (после загрузки коллекции)
                var modelSize = _currentConfig.SpeechRecognition.EngineSettings.TryGetValue("ModelSize", out var modelObj) 
                    ? modelObj?.ToString() 
                    : "tiny"; // Fallback на tiny если не найдено
        
                WhisperModelComponent.SetSelectedModelFromConfig(modelSize);
        
                // Устанавливаем язык
                WhisperModelComponent.SelectedLanguage = _currentConfig.SpeechRecognition.Language;

                // Проверяем статус модели
                await WhisperModelComponent.CheckModelStatusAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка в LoadPageSpecificSettingsAsync");
            }
        }

        protected override Task ApplySettingsToConfigAsync(AppConfig config)
        {
            Log.Information("🔍 ДИАГНОСТИКА: _currentConfig.System.SelectedLanguage = {CurrentLang}", 
                _currentConfig.System.SelectedLanguage);
            Log.Information("🔍 ДИАГНОСТИКА: config.System.SelectedLanguage ДО изменения = {ConfigLang}", 
                config.System.SelectedLanguage);

            try
            {
                // Применяем аудио настройки
                config.Audio.SelectedDeviceId = AudioDeviceComponent.SelectedDevice?.Id ?? "";
                config.Audio.SampleRate = SelectedSampleRate;
                config.Audio.MaxRecordingSeconds = MaxRecordingSeconds;

                // Применяем Whisper настройки
                config.SpeechRecognition.Language = WhisperModelComponent.SelectedLanguage;
                config.SpeechRecognition.EngineSettings["ModelSize"] = WhisperModelComponent.SelectedModel?.ModelSize ?? "tiny";
                config.System.SelectedLanguage = _currentConfig.System.SelectedLanguage;
                
                Log.Information("🔍 ДИАГНОСТИКА: config.System.SelectedLanguage ПОСЛЕ изменения = {ConfigLang}", 
                    config.System.SelectedLanguage);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка применения настроек к конфигурации");
                throw;
            }
        }

        protected override async Task ApplySettingsToServicesAsync()
        {
            try
            {
                // Применяем аудио устройство
                var deviceApplied = await AudioDeviceComponent.ApplySelectedDeviceAsync();

                // Применяем модель Whisper через существующий менеджер
                var modelApplied = await WhisperModelComponent.ModelManager.ApplyCurrentConfigAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка применения настроек к сервисам");
                throw;
            }
        }
        
        public override void SubscribeToUIEvents()
        {
        }

        protected override void CleanupPageSpecific()
        {
            try
            {
                _localizationService.LanguageChanged -= OnLanguageChanged;
                
                UnsubscribeFromComponentEvents();
                
                // Очищаем компоненты
                WhisperModelComponent.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при cleanup AudioSettingsViewModel");
            }
        }

        #endregion

        #region Component Events Management

        private void SubscribeToComponentEvents()
        {
            try
            {
                AudioDeviceComponent.DeviceChanged += OnDeviceChangedAsync;
                AudioDeviceComponent.StatusChanged += OnComponentStatusChanged;
                WhisperModelComponent.ModelChanged += OnModelChangedAsync;
                WhisperModelComponent.LanguageChanged += OnLanguageChangedAsync;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка подписки на события компонентов");
            }
        }

        private void UnsubscribeFromComponentEvents()
        {
            try
            {
                AudioDeviceComponent.DeviceChanged -= OnDeviceChangedAsync;
                AudioDeviceComponent.StatusChanged -= OnComponentStatusChanged;
                WhisperModelComponent.ModelChanged -= OnModelChangedAsync;
                WhisperModelComponent.LanguageChanged -= OnLanguageChangedAsync;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка отписки от событий компонентов");
            }
        }

        #endregion

        #region Event Handlers

        private async Task OnDeviceChangedAsync()
        {
            if (IsLoadingUI) return;
            await OnUISettingChangedAsync();
        }

        private async Task OnModelChangedAsync()
        {
            if (IsLoadingUI) return;
            await OnUISettingChangedAsync();
        }

        private async Task OnLanguageChangedAsync()
        {
            if (IsLoadingUI) return;
            await OnUISettingChangedAsync();
        }

        private void OnComponentStatusChanged(string status)
        {
            StatusMessage = status;
            MicrophoneStatusText = status;
            MicrophoneStatusColor = DetermineStatusColor(status);
        }

        /// <summary>
        /// Определяет цвет статуса микрофона по тексту
        /// </summary>
        private string DetermineStatusColor(string status)
        {
            return status.ToLower() switch
            {
                var s when s.Contains("тестируется") => "#ff9800", // Оранжевый
                var s when s.Contains("работает") => "#4caf50",    // Зеленый
                var s when s.Contains("проблема") || s.Contains("ошибка") => "#f44336", // Красный
                _ => "#4caf50" // По умолчанию зеленый
            };
        }

        #endregion
    }
}