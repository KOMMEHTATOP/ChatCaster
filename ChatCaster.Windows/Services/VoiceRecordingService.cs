using ChatCaster.Core.Models;
using ChatCaster.Core.Events;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using Serilog;
using System.Timers;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Главный сервис управления записью голоса
/// Координирует работу AudioCaptureService и SpeechRecognitionService
/// </summary>
public class VoiceRecordingService : IVoiceRecordingService, IDisposable
{
    public event EventHandler<RecordingStatusChangedEvent>? StatusChanged;
    public event EventHandler<VoiceRecognitionCompletedEvent>? RecognitionCompleted;

    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ISpeechRecognitionService _speechRecognitionService;
    private readonly IConfigurationService _configurationService;

    private RecordingState _currentState = new RecordingState();
    private readonly List<byte> _recordingBuffer = new();
    private System.Timers.Timer? _recordingTimer;
    private readonly object _stateLock = new object();
    private bool _isDisposed = false;

    public RecordingState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }
        private set
        {
            lock (_stateLock)
            {
                if (_currentState.Status != value.Status)
                {
                    var oldStatus = _currentState.Status;
                    _currentState = value;
                    Log.Information($"🔄 Состояние записи: {oldStatus} → {value.Status}");

                    StatusChanged?.Invoke(this, new RecordingStatusChangedEvent
                    {
                        OldStatus = oldStatus, NewStatus = value.Status, Reason = null // Можно добавить причину позже
                    });
                }
            }
        }
    }

    public bool IsRecording => CurrentState.Status == RecordingStatus.Recording;

    public VoiceRecordingService(
        IAudioCaptureService audioCaptureService,
        ISpeechRecognitionService speechRecognitionService,
        IConfigurationService configurationService)
    {
        _audioCaptureService = audioCaptureService ?? throw new ArgumentNullException(nameof(audioCaptureService));
        _speechRecognitionService =
            speechRecognitionService ?? throw new ArgumentNullException(nameof(speechRecognitionService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

        // Подписываемся на события аудио захвата
        _audioCaptureService.AudioDataReceived += OnAudioDataReceived;
    }
    

    public async Task<bool> StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsRecording)
            {
                Log.Information("📝 Запись уже идет, игнорируем");
                return false;
            }

            Log.Information("🎤 Начинаем запись...");
            CurrentState = new RecordingState
            {
                Status = RecordingStatus.Recording, StartTime = DateTime.Now
            };

            // Очищаем буфер
            lock (_recordingBuffer)
            {
                _recordingBuffer.Clear();
            }

            // Получаем конфигурацию
            var config = _configurationService.CurrentConfig;
            var audioConfig = config.Audio;
            var maxSeconds = audioConfig.MaxRecordingSeconds;

            // Запускаем захват аудио если не запущен
            if (!_audioCaptureService.IsCapturing)
            {
                bool captureStarted = await _audioCaptureService.StartCaptureAsync(audioConfig);

                if (!captureStarted)
                {
                    Log.Information("❌ Не удалось запустить захват аудио");
                    CurrentState = new RecordingState
                    {
                        Status = RecordingStatus.Error, ErrorMessage = "Не удалось запустить захват аудио"
                    };
                    return false;
                }
            }

            // Устанавливаем таймер автоостановки
            _recordingTimer = new System.Timers.Timer(maxSeconds * 1000);
            _recordingTimer.Elapsed += OnRecordingTimerElapsed;
            _recordingTimer.AutoReset = false;
            _recordingTimer.Start();

            CurrentState = new RecordingState
            {
                Status = RecordingStatus.Recording, StartTime = DateTime.Now, Duration = TimeSpan.Zero
            };
            Log.Information($"✅ Запись началась (макс. {maxSeconds} сек)");

            return true;
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Ошибка начала записи: {ex.Message}");
            CurrentState = new RecordingState
            {
                Status = RecordingStatus.Error, ErrorMessage = ex.Message
            };
            return false;
        }
    }

    public async Task<VoiceProcessingResult> StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsRecording)
            {
                Log.Information("📝 Запись не идет, возвращаем пустой результат");
                return new VoiceProcessingResult
                {
                    Success = false, RecognizedText = "", ErrorMessage = "Запись не была активна"
                };
            }

            Log.Information("🛑 Останавливаем запись...");
            await _audioCaptureService.StopCaptureAsync();
            CurrentState = new RecordingState
            {
                Status = RecordingStatus.Processing
            };

            // Останавливаем таймер
            _recordingTimer?.Stop();
            _recordingTimer?.Dispose();
            _recordingTimer = null;

            // Получаем данные из буфера
            byte[] audioData;

            lock (_recordingBuffer)
            {
                audioData = _recordingBuffer.ToArray();
                _recordingBuffer.Clear();
            }

            Log.Information($"📤 Получено {audioData.Length} байт для распознавания");

            if (audioData.Length == 0)
            {
                Log.Information("❌ Нет аудио данных для распознавания");
                CurrentState = new RecordingState
                {
                    Status = RecordingStatus.Idle
                };
                return new VoiceProcessingResult
                {
                    Success = false, RecognizedText = "", ErrorMessage = "Нет аудио данных"
                };
            }

            // Отправляем на распознавание
            var result = await _speechRecognitionService.RecognizeAsync(audioData, cancellationToken);

            CurrentState = new RecordingState
            {
                Status = result.Success ? RecordingStatus.Completed : RecordingStatus.Error,
                LastRecognizedText = result.RecognizedText,
                ErrorMessage = result.ErrorMessage
            };

            // Уведомляем о завершении
            RecognitionCompleted?.Invoke(this, new VoiceRecognitionCompletedEvent
            {
                Result = result, AudioDataSize = audioData.Length
            });

            if (result.Success && !string.IsNullOrEmpty(result.RecognizedText))
            {
                Log.Information($"✅ Распознано: '{result.RecognizedText}'");
            }
            else
            {
                Log.Information($"❌ Распознавание не удалось: {result.ErrorMessage}");
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Ошибка остановки записи: {ex.Message}");
            CurrentState = new RecordingState
            {
                Status = RecordingStatus.Error, ErrorMessage = ex.Message
            };

            return new VoiceProcessingResult
            {
                Success = false, RecognizedText = "", ErrorMessage = ex.Message
            };
        }
    }

    public async Task CancelRecordingAsync()
    {
        try
        {
            if (!IsRecording)
            {
                return;
            }

            Log.Information("❌ Отменяем запись...");
            await _audioCaptureService.StopCaptureAsync();

            // Останавливаем таймер
            _recordingTimer?.Stop();
            _recordingTimer?.Dispose();
            _recordingTimer = null;

            // Очищаем буфер
            lock (_recordingBuffer)
            {
                _recordingBuffer.Clear();
            }

            CurrentState = new RecordingState
            {
                Status = RecordingStatus.Cancelled
            };
            Log.Information("✅ Запись отменена");
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Ошибка отмены записи: {ex.Message}");
            CurrentState = new RecordingState
            {
                Status = RecordingStatus.Error, ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> TestMicrophoneAsync()
    {
        try
        {
            Log.Information("🔍 Тестируем микрофон...");
            return await _audioCaptureService.TestMicrophoneAsync();
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Ошибка тестирования микрофона: {ex.Message}");
            return false;
        }
    }

    public async Task<VoiceProcessingResult> ProcessAudioDataAsync(byte[] audioData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Log.Information($"📤 Обрабатываем {audioData.Length} байт аудио данных");
            return await _speechRecognitionService.RecognizeAsync(audioData, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Ошибка обработки аудио: {ex.Message}");
            return new VoiceProcessingResult
            {
                Success = false, RecognizedText = "", ErrorMessage = ex.Message
            };
        }
    }

    private void OnAudioDataReceived(object? sender, byte[] audioData)
    {
        try
        {
            // Добавляем данные в буфер только если идет запись
            if (IsRecording)
            {
                lock (_recordingBuffer)
                {
                    _recordingBuffer.AddRange(audioData);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Ошибка обработки аудио данных: {ex.Message}");
        }
    }

    private async void OnRecordingTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            var config = _configurationService.CurrentConfig;
            var maxSeconds = config.Audio.MaxRecordingSeconds;
            Log.Information($"⏰ Время записи ({maxSeconds} сек) истекло, автоостановка");

            await StopRecordingAsync();
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Ошибка автоостановки записи: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _recordingTimer?.Stop();
            _recordingTimer?.Dispose();
            _recordingTimer = null;

            // Отписываемся от событий
            _audioCaptureService.AudioDataReceived -= OnAudioDataReceived;

            lock (_recordingBuffer)
            {
                _recordingBuffer.Clear();
            }

            _isDisposed = true;
        }
    }
}
