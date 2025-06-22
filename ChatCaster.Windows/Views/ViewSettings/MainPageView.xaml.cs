using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.Windows.Services;

namespace ChatCaster.Windows.Views.ViewSettings
{
    public partial class MainPageView : Page
    {
        private readonly AudioCaptureService? _audioCaptureService;
        private readonly SpeechRecognitionService? _speechRecognitionService;
        private readonly ServiceContext? _serviceContext;
        private readonly OverlayService? _overlayService;
        private bool _isRecording = false;
        private readonly List<byte> _audioBuffer = new List<byte>();
        private System.Threading.Timer? _recordingTimer;

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
            _speechRecognitionService = speechRecognitionService;
            _serviceContext = serviceContext;
            _overlayService = overlayService;
            LoadCurrentDevice();
            
            // Подписываемся на события аудио
            if (_audioCaptureService != null)
            {
                _audioCaptureService.AudioDataReceived += OnAudioDataReceived;
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

        private void OnAudioDataReceived(object? sender, byte[] audioData)
        {
            // Только если еще записываем
            if (_isRecording)
            {
                _audioBuffer.AddRange(audioData);
                Console.WriteLine($"Получен аудио блок: {audioData.Length} байт, всего в буфере: {_audioBuffer.Count} байт");
            }
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecording)
            {
                await StopRecordingAsync();
            }
            else
            {
                await StartRecordingAsync();
            }
        }

        private readonly object _recordingLock = new object();
        private bool _isProcessingToggle = false;

        private async Task StartRecordingAsync()
        {
            // Защита от race condition
            lock (_recordingLock)
            {
                if (_isProcessingToggle || _isRecording)
                {
                    Console.WriteLine("StartRecording уже обрабатывается или запись идет, игнорируем...");
                    return;
                }
                _isProcessingToggle = true;
            }

            try
            {
                if (_audioCaptureService == null)
                {
                    ShowError("Сервис записи недоступен");
                    return;
                }

                _isRecording = true;
                _audioBuffer.Clear();
                UpdateRecordingButton("⏹️ Остановить", "RecordCircle24");
                UpdateRecordingStatus("Запись...", "#ff9800");
                ClearResults();
                
                // Показываем overlay если включен в настройках
                if (_serviceContext?.Config?.Overlay?.IsEnabled == true && _overlayService != null)
                {
                    Console.WriteLine("Показываем overlay - запись началась");
                    await _overlayService.ShowAsync(RecordingStatus.Recording);
                }                
                else
                {
                    Console.WriteLine("Overlay отключен в настройках или ServiceContext недоступен");
                }

                // Создаем конфигурацию аудио - используем настройки из конфигурации
                int maxSeconds = _serviceContext?.Config?.Audio?.MaxRecordingSeconds ?? 30;

                Console.WriteLine($"=== ДИАГНОСТИКА КОНФИГУРАЦИИ ===");
                Console.WriteLine($"ServiceContext: {(_serviceContext != null ? "есть" : "null")}");
                Console.WriteLine($"Config: {(_serviceContext?.Config != null ? "есть" : "null")}");
                Console.WriteLine($"Audio: {(_serviceContext?.Config?.Audio != null ? "есть" : "null")}");
                Console.WriteLine($"MaxRecordingSeconds из конфига: {_serviceContext?.Config?.Audio?.MaxRecordingSeconds}");
                Console.WriteLine($"Итоговое значение maxSeconds: {maxSeconds}");
                Console.WriteLine($"================================");

                var audioConfig = new AudioConfig
                {
                    SampleRate = 16000,
                    Channels = 1,
                    BitsPerSample = 16,
                    MaxRecordingSeconds = maxSeconds
                };
                
                Console.WriteLine($"Устанавливаем таймер на {maxSeconds} секунд ({maxSeconds * 1000} мс)");

                // Запускаем запись
                bool started = await _audioCaptureService.StartCaptureAsync(audioConfig);
                if (!started)
                {
                    ShowError("Не удалось запустить запись");
                    await StopRecordingAsync();
                    return;
                }

                // Автоматическая остановка через таймер
                _recordingTimer?.Dispose();
                _recordingTimer = new System.Threading.Timer(async _ =>
                {
                    Console.WriteLine($"⏰ ТАЙМЕР СРАБОТАЛ! Останавливаем запись после {maxSeconds} секунд");
                    if (_isRecording)
                    {
                        try
                        {
                            await Dispatcher.InvokeAsync(async () =>
                            {
                                await StopRecordingAsync();
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка автостопа: {ex.Message}");
                        }
                    }
                }, null, maxSeconds * 1000, Timeout.Infinite);
                Console.WriteLine("Запись началась");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка записи: {ex.Message}");
                await StopRecordingAsync();
            }
            finally
            {
                lock (_recordingLock)
                {
                    _isProcessingToggle = false;
                }
            }
        }

        /// <summary>
        /// Публичный метод для запуска записи через глобальный хоткей
        /// </summary>
        public async Task TriggerRecordingFromHotkey()
        {
            try
            {
                Console.WriteLine($"🎤 [MainPageView] TriggerRecordingFromHotkey вызван");
                Console.WriteLine($"📝 [MainPageView] Текущее состояние _isRecording: {_isRecording}");
        
                if (_isRecording)
                {
                    Console.WriteLine($"📝 [MainPageView] Запись идет, останавливаем...");
                    await StopRecordingAsync();
                }
                else
                {
                    Console.WriteLine($"📝 [MainPageView] Запись не идет, начинаем...");
                    await StartRecordingAsync();
                }
        
                Console.WriteLine($"🎤 [MainPageView] TriggerRecordingFromHotkey завершен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MainPageView] Ошибка в TriggerRecordingFromHotkey: {ex.Message}");
                ShowError($"Ошибка хоткея: {ex.Message}");
            }
        }
        
        private async Task StopRecordingAsync()
        {
            // Защита от повторного вызова
            if (!_isRecording) return;

            try
            {
                // Сразу сбрасываем флаг записи
                _isRecording = false;

                // Останавливаем таймер
                _recordingTimer?.Dispose();
                _recordingTimer = null;

                // Останавливаем захват аудио
                if (_audioCaptureService != null)
                {
                    await _audioCaptureService.StopCaptureAsync();
                }

                // Обрабатываем собранные аудио данные
                if (_audioBuffer.Count > 0 && _speechRecognitionService != null)
                {
                    UpdateRecordingStatus("Обработка...", "#2196f3");
                    
                    var startTime = DateTime.Now;
                    var audioData = _audioBuffer.ToArray();
                    
                    Console.WriteLine($"Отправляем на распознавание: {audioData.Length} байт");
                    var result = await _speechRecognitionService.RecognizeAsync(audioData);
                    var processingTime = DateTime.Now - startTime;

                    Console.WriteLine($"Результат распознавания: Success={result.Success}, Text='{result.RecognizedText}'");
                    ShowResult(result, processingTime);
                }
                else
                {
                    ShowError("Нет аудио данных для обработки");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка остановки записи: {ex.Message}");
                Console.WriteLine($"Ошибка в StopRecordingAsync: {ex.Message}");
            }
            finally
            {
                _isRecording = false;
                UpdateRecordingButton("🎙️ Записать", "Mic24");
                UpdateRecordingStatus("Готов к записи", "#4caf50");
                _audioBuffer.Clear();
            }
            
            // Скрываем overlay если был показан
            if (_serviceContext?.Config?.Overlay?.IsEnabled == true && _overlayService != null)
            {
                Console.WriteLine("Скрываем overlay - запись завершена");
                await _overlayService.HideAsync();
            }        
        }

        
        private void ShowResult(VoiceProcessingResult result, TimeSpan processingTime)
        {
            if (result.Success && !string.IsNullOrWhiteSpace(result.RecognizedText))
            {
                ResultText.Text = result.RecognizedText;
                ResultText.Foreground = Brushes.White;
                ResultText.FontStyle = FontStyles.Normal;

                ConfidenceText.Text = $"Уверенность: {result.Confidence:P0}";
                ProcessingTimeText.Text = $"Время: {processingTime.TotalMilliseconds:F0}мс";

                UpdateRecordingStatus("Готов к записи", "#4caf50");
                
                Console.WriteLine($"Успешно распознано: '{result.RecognizedText}'");
                // Показываем уведомление если включено
                
                if (_serviceContext?.Config?.System?.ShowNotifications == true)
                {
                    Console.WriteLine("Показываем уведомление о распознавании");
                    ShowNotification("ChatCaster", $"Распознано: {result.RecognizedText}");
                }
                else
                {
                    Console.WriteLine("Уведомления отключены в настройках");
                }

            }
            else
            {
                string errorMessage = result.ErrorMessage ?? "Не удалось распознать речь";
                ResultText.Text = errorMessage;
                ResultText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                ResultText.FontStyle = FontStyles.Italic;

                ConfidenceText.Text = "Уверенность: -";
                ProcessingTimeText.Text = $"Время: {processingTime.TotalMilliseconds:F0}мс";

                UpdateRecordingStatus("Готов к записи", "#4caf50");
                
                Console.WriteLine($"Ошибка распознавания: {errorMessage}");
            }
        }

        private void ShowNotification(string title, string message)
        {
            try
            {
                // Используем стандартные Windows уведомления
                var notification = new System.Windows.Forms.NotifyIcon();
                notification.Icon = System.Drawing.SystemIcons.Information;
                notification.Visible = true;
                notification.ShowBalloonTip(3000, title, message, System.Windows.Forms.ToolTipIcon.Info);
        
                // Убираем через 5 секунд
                Task.Delay(5000).ContinueWith(_ => notification.Dispose());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка показа уведомления: {ex.Message}");
            }
        }
        
        private void ShowError(string message)
        {
            ResultText.Text = message;
            ResultText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            ResultText.FontStyle = FontStyles.Italic;

            ConfidenceText.Text = "Уверенность: -";
            ProcessingTimeText.Text = "Время: -";

            UpdateRecordingStatus("Ошибка", "#f44336");
            
            Console.WriteLine($"Ошибка: {message}");
        }

        private void ClearResults()
        {
            ResultText.Text = "Обработка...";
            ResultText.Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153));
            ResultText.FontStyle = FontStyles.Italic;

            ConfidenceText.Text = "Уверенность: -";
            ProcessingTimeText.Text = "Время: -";
        }

        private void UpdateRecordingStatus(string status, string color)
        {
            RecordingStatusText.Text = status;
            RecordingStatusText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(color));
        }

        private void UpdateRecordingButton(string content, string iconSymbol)
        {
            RecordButton.Content = content;
            // TODO: Обновить иконку кнопки если нужно
        }

        // Публичные методы для обновления статуса извне
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

        // Cleanup при выгрузке страницы
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Отписываемся от событий
                if (_audioCaptureService != null)
                {
                    _audioCaptureService.AudioDataReceived -= OnAudioDataReceived;
                }

                // Останавливаем таймер
                _recordingTimer?.Dispose();
                _recordingTimer = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при выгрузке MainPageView: {ex.Message}");
            }
        }
    }
}