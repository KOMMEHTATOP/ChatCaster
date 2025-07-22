using ChatCaster.Core.Models;
using ChatCaster.Core.Events;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using Serilog;

namespace ChatCaster.Windows.Managers.VoiceRecording;

/// <summary>
/// Тонкий координатор записи голоса - делегирует всю работу менеджерам
/// </summary>
public class VoiceRecordingCoordinator : IVoiceRecordingService, IDisposable
{
    public event EventHandler<RecordingStatusChangedEvent>? StatusChanged;
    public event EventHandler<VoiceRecognitionCompletedEvent>? RecognitionCompleted;

    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ISpeechRecognitionService _speechRecognitionService;
    private readonly IConfigurationService _configurationService;

    private readonly RecordingStateManager _stateManager;
    private readonly AudioBufferManager _bufferManager;
    private readonly RecordingTimerManager _timerManager;

    private bool _isDisposed;

    public RecordingState CurrentState => _stateManager.CurrentState;
    public bool IsRecording => _stateManager.IsRecording;

    public VoiceRecordingCoordinator(
        IAudioCaptureService audioCaptureService,
        ISpeechRecognitionService speechRecognitionService,
        IConfigurationService configurationService)
    {
        _audioCaptureService = audioCaptureService ?? throw new ArgumentNullException(nameof(audioCaptureService));
        _speechRecognitionService =
            speechRecognitionService ?? throw new ArgumentNullException(nameof(speechRecognitionService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

        _stateManager = new RecordingStateManager();
        _bufferManager = new AudioBufferManager();
        _timerManager = new RecordingTimerManager();

        // Подписываемся на события менеджеров
        _stateManager.StatusChanged += (s, e) => StatusChanged?.Invoke(this, e);
        _timerManager.AutoStopTriggered += OnAutoStopTriggered;

        // Подписываемся на аудио данные
        _audioCaptureService.AudioDataReceived += OnAudioDataReceived;
    }

    public async Task<bool> StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsRecording)
            {
                return false;
            }

            Log.Information("🎤 Начинаем запись...");

            // Получаем конфигурацию
            var config = _configurationService.CurrentConfig;

            if (_configurationService.CurrentConfig.SpeechRecognition.EngineSettings.TryGetValue("ModelSize",
                    out var modelSize))
            {
                Log.Information($"модель распознавания {modelSize}");
            }
            else
            {
                Log.Information($"Модель распознавания {modelSize} не найдена в конфиге");
            }

            ;

            var audioConfig = config.Audio;
            var maxSeconds = audioConfig.MaxRecordingSeconds;

            // Запускаем захват аудио если не запущен
            if (!_audioCaptureService.IsCapturing)
            {
                bool captureStarted = await _audioCaptureService.StartCaptureAsync(audioConfig);

                if (!captureStarted)
                {
                    Log.Information("❌ Не удалось запустить захват аудио");
                    _stateManager.SetError("Не удалось запустить захват аудио");
                    return false;
                }
            }

            // Делегируем работу менеджерам
            _stateManager.StartRecording();
            _bufferManager.StartBuffering();
            _timerManager.StartAutoStopTimer(maxSeconds);

            Log.Information($"✅ Запись началась (макс. {maxSeconds} сек)");
            return true;
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Ошибка начала записи: {ex.Message}");
            _stateManager.SetError(ex.Message);
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

            // Останавливаем захват аудио
            await _audioCaptureService.StopCaptureAsync();

            // Переключаемся в обработку
            _stateManager.StartProcessing();

            // Останавливаем таймер
            _timerManager.StopTimer();

            // Получаем данные из буфера
            var audioData = _bufferManager.StopBufferingAndGetData();

            if (audioData.Length == 0)
            {
                Log.Information("❌ Нет аудио данных для распознавания");
                _stateManager.SetIdle();
                return new VoiceProcessingResult
                {
                    Success = false, RecognizedText = "", ErrorMessage = "Нет аудио данных"
                };
            }

            // Отправляем на распознавание
            Log.Information("📤 Получено {AudioSize} байт для распознавания", audioData.Length);

            // ДОБАВЛЕНА ПРОВЕРКА ИНИЦИАЛИЗАЦИИ:
            if (!_speechRecognitionService.IsInitialized)
            {
                Log.Warning("❌ Речевой сервис не инициализирован, попытка переинициализации...");

                var config = _configurationService.CurrentConfig;
                bool reinitialized = await _speechRecognitionService.InitializeAsync(config.SpeechRecognition);

                if (!reinitialized)
                {
                    Log.Error("❌ Не удалось переинициализировать речевой сервис");
                    _stateManager.SetError("Сервис распознавания речи не доступен");
                    return new VoiceProcessingResult
                    {
                        Success = false, RecognizedText = "", ErrorMessage = "Сервис распознавания речи не инициализирован"
                    };
                }

                Log.Information("✅ Речевой сервис успешно переинициализирован");
            }

            var result = await _speechRecognitionService.RecognizeAsync(audioData, cancellationToken);

            // Обновляем состояние с результатом
            _stateManager.CompleteRecording(result.RecognizedText, result.Success, result.ErrorMessage);

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
            _stateManager.SetError(ex.Message);

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

            // Останавливаем захват аудио
            await _audioCaptureService.StopCaptureAsync();

            // Делегируем отмену менеджерам
            _stateManager.CancelRecording();
            _bufferManager.CancelBuffering();
            _timerManager.StopTimer();

            Log.Information("✅ Запись отменена");
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Ошибка отмены записи: {ex.Message}");
            _stateManager.SetError(ex.Message);
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

    private async void OnAutoStopTriggered(object? sender, EventArgs e)
    {
        try
        {
            await StopRecordingAsync();
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Ошибка автоостановки записи: {ex.Message}");
        }
    }

    private void OnAudioDataReceived(object? sender, byte[] audioData)
    {
        _bufferManager.OnAudioDataReceived(audioData);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // Отписываемся от событий
            _audioCaptureService.AudioDataReceived -= OnAudioDataReceived;
            _timerManager.AutoStopTriggered -= OnAutoStopTriggered;

            // Освобождаем ресурсы менеджеров
            _timerManager?.Dispose();

            _isDisposed = true;
        }
    }
}
