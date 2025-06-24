using ChatCaster.Core.Events;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;

namespace ChatCaster.Windows.Views.ViewSettings
{
    public partial class MainPageView : Page
    {
        private readonly AudioCaptureService? _audioCaptureService;
        private readonly ServiceContext? _serviceContext;

        public MainPageView()
        {
            InitializeComponent();
            UpdateRecordingStatus("Готов к записи", "#4caf50");
        }

        // Конструктор с сервисами
        public MainPageView(AudioCaptureService audioCaptureService, ServiceContext serviceContext) : this()
        {
            _audioCaptureService = audioCaptureService;
            _serviceContext = serviceContext;
            
            LoadCurrentDevice();
            
            // Подписываемся на события VoiceRecordingService
            if (serviceContext?.VoiceRecordingService != null)
            {
                serviceContext.VoiceRecordingService.StatusChanged += OnRecordingStatusChanged;
                serviceContext.VoiceRecordingService.RecognitionCompleted += OnRecognitionCompleted;
            }
        }

        private async void LoadCurrentDevice()
        {
            try
            {
                if (_audioCaptureService != null)
                {
                    var devices = await _audioCaptureService.GetAvailableDevicesAsync();
                    var defaultDevice = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
                    CurrentDeviceText.Text = $"Устройство: {defaultDevice?.Name ?? "Не найдено"}";
                }
                else
                {
                    CurrentDeviceText.Text = "Устройство: Сервис недоступен";
                }
            }
            catch (Exception ex)
            {
                CurrentDeviceText.Text = $"Ошибка: {ex.Message}";
            }
        }

        // Обработчик изменения состояния записи
        private void OnRecordingStatusChanged(object? sender, RecordingStatusChangedEvent e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                switch (e.NewStatus)
                {
                    case RecordingStatus.Recording:
                        UpdateRecordingButton("⏹️ Остановить", "RecordCircle24");
                        UpdateRecordingStatus("Запись...", "#ff9800");
                        ClearResults();
                        break;
                    
                    case RecordingStatus.Processing:
                        UpdateRecordingStatus("Обработка...", "#2196f3");
                        break;
                    
                    case RecordingStatus.Completed:
                    case RecordingStatus.Idle:
                        UpdateRecordingButton("🎙️ Записать", "Mic24");
                        UpdateRecordingStatus("Готов к записи", "#4caf50");
                        break;
                    
                    case RecordingStatus.Error:
                    case RecordingStatus.Cancelled:
                        UpdateRecordingButton("🎙️ Записать", "Mic24");
                        UpdateRecordingStatus("Ошибка", "#f44336");
                        break;
                }
            });
        }

        // Обработчик завершения распознавания
        private void OnRecognitionCompleted(object? sender, VoiceRecognitionCompletedEvent e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                var result = e.Result;
                
                if (result.Success && !string.IsNullOrWhiteSpace(result.RecognizedText))
                {
                    ResultText.Text = result.RecognizedText;
                    ResultText.Foreground = Brushes.White;
                    ResultText.FontStyle = FontStyles.Normal;

                    ConfidenceText.Text = $"Уверенность: {result.Confidence:P0}";
                    ProcessingTimeText.Text = $"Время: {result.ProcessingTime.TotalMilliseconds:F0}мс";
                }
                else
                {
                    string errorMessage = result.ErrorMessage ?? "Не удалось распознать речь";
                    ShowError(errorMessage);
                }
            });
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
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
                    await voiceService.StopRecordingAsync();
                }
                else
                {
                    await voiceService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
            }
        }

        /// <summary>
        /// Метод для хоткея - работает через VoiceRecordingService
        /// </summary>
        public async Task TriggerRecordingFromHotkey()
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
                    await voiceService.StopRecordingAsync();
                }
                else
                {
                    await voiceService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка хоткея: {ex.Message}");
            }
        }

        // ===== ВНУТРЕННИЕ HELPER МЕТОДЫ =====

        public void ShowError(string message)
        {
            ResultText.Text = message;
            ResultText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            ResultText.FontStyle = FontStyles.Italic;

            ConfidenceText.Text = "Уверенность: -";
            ProcessingTimeText.Text = "Время: -";

            UpdateRecordingStatus("Ошибка", "#f44336");
        }

        public void ClearResults()
        {
            ResultText.Text = "Обработка...";
            ResultText.Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153));
            ResultText.FontStyle = FontStyles.Italic;

            ConfidenceText.Text = "Уверенность: -";
            ProcessingTimeText.Text = "Время: -";
        }

        public void UpdateRecordingStatus(string status, string color)
        {
            RecordingStatusText.Text = status;
            RecordingStatusText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(color));
        }

        public void UpdateRecordingButton(string content, string iconSymbol)
        {
            RecordButton.Content = content;
            // Обновить иконку кнопки если нужно
        }

        // ===== CLEANUP - КРИТИЧЕСКИ ВАЖЕН! =====
        
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Отписываемся от событий VoiceRecordingService
                if (_serviceContext?.VoiceRecordingService != null)
                {
                    _serviceContext.VoiceRecordingService.StatusChanged -= OnRecordingStatusChanged;
                    _serviceContext.VoiceRecordingService.RecognitionCompleted -= OnRecognitionCompleted;
                }
            }
            catch (Exception ex)
            {
                // Здесь можно оставить Console.WriteLine для критических ошибок cleanup
                // или заменить на ваш логгер когда внедрите
                System.Diagnostics.Debug.WriteLine($"❌ [MainPageView] Ошибка при выгрузке: {ex.Message}");
            }
        }
    }
}