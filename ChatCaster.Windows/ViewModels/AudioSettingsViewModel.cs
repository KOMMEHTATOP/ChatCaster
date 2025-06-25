using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels.Base;

namespace ChatCaster.Windows.ViewModels
{
    public partial class AudioSettingsViewModel : BaseSettingsViewModel
    {
        #region Private Services
        private readonly AudioCaptureService? _audioCaptureService;
        private readonly SpeechRecognitionService? _speechRecognitionService;
        #endregion

        #region Observable Properties

        [ObservableProperty]
        private ObservableCollection<AudioDeviceItem> _availableDevices = new();

        [ObservableProperty]
        private AudioDeviceItem? _selectedDevice;

        [ObservableProperty]
        private ObservableCollection<WhisperModelItem> _availableModels = new();

        [ObservableProperty]
        private WhisperModelItem? _selectedModel;

        [ObservableProperty]
        private ObservableCollection<LanguageItem> _availableLanguages = new();

        [ObservableProperty]
        private LanguageItem? _selectedLanguage;

        [ObservableProperty]
        private int _maxRecordingDuration = 30;

        [ObservableProperty]
        private string _maxDurationText = "30с";

        [ObservableProperty]
        private string _microphoneStatusText = "Микрофон готов";

        [ObservableProperty]
        private string _microphoneStatusColor = "#4caf50";

        [ObservableProperty]
        private string _modelStatusText = "Модель готова";

        [ObservableProperty]
        private string _modelStatusColor = "#4caf50";

        [ObservableProperty]
        private bool _isTestingMicrophone = false;

        [ObservableProperty]
        private bool _isDownloadingModel = false;

        [ObservableProperty]
        private bool _isDownloadButtonVisible = false;

        #endregion

        #region Commands

        [RelayCommand]
        private async Task TestMicrophone()
        {
            if (IsTestingMicrophone || _audioCaptureService == null) return;

            try
            {
                IsTestingMicrophone = true;
                UpdateMicrophoneStatus("Тестируется...", "#ff9800");

                // Устанавливаем выбранное устройство
                if (SelectedDevice != null)
                {
                    await _audioCaptureService.SetActiveDeviceAsync(SelectedDevice.Id);
                }

                // Тестируем микрофон
                bool testResult = await _audioCaptureService.TestMicrophoneAsync();

                if (testResult)
                {
                    UpdateMicrophoneStatus("Микрофон работает", "#4caf50");
                }
                else
                {
                    UpdateMicrophoneStatus("Проблема с микрофоном", "#f44336");
                }
            }
            catch (Exception ex)
            {
                UpdateMicrophoneStatus($"Ошибка тестирования: {ex.Message}", "#f44336");
            }
            finally
            {
                IsTestingMicrophone = false;
            }
        }

        [RelayCommand]
        private async Task DownloadModel()
        {
            if (IsDownloadingModel || _speechRecognitionService == null || SelectedModel == null) return;

            try
            {
                IsDownloadingModel = true;
                UpdateModelStatus("Начинаем загрузку...", "#ff9800");

                // Подписываемся на события загрузки
                _speechRecognitionService.DownloadProgress += OnModelDownloadProgress;
                _speechRecognitionService.DownloadCompleted += OnModelDownloadCompleted;

                // Инициализируем модель (это запустит загрузку если нужно)
                var config = new WhisperConfig { Model = SelectedModel.Model };
                await _speechRecognitionService.InitializeAsync(config);
            }
            catch (Exception ex)
            {
                UpdateModelStatus($"Ошибка загрузки: {ex.Message}", "#f44336");
                IsDownloadingModel = false;
            }
        }

        #endregion

        #region Constructor
        public AudioSettingsViewModel(
            ConfigurationService? configurationService,
            ServiceContext? serviceContext,
            AudioCaptureService? audioCaptureService,
            SpeechRecognitionService? speechRecognitionService) : base(configurationService, serviceContext)
        {
            _audioCaptureService = audioCaptureService;
            _speechRecognitionService = speechRecognitionService;
            
            InitializeStaticData();
        }
        #endregion

        #region BaseSettingsViewModel Implementation

        protected override async Task LoadPageSpecificSettingsAsync()
        {
            if (_serviceContext?.Config == null) return;

            var config = _serviceContext.Config;

            // Применяем настройки аудио
            MaxRecordingDuration = config.Audio.MaxRecordingSeconds;
            MaxDurationText = $"{config.Audio.MaxRecordingSeconds}с";

            // Выбираем сохраненное устройство
            if (!string.IsNullOrEmpty(config.Audio.SelectedDeviceId))
            {
                SelectedDevice = AvailableDevices.FirstOrDefault(d => d.Id == config.Audio.SelectedDeviceId);
            }

            // Выбираем сохраненную модель Whisper
            SelectedModel = AvailableModels.FirstOrDefault(m => m.Model == config.Whisper.Model);

            // Выбираем сохраненный язык
            SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == config.Whisper.Language);

            Console.WriteLine("Настройки аудио загружены");
        }

        protected override async Task ApplySettingsToConfigAsync(AppConfig config)
        {
            // Собираем данные из UI и обновляем конфигурацию
            if (SelectedDevice != null)
            {
                config.Audio.SelectedDeviceId = SelectedDevice.Id;
            }

            if (SelectedModel != null)
            {
                config.Whisper.Model = SelectedModel.Model;
            }

            if (SelectedLanguage != null)
            {
                config.Whisper.Language = SelectedLanguage.Code;
            }

            config.Audio.MaxRecordingSeconds = MaxRecordingDuration;

            await Task.CompletedTask;
        }

        protected override async Task ApplySettingsToServicesAsync()
        {
            // Применяем к аудио сервису если выбрано новое устройство
            if (_audioCaptureService != null && SelectedDevice != null)
            {
                await _audioCaptureService.SetActiveDeviceAsync(SelectedDevice.Id);
            }
        }

        protected override async Task InitializePageSpecificDataAsync()
        {
            await LoadMicrophoneDevicesAsync();
            await CheckModelStatusAsync();
        }

        public override void SubscribeToUIEvents()
        {
            // Подписываемся на изменения выбора через PropertyChanged
            PropertyChanged += OnPropertyChanged;
        }

        protected override void UnsubscribeFromUIEvents()
        {
            PropertyChanged -= OnPropertyChanged;
            
            // Отписываемся от событий загрузки модели если подписаны
            if (_speechRecognitionService != null)
            {
                _speechRecognitionService.DownloadProgress -= OnModelDownloadProgress;
                _speechRecognitionService.DownloadCompleted -= OnModelDownloadCompleted;
            }
        }

        #endregion

        #region Private Methods

        private void InitializeStaticData()
        {
            // Инициализируем доступные модели Whisper
            AvailableModels.Clear();
            AvailableModels.Add(new WhisperModelItem(WhisperModel.Tiny, "🏃 Tiny (~76 MB)"));
            AvailableModels.Add(new WhisperModelItem(WhisperModel.Base, "⚡ Base (~145 MB)"));
            AvailableModels.Add(new WhisperModelItem(WhisperModel.Small, "🎯 Small (~476 MB)"));
            AvailableModels.Add(new WhisperModelItem(WhisperModel.Medium, "🔥 Medium (~1.5 GB)"));
            AvailableModels.Add(new WhisperModelItem(WhisperModel.Large, "🚀 Large (~3.0 GB)"));

            // Инициализируем доступные языки
            AvailableLanguages.Clear();
            AvailableLanguages.Add(new LanguageItem("ru", "🇷🇺 Русский"));
            AvailableLanguages.Add(new LanguageItem("en", "🇺🇸 English"));

            // Устанавливаем значения по умолчанию
            SelectedModel = AvailableModels.FirstOrDefault(m => m.Model == WhisperModel.Base);
            SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == "ru");
        }

        private async Task LoadMicrophoneDevicesAsync()
        {
            try
            {
                if (_audioCaptureService == null) return;

                var devices = await _audioCaptureService.GetAvailableDevicesAsync();
                
                AvailableDevices.Clear();
                foreach (var device in devices)
                {
                    var deviceItem = new AudioDeviceItem(device.Id, device.Name, device.IsDefault);
                    AvailableDevices.Add(deviceItem);

                    // Выбираем устройство по умолчанию если нет выбранного
                    if (SelectedDevice == null && device.IsDefault)
                    {
                        SelectedDevice = deviceItem;
                    }
                }

                UpdateMicrophoneStatus("Микрофон готов", "#4caf50");
            }
            catch (Exception ex)
            {
                UpdateMicrophoneStatus($"Ошибка загрузки устройств: {ex.Message}", "#f44336");
            }
        }

        private async Task CheckModelStatusAsync()
        {
            try
            {
                if (_speechRecognitionService == null || SelectedModel == null) return;

                bool isAvailable = await _speechRecognitionService.IsModelAvailableAsync(SelectedModel.Model);
                
                if (isAvailable)
                {
                    UpdateModelStatus("Модель готова", "#4caf50");
                    IsDownloadButtonVisible = false;
                }
                else
                {
                    long sizeBytes = await _speechRecognitionService.GetModelSizeAsync(SelectedModel.Model);
                    string sizeText = FormatFileSize(sizeBytes);
                    UpdateModelStatus($"Модель не скачана ({sizeText})", "#ff9800");
                    IsDownloadButtonVisible = true;
                }
            }
            catch (Exception ex)
            {
                UpdateModelStatus($"Ошибка проверки модели: {ex.Message}", "#f44336");
            }
        }

        private async void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (IsLoadingUI) return;

            switch (e.PropertyName)
            {
                case nameof(SelectedDevice):
                case nameof(SelectedLanguage):
                case nameof(MaxRecordingDuration):
                    await OnUISettingChangedAsync();
                    break;
                    
                case nameof(SelectedModel):
                    await OnUISettingChangedAsync();
                    await CheckModelStatusAsync();
                    break;
            }
        }

        partial void OnMaxRecordingDurationChanged(int value)
        {
            MaxDurationText = $"{value}с";
        }

        private void OnModelDownloadProgress(object? sender, ModelDownloadProgressEvent e)
        {
            UpdateModelStatus($"Загрузка {e.ProgressPercentage}%...", "#ff9800");
        }

        private void OnModelDownloadCompleted(object? sender, ModelDownloadCompletedEvent e)
        {
            // Отписываемся от событий
            if (_speechRecognitionService != null)
            {
                _speechRecognitionService.DownloadProgress -= OnModelDownloadProgress;
                _speechRecognitionService.DownloadCompleted -= OnModelDownloadCompleted;
            }

            if (e.Success)
            {
                UpdateModelStatus("Модель готова", "#4caf50");
                IsDownloadButtonVisible = false;
            }
            else
            {
                UpdateModelStatus($"Ошибка загрузки: {e.ErrorMessage}", "#f44336");
            }

            IsDownloadingModel = false;
        }

        private void UpdateMicrophoneStatus(string text, string color)
        {
            MicrophoneStatusText = text;
            MicrophoneStatusColor = color;
        }

        private void UpdateModelStatus(string text, string color)
        {
            ModelStatusText = text;
            ModelStatusColor = color;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes >= 1_073_741_824) // GB
                return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) // MB
                return $"{bytes / 1_048_576.0:F0} MB";
            if (bytes >= 1024) // KB
                return $"{bytes / 1024.0:F0} KB";
            return $"{bytes} bytes";
        }

        #endregion
    }

    #region Helper Classes

    public class AudioDeviceItem
    {
        public string Id { get; }
        public string Name { get; }
        public bool IsDefault { get; }

        public AudioDeviceItem(string id, string name, bool isDefault)
        {
            Id = id;
            Name = name;
            IsDefault = isDefault;
        }

        public override string ToString() => Name;
    }

    public class WhisperModelItem
    {
        public WhisperModel Model { get; }
        public string DisplayName { get; }

        public WhisperModelItem(WhisperModel model, string displayName)
        {
            Model = model;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }

    public class LanguageItem
    {
        public string Code { get; }
        public string DisplayName { get; }

        public LanguageItem(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }

    #endregion
}