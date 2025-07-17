using ChatCaster.Core.Events;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.System;
using ChatCaster.Windows.Managers.MainPage;
using ChatCaster.Windows.ViewModels.Base;
using ChatCaster.Windows.ViewModels.Components;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ChatCaster.Windows.ViewModels
{
    /// <summary>
    /// ViewModel главной страницы приложения
    /// Ответственности: координация компонентов записи и отображения результатов
    /// </summary>
    public partial class MainPageViewModel : ViewModelBase
    {
        #region Services

        private readonly IVoiceRecordingService _voiceRecordingService;
        private readonly DeviceDisplayManager _deviceDisplayManager;
        private readonly ILocalizationService _localizationService;
        private readonly IConfigurationService _configurationService;

        #endregion

        #region Components

        public RecordingStatusComponentViewModel RecordingStatusComponent { get; }
        public RecognitionResultsComponentViewModel RecognitionResultsComponent { get; }

        #endregion

        #region Observable Properties

        [ObservableProperty]
        private string _currentDeviceText = "Устройство не выбрано";
        [ObservableProperty]
        private string _lastResultTitle = "Последний результат";

        #endregion

        #region Constructor

        public MainPageViewModel(
            IVoiceRecordingService voiceRecordingService,
            IConfigurationService configurationService,
            ILocalizationService localizationService,
            RecordingStatusComponentViewModel recordingStatusComponent,
            RecognitionResultsComponentViewModel recognitionResultsComponent,
            DeviceDisplayManager deviceDisplayManager)
        {
            _voiceRecordingService = voiceRecordingService ?? throw new ArgumentNullException(nameof(voiceRecordingService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _deviceDisplayManager = deviceDisplayManager ?? throw new ArgumentNullException(nameof(deviceDisplayManager));

            // Компоненты из DI
            RecordingStatusComponent = recordingStatusComponent ?? throw new ArgumentNullException(nameof(recordingStatusComponent));
            RecognitionResultsComponent = recognitionResultsComponent ?? throw new ArgumentNullException(nameof(recognitionResultsComponent));

            // Подписываемся на локализацию
            _localizationService.LanguageChanged += OnLanguageChanged;
            UpdateLocalizedStrings();

            // Подписываемся на события
            SubscribeToEvents();

            // Инициализируем отображение устройства
            InitializeDeviceDisplayAsync(_deviceDisplayManager);

            // Устанавливаем начальные состояния
            RecordingStatusComponent.SetInitialState();
            RecognitionResultsComponent.SetInitialState();

            // Подписываемся на изменения конфигурации для обновления устройства
            configurationService.ConfigurationChanged += OnConfigurationChanged;

            Log.Debug("MainPageViewModel инициализирован с компонентами");
        }

        #endregion

        #region Commands

        [RelayCommand]
        public async Task ToggleRecording()
        {
            try
            {
                if (_voiceRecordingService.IsRecording)
                {
                    Log.Debug("MainPageViewModel: останавливаем запись");
                    await _voiceRecordingService.StopRecordingAsync();
                }
                else
                {
                    Log.Debug("MainPageViewModel: начинаем запись");
                    await _voiceRecordingService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MainPageViewModel: ошибка переключения записи");
                RecordingStatusComponent.SetErrorState($"Ошибка: {ex.Message}");
            }
        }

        #endregion

        #region Event Subscription

        private void SubscribeToEvents()
        {
            try
            {
                // Подписываемся на события записи
                _voiceRecordingService.StatusChanged += OnRecordingStatusChanged;
                _voiceRecordingService.RecognitionCompleted += OnRecognitionCompleted;

                // Подписываемся на события компонентов
                RecognitionResultsComponent.TextRecognized += OnTextRecognized;

                Log.Debug("MainPageViewModel: события подписаны");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MainPageViewModel: ошибка подписки на события");
            }
        }

        private void UnsubscribeFromEvents()
        {
            try
            {
                // Отписываемся от событий записи
                _voiceRecordingService.StatusChanged -= OnRecordingStatusChanged;
                _voiceRecordingService.RecognitionCompleted -= OnRecognitionCompleted;

                // Отписываемся от событий компонентов
                RecognitionResultsComponent.TextRecognized -= OnTextRecognized;

                _localizationService.LanguageChanged -= OnLanguageChanged;
                _configurationService.ConfigurationChanged -= OnConfigurationChanged;
                
                Log.Debug("MainPageViewModel: события отписаны");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MainPageViewModel: ошибка отписки от событий");
            }
        }

        #endregion

        #region Event Handlers

        private void OnRecordingStatusChanged(object? sender, RecordingStatusChangedEvent e)
        {
            try
            {
                RecordingStatusComponent.UpdateRecordingStatus(e);
                Log.Debug("MainPageViewModel: статус записи обновлен: {Status}", e.NewStatus);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MainPageViewModel: ошибка обработки изменения статуса записи");
            }
        }

        private void OnRecognitionCompleted(object? sender, VoiceRecognitionCompletedEvent e)
        {
            try
            {
                RecognitionResultsComponent.HandleRecognitionCompleted(e);
                Log.Information("MainPageViewModel: обработано завершение распознавания");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MainPageViewModel: ошибка обработки завершения распознавания");
            }
        }

        private void OnTextRecognized(string recognizedText)
        {
            try
            {
                // Дополнительная обработка распознанного текста если нужна
                Log.Information("MainPageViewModel: распознан текст: {Text}", recognizedText);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MainPageViewModel: ошибка обработки распознанного текста");
            }
        }

        private async void OnConfigurationChanged(object? sender, ConfigurationChangedEvent e)
        {
            try
            {
                // Обновляем отображение устройства при изменении конфигурации
                if (e.SettingName == "ConfigurationLoaded" || e.SettingName == "ConfigurationSaved")
                {
                    Log.Information("MainPageViewModel: обновляем отображение устройства после {EventType}", e.SettingName);
                    await UpdateDeviceDisplayAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MainPageViewModel: ошибка обработки изменения конфигурации");
            }
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLocalizedStrings();
        }

        #endregion

        #region Initialization

        private async void InitializeDeviceDisplayAsync(DeviceDisplayManager deviceDisplayManager)
        {
            try
            {
                var deviceInfo = await deviceDisplayManager.GetCurrentDeviceDisplayAsync();
                CurrentDeviceText = deviceInfo.FullDisplayText;
                Log.Debug("MainPageViewModel: отображение устройства инициализировано: {DeviceText}", CurrentDeviceText);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MainPageViewModel: ошибка инициализации отображения устройства");
                CurrentDeviceText = "Устройство: Ошибка получения";
            }
        }

        private async Task UpdateDeviceDisplayAsync()
        {
            try
            {
                var deviceInfo = await _deviceDisplayManager.GetCurrentDeviceDisplayAsync();
                CurrentDeviceText = deviceInfo.FullDisplayText;
                Log.Debug("MainPageViewModel: отображение устройства обновлено: {DeviceText}", CurrentDeviceText);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MainPageViewModel: ошибка обновления отображения устройства");
            }
        }

        private void UpdateLocalizedStrings()
        {
            LastResultTitle = _localizationService.GetString("Main_LastResult");
            CurrentDeviceText = _localizationService.GetString("Main_CurrentDevice");
        }

        #endregion

        #region Cleanup

        public void Cleanup()
        {
            try
            {
                Log.Debug("MainPageViewModel: начинаем cleanup");

                UnsubscribeFromEvents();

                // Очищаем компоненты
                RecordingStatusComponent.Dispose();
                RecognitionResultsComponent.Dispose();

                Log.Information("MainPageViewModel: cleanup завершен");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MainPageViewModel: ошибка при cleanup");
            }
        }

        #endregion
    }
}