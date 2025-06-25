using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels.Base;
using Serilog;

namespace ChatCaster.Windows.ViewModels
{
    public partial class MainPageViewModel : ViewModelBase
    {
        #region Private Services
        private readonly AudioCaptureService? _audioCaptureService;
        private readonly ServiceContext? _serviceContext;
        #endregion

        #region Observable Properties

        [ObservableProperty]
        private string _recordingStatusText = "Готов к записи";

        [ObservableProperty]
        private Brush _recordingStatusBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // #4caf50

        [ObservableProperty]
        private string _currentDeviceText = "Устройство: Загрузка...";

        [ObservableProperty]
        private string _recordButtonText = "🎙️ Записать";

        [ObservableProperty]
        private string _recordButtonIcon = "Mic24";

        [ObservableProperty]
        private string _resultText = "Здесь появится распознанный текст...";

        [ObservableProperty]
        private Brush _resultTextBrush = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // #666666

        [ObservableProperty]
        private FontStyle _resultFontStyle = FontStyles.Italic;

        [ObservableProperty]
        private string _confidenceText = "Уверенность: -";

        [ObservableProperty]
        private string _processingTimeText = "Время: -";

        [ObservableProperty]
        private bool _isRecording = false;

        #endregion

        #region Commands

        [RelayCommand]
        private async Task ToggleRecording()
        {
            try
            {
                var voiceService = _serviceContext?.VoiceRecordingService;
                if (voiceService == null)
                {
                    ShowError("Сервис записи недоступен");
                    return;
                }

                if (voiceService.IsRecording)
                {
                    Log.Debug("Останавливаем запись через VoiceRecordingService");
                    await voiceService.StopRecordingAsync();
                }
                else
                {
                    Log.Debug("Начинаем запись через VoiceRecordingService");
                    await voiceService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
                Log.Error(ex, "Ошибка в ToggleRecording");
            }
        }

        #endregion

        #region Constructor
        public MainPageViewModel(
            AudioCaptureService? audioCaptureService,
            ServiceContext? serviceContext)
        {
            _audioCaptureService = audioCaptureService;
            _serviceContext = serviceContext;

            // Подписываемся на события VoiceRecordingService
            if (serviceContext?.VoiceRecordingService != null)
            {
                serviceContext.VoiceRecordingService.StatusChanged += OnRecordingStatusChanged;
                serviceContext.VoiceRecordingService.RecognitionCompleted += OnRecognitionCompleted;
                Log.Debug("MainPageViewModel подписался на события VoiceRecordingService");
            }
            else
            {
                Log.Warning("VoiceRecordingService недоступен в MainPageViewModel");
            }

            // Загружаем информацию о текущем устройстве
            _ = LoadCurrentDeviceAsync();
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Обновление статуса подключения устройств
        /// </summary>
        public void UpdateConnectionStatus(bool isConnected)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (isConnected)
                {
                    UpdateRecordingStatus("Готов к записи", "#4caf50");
                }
                else
                {
                    UpdateRecordingStatus("Не подключен", "#f44336");
                }
            });
        }

        /// <summary>
        /// Cleanup при выгрузке
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // Отписываемся от событий VoiceRecordingService
                if (_serviceContext?.VoiceRecordingService != null)
                {
                    _serviceContext.VoiceRecordingService.StatusChanged -= OnRecordingStatusChanged;
                    _serviceContext.VoiceRecordingService.RecognitionCompleted -= OnRecognitionCompleted;
                }

                Log.Debug("MainPageViewModel cleanup завершен");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при cleanup MainPageViewModel");
            }
        }

        #endregion

        #region Private Methods

        private async Task LoadCurrentDeviceAsync()
        {
            try
            {
                if (_audioCaptureService != null)
                {
                    var devices = (await _audioCaptureService.GetAvailableDevicesAsync()).ToList();
                    var defaultDevice = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
                    
                    // Обновляем UI в UI потоке
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentDeviceText = $"Устройство: {defaultDevice?.Name ?? "Не найдено"}";
                    });
                    
                    Log.Debug("Загружено аудио устройство: {DeviceName}", defaultDevice?.Name ?? "Не найдено");
                }
                else
                {
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentDeviceText = "Устройство: Сервис недоступен";
                    });
                    
                    Log.Warning("AudioCaptureService недоступен при загрузке устройства");
                }
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    CurrentDeviceText = $"Ошибка: {ex.Message}";
                });
                
                Log.Error(ex, "Ошибка при загрузке аудио устройства");
            }
        }

        private void OnRecordingStatusChanged(object? sender, RecordingStatusChangedEvent e)
        {
            // КРИТИЧНО: обновляем UI только в UI потоке
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Log.Debug("MainPageViewModel получил событие StatusChanged: {Status}", e.NewStatus);
                
                switch (e.NewStatus)
                {
                    case RecordingStatus.Recording:
                        UpdateRecordingButton("⏹️ Остановить", "RecordCircle24");
                        UpdateRecordingStatus("Запись...", "#ff9800");
                        ClearResults();
                        IsRecording = true;
                        break;
                    
                    case RecordingStatus.Processing:
                        UpdateRecordingStatus("Обработка...", "#2196f3");
                        break;
                    
                    case RecordingStatus.Completed:
                    case RecordingStatus.Idle:
                        UpdateRecordingButton("🎙️ Записать", "Mic24");
                        UpdateRecordingStatus("Готов к записи", "#4caf50");
                        IsRecording = false;
                        break;
                    
                    case RecordingStatus.Error:
                    case RecordingStatus.Cancelled:
                        UpdateRecordingButton("🎙️ Записать", "Mic24");
                        UpdateRecordingStatus("Ошибка", "#f44336");
                        IsRecording = false;
                        break;
                }
            });
        }

        private void OnRecognitionCompleted(object? sender, VoiceRecognitionCompletedEvent e)
        {
            // КРИТИЧНО: обновляем UI только в UI потоке
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Log.Debug("MainPageViewModel получил событие RecognitionCompleted");
                
                var result = e.Result;
                
                if (result.Success && !string.IsNullOrWhiteSpace(result.RecognizedText))
                {
                    ResultText = result.RecognizedText;
                    ResultTextBrush = Brushes.White;
                    ResultFontStyle = FontStyles.Normal;

                    ConfidenceText = $"Уверенность: {result.Confidence:P0}";
                    ProcessingTimeText = $"Время: {result.ProcessingTime.TotalMilliseconds:F0}мс";

                    Log.Information("Распознавание завершено: '{Text}' (уверенность: {Confidence:P0}, время: {ProcessingTime}мс)", 
                        result.RecognizedText, result.Confidence, result.ProcessingTime.TotalMilliseconds);
                }
                else
                {
                    string errorMessage = result.ErrorMessage ?? "Не удалось распознать речь";
                    ShowError(errorMessage);
                    Log.Warning("Распознавание не удалось: {Error}", errorMessage);
                }
            });
        }

        private void ShowError(string message)
        {
            // Эта функция уже вызывается из UI потока, но для безопасности добавим проверку
            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                ShowErrorInternal(message);
            }
            else
            {
                Application.Current?.Dispatcher.InvokeAsync(() => ShowErrorInternal(message));
            }
        }

        private void ShowErrorInternal(string message)
        {
            ResultText = message;
            ResultTextBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // #f44336
            ResultFontStyle = FontStyles.Italic;

            ConfidenceText = "Уверенность: -";
            ProcessingTimeText = "Время: -";

            UpdateRecordingStatus("Ошибка", "#f44336");
            
            Log.Warning("Отображена ошибка: {Message}", message);
        }

        private void ClearResults()
        {
            ResultText = "Обработка...";
            ResultTextBrush = new SolidColorBrush(Color.FromRgb(153, 153, 153)); // #999999
            ResultFontStyle = FontStyles.Italic;

            ConfidenceText = "Уверенность: -";
            ProcessingTimeText = "Время: -";
        }

        private void UpdateRecordingStatus(string status, string colorHex)
        {
            RecordingStatusText = status;
            RecordingStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        }

        private void UpdateRecordingButton(string content, string iconSymbol)
        {
            RecordButtonText = content;
            RecordButtonIcon = iconSymbol;
        }

        #endregion
    }
}