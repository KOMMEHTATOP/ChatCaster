using System.Collections.ObjectModel;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.UI;
using ChatCaster.Windows.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ChatCaster.Windows.ViewModels
{
    public partial class MainPageViewModel : ViewModelBase
    {
        #region Services

        private readonly IAudioCaptureService _audioService;
        private readonly IVoiceRecordingService _voiceRecordingService;
        private readonly AppConfig _config;
        private readonly INotificationService _notificationService;
        private readonly IConfigurationService _configurationService;

        #endregion

        #region Observable Properties

        [ObservableProperty]
        private string _currentMicrophone = "Не выбран";

        [ObservableProperty]
        private float _microphoneLevel = 0.0f;

        [ObservableProperty]
        private bool _isRecording = false;

        [ObservableProperty]
        private string _recordingStatusText = "Готов к записи";

        [ObservableProperty]
        private string _lastRecognizedText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _recentRecognitions = new();

        [ObservableProperty]
        private string _recordButtonText = "Записать";

        [ObservableProperty]
        private string _resultText = "Здесь появится распознанный текст...";

        [ObservableProperty]
        private string _confidenceText = "";

        [ObservableProperty]
        private string _processingTimeText = "";

        [ObservableProperty]
        private string _currentDeviceText = "Устройство не выбрано";

        [ObservableProperty]
        private System.Windows.Media.Brush _recordingStatusBrush = System.Windows.Media.Brushes.White;

        [ObservableProperty]
        private System.Windows.Media.Brush _resultTextBrush = System.Windows.Media.Brushes.Gray;

        [ObservableProperty]
        private System.Windows.FontStyle _resultFontStyle = System.Windows.FontStyles.Italic;

        #endregion

        #region Commands

        [RelayCommand]
        public async Task ToggleRecording()
        {
            try
            {
                if (_voiceRecordingService.IsRecording)
                {
                    Log.Debug("Останавливаем запись через главную страницу");
                    RecordButtonText = "Обработка...";
                    var result = await _voiceRecordingService.StopRecordingAsync();
                    
                    if (result.Success && !string.IsNullOrEmpty(result.RecognizedText))
                    {
                        LastRecognizedText = result.RecognizedText;
                        ResultText = result.RecognizedText;
                        AddToRecentRecognitions(result.RecognizedText);
                    }
                    RecordButtonText = "Записать";
                }
                else
                {
                    Log.Debug("Начинаем запись через главную страницу");
                    RecordButtonText = "Остановить";
                    await _voiceRecordingService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка переключения записи на главной странице");
                RecordingStatusText = $"Ошибка: {ex.Message}";
                RecordButtonText = "Записать";
            }
        }

        [RelayCommand]
        private async Task TestMicrophone()
        {
            try
            {
                Log.Debug("Тестирование микрофона");
                
                RecordingStatusText = "Тестирование микрофона...";
                
                var result = await _audioService.TestMicrophoneAsync();
                
                if (result)
                {
                    RecordingStatusText = "Микрофон работает";
                    
                    // Отправляем уведомление об успешном тесте через новый сервис
                    var deviceName = !string.IsNullOrEmpty(CurrentMicrophone) && CurrentMicrophone != "Не выбран" 
                        ? CurrentMicrophone 
                        : null;
                    _notificationService.NotifyMicrophoneTest(true, deviceName);
                }
                else
                {
                    RecordingStatusText = "Проблема с микрофоном";
                    
                    // Отправляем уведомление об ошибке теста через новый сервис
                    _notificationService.NotifyMicrophoneTest(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка тестирования микрофона");
                RecordingStatusText = $"Ошибка тестирования: {ex.Message}";
                
                // Отправляем уведомление об ошибке через новый сервис
                _notificationService.NotifyMicrophoneTest(false);
            }
        }

        #endregion

        #region Constructor

        public MainPageViewModel(
            IAudioCaptureService audioService,
            IVoiceRecordingService voiceRecordingService,
            AppConfig config,
            INotificationService notificationService,
            IConfigurationService configurationService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _voiceRecordingService = voiceRecordingService ?? throw new ArgumentNullException(nameof(voiceRecordingService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService)); // ✅ ЗАМЕНИЛИ инициализацию
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService)); // ✅ ДОБАВЛЕНО

            // Подписываемся на события
            SubscribeToEvents();

            // Инициализируем начальные значения
            InitializeInitialValues();

            Log.Debug("MainPageViewModel инициализирован с INotificationService"); // ✅ ОБНОВИЛИ лог
        }

        #endregion

        #region Initialization

        private void InitializeInitialValues()
        {
            try
            {
                // Получаем человеческое имя устройства
                SetDeviceNameFromConfig();
                
                // Устанавливаем начальный статус
                RecordingStatusText = "Готов к записи";
                RecordButtonText = "Записать";
                ResultText = "Здесь появится распознанный текст...";
                
                Log.Debug("Начальные значения MainPage установлены");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка установки начальных значений MainPage");
            }
        }

        /// <summary>
        /// Устанавливает имя устройства из конфига, получая человеческое имя
        /// </summary>
        private async void SetDeviceNameFromConfig()
        {
            try
            {
                Log.Information("=== SetDeviceNameFromConfig НАЧАЛО ===");
                Log.Information("_config.Audio.SelectedDeviceId = '{DeviceId}'", _config.Audio.SelectedDeviceId ?? "NULL");

                var selectedDeviceId = _config.Audio.SelectedDeviceId;
                if (string.IsNullOrEmpty(selectedDeviceId))
                {
                    Log.Warning("DeviceId пустой, устанавливаем 'Не выбрано'");

                    CurrentMicrophone = "Не выбран";
                    CurrentDeviceText = "Устройство: Не выбрано";
                    return;
                }

                Log.Information("Получаем список устройств...");

                // Получаем список доступных устройств
                var devices = await _audioService.GetAvailableDevicesAsync();
                Log.Information("Найдено {Count} устройств", devices?.Count() ?? 0);

                var selectedDevice = devices.FirstOrDefault(d => d.Id == selectedDeviceId);

                if (selectedDevice != null)
                {
                    // Показываем человеческое имя
                    CurrentMicrophone = selectedDevice.Name;
                    CurrentDeviceText = $"Устройство: {selectedDevice.Name}";
                    Log.Debug("Устройство найдено: {DeviceName}", selectedDevice.Name);
                }
                else
                {
                    // Устройство сохранено в конфиге, но не найдено в системе
                    CurrentMicrophone = "Недоступно";
                    CurrentDeviceText = $"Устройство: Недоступно ({selectedDeviceId})";
                    Log.Warning("Устройство из конфига не найдено: {DeviceId}", selectedDeviceId);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка получения имени устройства");
                CurrentMicrophone = "Ошибка";
                CurrentDeviceText = "Устройство: Ошибка получения";
            }
        }

        /// <summary>
        /// Метод для обновления информации об устройстве из конфига
        /// </summary>
        /// <summary>
        /// Метод для обновления информации об устройстве из актуальной конфигурации
        /// </summary>
        public async void UpdateDeviceFromConfig()
        {
            try
            {
                Log.Information("=== UpdateDeviceFromConfig НАЧАЛО ===");
        
                // ✅ ИСПОЛЬЗУЕМ АКТУАЛЬНУЮ КОНФИГУРАЦИЮ из ConfigurationService
                var currentConfig = _configurationService.CurrentConfig;
                var selectedDeviceId = currentConfig.Audio.SelectedDeviceId;
        
                Log.Information("Актуальный SelectedDeviceId из ConfigurationService = '{DeviceId}'", selectedDeviceId ?? "NULL");
        
                if (string.IsNullOrEmpty(selectedDeviceId))
                {
                    Log.Warning("DeviceId пустой, устанавливаем 'Не выбрано'");
                    CurrentMicrophone = "Не выбран";
                    CurrentDeviceText = "Устройство: Не выбрано";
                    return;
                }

                Log.Information("Получаем список устройств...");
                var devices = await _audioService.GetAvailableDevicesAsync();
                Log.Information("Найдено {Count} устройств", devices?.Count() ?? 0);
        
                var selectedDevice = devices.FirstOrDefault(d => d.Id == selectedDeviceId);

                if (selectedDevice != null)
                {
                    Log.Information("Устройство найдено: {DeviceName}", selectedDevice.Name);
                    CurrentMicrophone = selectedDevice.Name;
                    CurrentDeviceText = $"Устройство: {selectedDevice.Name}";
                }
                else
                {
                    Log.Warning("Устройство из конфига не найдено: {DeviceId}", selectedDeviceId);
                    CurrentMicrophone = "Недоступно";
                    CurrentDeviceText = $"Устройство: Недоступно ({selectedDeviceId})";
                }
        
                Log.Information("=== UpdateDeviceFromConfig РЕЗУЛЬТАТ: {DeviceText} ===", CurrentDeviceText);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка в UpdateDeviceFromConfig");
                CurrentMicrophone = "Ошибка";
                CurrentDeviceText = "Устройство: Ошибка получения";
            }
        }
        #endregion

        #region Event Subscription

        private void SubscribeToEvents()
        {
            try
            {
                // Подписываемся на изменения уровня микрофона
                _audioService.VolumeChanged += OnVolumeChanged;

                // Подписываемся на события записи
                _voiceRecordingService.StatusChanged += OnRecordingStatusChanged;
                _voiceRecordingService.RecognitionCompleted += OnRecognitionCompleted;
                
                // Подписываемся на изменения конфигурации
                _configurationService.ConfigurationChanged += OnConfigurationChanged;

                Log.Debug("События MainPage подписаны");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка подписки на события MainPage");
            }
        }

        private void UnsubscribeFromEvents()
        {
            try
            {
                // Отписываемся от событий аудио
                _audioService.VolumeChanged -= OnVolumeChanged;

                // Отписываемся от событий записи
                _voiceRecordingService.StatusChanged -= OnRecordingStatusChanged;
                _voiceRecordingService.RecognitionCompleted -= OnRecognitionCompleted;
                _configurationService.ConfigurationChanged -= OnConfigurationChanged;

                Log.Debug("События MainPage отписаны");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка отписки от событий MainPage");
            }
        }

        #endregion

        #region Event Handlers

        private void OnVolumeChanged(object? sender, float volume)
        {
            try
            {
                MicrophoneLevel = volume;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки изменения уровня микрофона");
            }
        }

        // Обработчик изменений конфигурации
        private void OnConfigurationChanged(object? sender, ConfigurationChangedEvent e)
        {
            try
            {
                Log.Information("MainPageViewModel получил уведомление об изменении конфигурации: {SettingName}", e.SettingName);
        
                // Обновляем отображение устройства при загрузке И при сохранении конфигурации
                if (e.SettingName == "ConfigurationLoaded" || e.SettingName == "ConfigurationSaved")
                {
                    Log.Information("Обновляем отображение микрофона после {EventType}", e.SettingName);
                    UpdateDeviceFromConfig();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки изменения конфигурации в MainPageViewModel");
            }
        }
        
        private void OnRecordingStatusChanged(object? sender, RecordingStatusChangedEvent e)
        {
            try
            {
                IsRecording = e.NewStatus == RecordingStatus.Recording;

                RecordingStatusText = e.NewStatus switch
                {
                    RecordingStatus.Idle => "Готов к записи",
                    RecordingStatus.Recording => "Идет запись...",
                    RecordingStatus.Processing => "Обработка...",
                    RecordingStatus.Error => $"Ошибка: {e.Reason}",
                    _ => "Неизвестный статус"
                };

                // Обновляем кнопку в зависимости от статуса
                RecordButtonText = e.NewStatus switch
                {
                    RecordingStatus.Recording => "Остановить",
                    RecordingStatus.Processing => "Обработка...",
                    _ => "Записать"
                };

                Log.Debug("Статус записи изменен на MainPage: {NewStatus}", e.NewStatus);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки изменения статуса записи");
            }
        }

        private void OnRecognitionCompleted(object? sender, VoiceRecognitionCompletedEvent e)
        {
            try
            {
                if (e.Result.Success && !string.IsNullOrEmpty(e.Result.RecognizedText))
                {
                    LastRecognizedText = e.Result.RecognizedText;
                    ResultText = e.Result.RecognizedText;
                    ResultTextBrush = System.Windows.Media.Brushes.White;
                    ResultFontStyle = System.Windows.FontStyles.Normal;
                    
                    AddToRecentRecognitions(e.Result.RecognizedText);
                    RecordingStatusText = "Распознавание завершено";
                    
                    // Добавляем информацию о точности и времени
                    ConfidenceText = "Точность: высокая";
                    ProcessingTimeText = "Время: < 1с";
                    
                    Log.Information("Распознавание завершено на MainPage: {Text}", e.Result.RecognizedText);
                }
                else
                {
                    RecordingStatusText = $"Ошибка распознавания: {e.Result.ErrorMessage}";
                    ResultText = "Не удалось распознать речь";
                    ResultTextBrush = System.Windows.Media.Brushes.Red;
                    ResultFontStyle = System.Windows.FontStyles.Italic;
                    
                    Log.Warning("Ошибка распознавания на MainPage: {Error}", e.Result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки завершения распознавания");
            }
        }

        #endregion

        #region Helper Methods

        private void AddToRecentRecognitions(string text)
        {
            try
            {
                // Добавляем в начало списка
                RecentRecognitions.Insert(0, text);

                // Ограничиваем количество записей
                while (RecentRecognitions.Count > 10)
                {
                    RecentRecognitions.RemoveAt(RecentRecognitions.Count - 1);
                }

                Log.Debug("Добавлен текст в недавние распознавания: {Text}", text);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка добавления текста в недавние распознавания");
            }
        }

        #endregion

        #region Cleanup

        public void Cleanup()
        {
            try
            {
                Log.Debug("Cleanup MainPageViewModel начат");

                UnsubscribeFromEvents();

                // Очищаем коллекции
                RecentRecognitions.Clear();

                Log.Information("Cleanup MainPageViewModel завершен");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при cleanup MainPageViewModel");
            }
        }

        #endregion
    }
}