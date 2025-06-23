using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels.Base;

namespace ChatCaster.Windows.ViewModels
{
    public partial class MainPageViewModel : ViewModelBase
    {
        #region Private Services
        private readonly AudioCaptureService? _audioCaptureService;
        private readonly ServiceContext? _serviceContext;
        private readonly OverlayService? _overlayService;
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
        private System.Windows.FontStyle _resultFontStyle = System.Windows.FontStyles.Italic;

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
                    Console.WriteLine("🛑 [MainPageViewModel] Останавливаем запись через VoiceRecordingService");
                    await voiceService.StopRecordingAsync();
                }
                else
                {
                    Console.WriteLine("🎤 [MainPageViewModel] Начинаем запись через VoiceRecordingService");
                    await voiceService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
                Console.WriteLine($"❌ [MainPageViewModel] Ошибка в ToggleRecording: {ex.Message}");
            }
        }

        #endregion

        #region Constructor
        public MainPageViewModel(
            AudioCaptureService? audioCaptureService,
            ServiceContext? serviceContext,
            OverlayService? overlayService)
        {
            _audioCaptureService = audioCaptureService;
            _serviceContext = serviceContext;
            _overlayService = overlayService;

            // Подписываемся на события VoiceRecordingService
            if (serviceContext?.VoiceRecordingService != null)
            {
                serviceContext.VoiceRecordingService.StatusChanged += OnRecordingStatusChanged;
                serviceContext.VoiceRecordingService.RecognitionCompleted += OnRecognitionCompleted;
            }

            // Загружаем информацию о текущем устройстве
            _ = LoadCurrentDeviceAsync();
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Метод для вызова записи через хоткей
        /// </summary>
        public async Task TriggerRecordingFromHotkeyAsync()
        {
            try
            {
                Console.WriteLine($"🎤 [MainPageViewModel] TriggerRecordingFromHotkey - переадресация к VoiceRecordingService");
                
                var voiceService = _serviceContext?.VoiceRecordingService;
                if (voiceService == null)
                {
                    Console.WriteLine($"❌ [MainPageViewModel] VoiceRecordingService недоступен");
                    ShowError("Сервис записи недоступен");
                    return;
                }

                if (voiceService.IsRecording)
                {
                    Console.WriteLine($"📝 [MainPageViewModel] Останавливаем запись через сервис");
                    await voiceService.StopRecordingAsync();
                }
                else
                {
                    Console.WriteLine($"📝 [MainPageViewModel] Начинаем запись через сервис");
                    await voiceService.StartRecordingAsync();
                }

                Console.WriteLine($"🎤 [MainPageViewModel] TriggerRecordingFromHotkey завершен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MainPageViewModel] Ошибка в TriggerRecordingFromHotkey: {ex.Message}");
                ShowError($"Ошибка хоткея: {ex.Message}");
            }
        }

        /// <summary>
        /// Обновление статуса подключения устройств
        /// </summary>
        public void UpdateConnectionStatus(bool isConnected)
        {
            if (isConnected)
            {
                UpdateRecordingStatus("Готов к записи", "#4caf50");
            }
            else
            {
                UpdateRecordingStatus("Не подключен", "#f44336");
            }
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

                Console.WriteLine($"🧹 [MainPageViewModel] Очистка завершена");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MainPageViewModel] Ошибка при cleanup: {ex.Message}");
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
                    var devices = await _audioCaptureService.GetAvailableDevicesAsync();
                    var defaultDevice = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
                    CurrentDeviceText = $"Устройство: {defaultDevice?.Name ?? "Не найдено"}";
                }
                else
                {
                    CurrentDeviceText = "Устройство: Сервис недоступен";
                }
            }
            catch (Exception ex)
            {
                CurrentDeviceText = $"Ошибка: {ex.Message}";
            }
        }

        private void OnRecordingStatusChanged(object? sender, RecordingStatusChangedEvent e)
        {
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
        }

        private void OnRecognitionCompleted(object? sender, VoiceRecognitionCompletedEvent e)
        {
            var result = e.Result;
            
            if (result.Success && !string.IsNullOrWhiteSpace(result.RecognizedText))
            {
                ResultText = result.RecognizedText;
                ResultTextBrush = Brushes.White;
                ResultFontStyle = System.Windows.FontStyles.Normal;

                ConfidenceText = $"Уверенность: {result.Confidence:P0}";
                ProcessingTimeText = $"Время: {result.ProcessingTime.TotalMilliseconds:F0}мс";

                Console.WriteLine($"✅ [MainPageViewModel] Отображаем результат: '{result.RecognizedText}'");
            }
            else
            {
                string errorMessage = result.ErrorMessage ?? "Не удалось распознать речь";
                ShowError(errorMessage);
            }
        }

        private void ShowError(string message)
        {
            ResultText = message;
            ResultTextBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // #f44336
            ResultFontStyle = System.Windows.FontStyles.Italic;

            ConfidenceText = "Уверенность: -";
            ProcessingTimeText = "Время: -";

            UpdateRecordingStatus("Ошибка", "#f44336");
            
            Console.WriteLine($"❌ [MainPageViewModel] Ошибка: {message}");
        }

        private void ClearResults()
        {
            ResultText = "Обработка...";
            ResultTextBrush = new SolidColorBrush(Color.FromRgb(153, 153, 153)); // #999999
            ResultFontStyle = System.Windows.FontStyles.Italic;

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