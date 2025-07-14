using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
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
            INotificationService notificationService)
            : base(configurationService, currentConfig)
        {
            Log.Information("AudioSettingsViewModel конструктор вызван (рефакторинг)");

            // Создаем менеджеры
            var audioDeviceManager = new AudioDeviceManager(audioService);
            var whisperModelManager = new WhisperModelManager(speechRecognitionService, currentConfig);

            // Инициализируем компоненты
            AudioDeviceComponent = new AudioDeviceComponentViewModel(audioDeviceManager, notificationService);
            WhisperModelComponent = new WhisperModelComponentViewModel(whisperModelManager);

            // Подписываемся на события компонентов
            SubscribeToComponentEvents();

            Log.Information("AudioSettingsViewModel создан с компонентами");
        }

        #endregion

        #region Observable Property Changed Handlers - IMMEDIATE APPLY

        partial void OnMaxRecordingSecondsChanged(int value)
        {
            if (IsLoadingUI) return;
            Log.Information("Время записи изменено: {Seconds}с", value);
            _ = OnUISettingChangedAsync();
        }

        partial void OnSelectedSampleRateChanged(int value)
        {
            if (IsLoadingUI) return;
            Log.Information("Частота дискретизации изменена: {SampleRate}Hz", value);
            _ = OnUISettingChangedAsync();
        }

        #endregion

        #region BaseSettingsViewModel Implementation


        protected override async Task LoadPageSpecificSettingsAsync()
        {
            Log.Information("AudioSettingsViewModel LoadPageSpecificSettingsAsync НАЧАТ");

            try
            {
                // Загружаем аудио настройки
                MaxRecordingSeconds = _currentConfig.Audio.MaxRecordingSeconds;
                SelectedSampleRate = _currentConfig.Audio.SampleRate;

                // Загружаем устройства и устанавливаем выбранное
                await AudioDeviceComponent.LoadDevicesAsync();
                AudioDeviceComponent.SetSelectedDeviceFromConfig(_currentConfig.Audio.SelectedDeviceId);

                // ИСПРАВЛЕНО: Сначала загружаем модели со статусами
                await WhisperModelComponent.LoadModelsWithStatusAsync();
        
                // ПОТОМ устанавливаем выбранную модель (после загрузки коллекции)
                var modelSize = _currentConfig.SpeechRecognition.EngineSettings.TryGetValue("ModelSize", out var modelObj) 
                    ? modelObj?.ToString() 
                    : "tiny"; // Fallback на tiny если не найдено
        
                Log.Information("AudioSettingsViewModel устанавливаем модель из конфига: {ModelSize}", modelSize);
                WhisperModelComponent.SetSelectedModelFromConfig(modelSize);
        
                // Устанавливаем язык
                WhisperModelComponent.SelectedLanguage = _currentConfig.SpeechRecognition.Language;

                // Проверяем статус модели
                await WhisperModelComponent.CheckModelStatusAsync();

                Log.Information("AudioSettingsViewModel настройки загружены успешно");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка в LoadPageSpecificSettingsAsync");
            }
        }

        
        protected override async Task ApplySettingsToConfigAsync(AppConfig config)
        {
            try
            {
                Log.Information("AudioSettingsViewModel применяем настройки к конфигурации");

                // Применяем аудио настройки
                config.Audio.SelectedDeviceId = AudioDeviceComponent.SelectedDevice?.Id ?? "";
                config.Audio.SampleRate = SelectedSampleRate;
                config.Audio.MaxRecordingSeconds = MaxRecordingSeconds;

                // Применяем Whisper настройки
                config.SpeechRecognition.Language = WhisperModelComponent.SelectedLanguage;
                config.SpeechRecognition.EngineSettings["ModelSize"] = WhisperModelComponent.SelectedModel?.ModelSize ?? "tiny";
                
                Log.Information("AudioSettingsViewModel настройки применены к конфигурации");
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
                Log.Information("AudioSettingsViewModel применяем настройки к сервисам");

                // Применяем аудио устройство
                var deviceApplied = await AudioDeviceComponent.ApplySelectedDeviceAsync();
                if (deviceApplied)
                {
                    Log.Information("AudioSettingsViewModel аудио устройство применено");
                }

                // Применяем модель Whisper через существующий менеджер
                Log.Information("🔍 [APPLY] Применяем модель Whisper к сервису");
                var modelApplied = await WhisperModelComponent.ModelManager.ApplyCurrentConfigAsync();
        
                if (modelApplied)
                {
                    Log.Information("🔍 [APPLY] ✅ Модель Whisper успешно применена");
                }
                else
                {
                    Log.Error("🔍 [APPLY] ❌ Ошибка применения модели Whisper");
                }

                Log.Information("AudioSettingsViewModel настройки применены к сервисам");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка применения настроек к сервисам");
                throw;
            }
        }
        
        public override void SubscribeToUIEvents()
        {
            Log.Information("AudioSettingsViewModel UI события обрабатываются через компоненты и Observable свойства");
        }

        protected override void CleanupPageSpecific()
        {
            try
            {
                Log.Debug("AudioSettingsViewModel Cleanup начат");
                UnsubscribeFromComponentEvents();
                Log.Information("AudioSettingsViewModel Cleanup завершен");
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
                
                Log.Debug("AudioSettingsViewModel события компонентов подписаны");
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
                
                Log.Debug("AudioSettingsViewModel события компонентов отписаны");
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
            Log.Debug("AudioSettingsViewModel устройство изменено, применяем настройки");
            await OnUISettingChangedAsync();
        }

        private async Task OnModelChangedAsync()
        {
            if (IsLoadingUI) return;
            Log.Debug("AudioSettingsViewModel модель изменена, применяем настройки");
            await OnUISettingChangedAsync();
        }

        private async Task OnLanguageChangedAsync()
        {
            if (IsLoadingUI) return;
            Log.Debug("AudioSettingsViewModel язык изменен, применяем настройки");
            await OnUISettingChangedAsync();
        }

        private void OnComponentStatusChanged(string status)
        {
            StatusMessage = status;
            MicrophoneStatusText = status;
            MicrophoneStatusColor = DetermineStatusColor(status);
            Log.Debug("AudioSettingsViewModel статус от компонента: {Status}", status);
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