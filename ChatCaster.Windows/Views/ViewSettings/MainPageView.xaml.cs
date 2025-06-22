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
        private readonly OverlayService? _overlayService;

        public MainPageView()
        {
            InitializeComponent();
            UpdateRecordingStatus("Готов к записи", "#4caf50");
        }

        // Конструктор с сервисами
        public MainPageView(AudioCaptureService audioCaptureService, 
                           SpeechRecognitionService speechRecognitionService, 
                           ServiceContext serviceContext, OverlayService overlayService) : this()
        {
            _audioCaptureService = audioCaptureService;
            _serviceContext = serviceContext;
            _overlayService = overlayService;
            
            LoadCurrentDevice();
            
            // НОВОЕ: Подписываемся на события VoiceRecordingService
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

        // НОВОЕ: Обработчик изменения состояния записи
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

        // НОВОЕ: Обработчик завершения распознавания
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

                    Console.WriteLine($"✅ [MainPageView] Отображаем результат: '{result.RecognizedText}'");
                    
                    // Показываем уведомление если включено (через TrayService будет показано)
                }
                else
                {
                    string errorMessage = result.ErrorMessage ?? "Не удалось распознать речь";
                    ShowError(errorMessage);
                }
            });
        }

        // УПРОЩЕННЫЙ: Кнопка теперь работает через VoiceRecordingService
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
                    Console.WriteLine("🛑 [MainPageView] Останавливаем запись через VoiceRecordingService");
                    await voiceService.StopRecordingAsync();
                }
                else
                {
                    Console.WriteLine("🎤 [MainPageView] Начинаем запись через VoiceRecordingService");
                    await voiceService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
                Console.WriteLine($"❌ [MainPageView] Ошибка в RecordButton_Click: {ex.Message}");
            }
        }

        /// <summary>
        /// НОВЫЙ: Метод для хоткея - теперь работает через VoiceRecordingService
        /// </summary>
        public async Task TriggerRecordingFromHotkey()
        {
            try
            {
                Console.WriteLine($"🎤 [MainPageView] TriggerRecordingFromHotkey - переадресация к VoiceRecordingService");
                
                var voiceService = _serviceContext?.VoiceRecordingService;
                if (voiceService == null)
                {
                    Console.WriteLine($"❌ [MainPageView] VoiceRecordingService недоступен");
                    ShowError("Сервис записи недоступен");
                    return;
                }

                if (voiceService.IsRecording)
                {
                    Console.WriteLine($"📝 [MainPageView] Останавливаем запись через сервис");
                    await voiceService.StopRecordingAsync();
                }
                else
                {
                    Console.WriteLine($"📝 [MainPageView] Начинаем запись через сервис");
                    await voiceService.StartRecordingAsync();
                }

                Console.WriteLine($"🎤 [MainPageView] TriggerRecordingFromHotkey завершен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MainPageView] Ошибка в TriggerRecordingFromHotkey: {ex.Message}");
                ShowError($"Ошибка хоткея: {ex.Message}");
            }
        }

        // ТОЛЬКО UI МЕТОДЫ - никакой бизнес-логики
        public void ShowError(string message)
        {
            ResultText.Text = message;
            ResultText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            ResultText.FontStyle = FontStyles.Italic;

            ConfidenceText.Text = "Уверенность: -";
            ProcessingTimeText.Text = "Время: -";

            UpdateRecordingStatus("Ошибка", "#f44336");
            
            Console.WriteLine($"❌ [MainPageView] Ошибка: {message}");
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
            // TODO: Обновить иконку кнопки если нужно
        }

        // Методы для внешнего управления UI
        public void UpdateDeviceStatus(string deviceName)
        {
            CurrentDeviceText.Text = $"Устройство: {deviceName}";
        }

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

        // НОВЫЕ методы для обновления из ChatCasterWindow
        public void UpdateRecordingState(bool isRecording)
        {
            if (isRecording)
            {
                UpdateRecordingButton("⏹️ Остановить", "RecordCircle24");
                UpdateRecordingStatus("Запись...", "#ff9800");
                ClearResults();
            }
            else
            {
                UpdateRecordingButton("🎙️ Записать", "Mic24");
                UpdateRecordingStatus("Готов к записи", "#4caf50");
            }
        }

        public void UpdateRecognizedText(string recognizedText)
        {
            if (!string.IsNullOrEmpty(recognizedText))
            {
                ResultText.Text = recognizedText;
                ResultText.Foreground = Brushes.White;
                ResultText.FontStyle = FontStyles.Normal;
                
                Console.WriteLine($"📱 [MainPageView] UI обновлен с текстом: '{recognizedText}'");
            }
        }

        // Cleanup при выгрузке страницы
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

                Console.WriteLine($"🧹 [MainPageView] Очистка завершена");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MainPageView] Ошибка при выгрузке: {ex.Message}");
            }
        }
    }
}