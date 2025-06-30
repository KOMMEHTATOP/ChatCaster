using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.SpeechRecognition.Whisper.Exceptions;
using ChatCaster.SpeechRecognition.Whisper.Models;
using ChatCaster.SpeechRecognition.Whisper.Utils;
using Microsoft.Extensions.Logging;
using Whisper.net;
using Whisper.net.Ggml;

namespace ChatCaster.SpeechRecognition.Whisper.Services;

/// <summary>
/// Реализация ISpeechRecognitionService для Whisper движка
/// </summary>
public class WhisperSpeechRecognitionService : ISpeechRecognitionService, IDisposable
{
    private readonly ILogger<WhisperSpeechRecognitionService> _logger;
    private readonly WhisperModelManager _modelManager;
    private readonly WhisperAudioProcessor _audioProcessor;
    private readonly AudioConverter _audioConverter;
    
    private WhisperConfig _config;
    private WhisperProcessor? _processor;
    private WhisperFactory? _factory;
    private bool _isInitialized;
    private bool _disposed;
    
    // События из интерфейса
    public event EventHandler<SpeechRecognitionProgressEvent>? RecognitionProgress;
    public event EventHandler<SpeechRecognitionErrorEvent>? RecognitionError;

    public WhisperSpeechRecognitionService(
        ILogger<WhisperSpeechRecognitionService> logger,
        WhisperModelManager modelManager,
        WhisperAudioProcessor audioProcessor,
        AudioConverter audioConverter,
        WhisperConfig config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _audioProcessor = audioProcessor ?? throw new ArgumentNullException(nameof(audioProcessor));
        _audioConverter = audioConverter ?? throw new ArgumentNullException(nameof(audioConverter));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    #region ISpeechRecognitionService Implementation

    public bool IsInitialized => _isInitialized && _processor != null;
    public string EngineName => WhisperConstants.EngineName;
    public string EngineVersion => WhisperConstants.EngineVersion;

    public async Task<bool> InitializeAsync(SpeechRecognitionConfig config)
    {
        try
        {
            _logger.LogInformation("Initializing Whisper speech recognition engine");

            // Конвертируем конфигурацию
            _config = WhisperConfig.FromSpeechConfig(config);
            
            // Валидируем конфигурацию
            var validation = _config.Validate();
            if (!validation.IsValid)
            {
                var errors = string.Join(", ", validation.Errors);
                throw WhisperConfigurationException.InvalidConfiguration(errors);
            }

            // Логируем предупреждения
            foreach (var warning in validation.Warnings)
            {
                _logger.LogWarning("Configuration warning: {Warning}", warning);
            }

            // Подготавливаем модель
            var modelPath = await _modelManager.PrepareModelAsync(_config.ModelSize, _config.ModelPath);
            
            // Создаем Whisper factory
            _factory = WhisperFactory.FromPath(modelPath);
            
            // Создаем процессор с настройками
            var processorBuilder = _factory.CreateBuilder()
                .WithLanguage(_config.Language == WhisperConstants.Languages.Auto ? null : _config.Language)
                .WithThreads(_config.ThreadCount);
            
            // Применяем дополнительные настройки
            if (_config.EnableTranslation)
            {
                processorBuilder = processorBuilder.WithTranslate();
            }
            
            if (!string.IsNullOrEmpty(_config.InitialPrompt))
            {
                processorBuilder = processorBuilder.WithPrompt(_config.InitialPrompt);
            }

            // Создаем процессор
            _processor = processorBuilder.Build();
            
            _isInitialized = true;
            _logger.LogInformation("Whisper engine initialized successfully with model: {ModelSize}", _config.ModelSize);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Whisper engine");
            _isInitialized = false;
            
            // Уведомляем об ошибке
            OnRecognitionError(new SpeechRecognitionErrorEvent
            {
                Engine = EngineName,
                ErrorMessage = ex.Message,
                Exception = ex
            });
            
            return false;
        }
    }

    public async Task<VoiceProcessingResult> RecognizeAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            throw new WhisperInitializationException("Whisper engine is not initialized");
        }

        if (audioData == null || audioData.Length == 0)
        {
            throw WhisperAudioException.EmptyData();
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Starting speech recognition, audio size: {Size} bytes", audioData.Length);
            
            // Уведомляем о начале обработки
            OnRecognitionProgress(new SpeechRecognitionProgressEvent
            {
                Engine = EngineName,
                ProgressPercentage = 10,
                Status = "Converting audio format"
            });

            // Конвертируем аудио в формат Whisper
            var samples = await _audioConverter.ConvertToSamplesAsync(audioData, cancellationToken);
            
            OnRecognitionProgress(new SpeechRecognitionProgressEvent
            {
                Engine = EngineName,
                ProgressPercentage = 30,
                Status = "Processing with Whisper"
            });

            // Выполняем распознавание
            var whisperResult = await ProcessWithWhisperAsync(samples, cancellationToken);
            
            OnRecognitionProgress(new SpeechRecognitionProgressEvent
            {
                Engine = EngineName,
                ProgressPercentage = 90,
                Status = "Finalizing results"
            });

            // Добавляем метаданные
            whisperResult.AudioSizeBytes = audioData.Length;
            whisperResult.AudioDurationMs = (int)(samples.Length / (float)WhisperConstants.Audio.RequiredSampleRate * 1000);
            whisperResult.ModelUsed = _config.ModelSize;
            whisperResult.ProcessingTime = stopwatch.Elapsed;

            var result = whisperResult.ToVoiceProcessingResult();
            
            OnRecognitionProgress(new SpeechRecognitionProgressEvent
            {
                Engine = EngineName,
                ProgressPercentage = 100,
                Status = "Completed"
            });

            _logger.LogInformation("Speech recognition completed: {Text} (confidence: {Confidence:F2}, time: {Time}ms)", 
                result.RecognizedText, result.Confidence, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Speech recognition was cancelled");
            return new VoiceProcessingResult
            {
                Success = false,
                ErrorMessage = "Recognition was cancelled",
                ProcessingTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Speech recognition failed");
            
            OnRecognitionError(new SpeechRecognitionErrorEvent
            {
                Engine = EngineName,
                ErrorMessage = ex.Message,
                Exception = ex
            });

            return new VoiceProcessingResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed
            };
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    public async Task<bool> ReloadConfigAsync(SpeechRecognitionConfig config)
    {
        try
        {
            _logger.LogInformation("Reloading Whisper configuration");
            
            // Сохраняем старую конфигурацию
            var oldConfig = _config.Clone();
            
            // Применяем новую
            var newConfig = WhisperConfig.FromSpeechConfig(config);
            
            // Проверяем нужна ли полная реинициализация
            bool needsReinitialization = 
                oldConfig.ModelSize != newConfig.ModelSize ||
                oldConfig.ModelPath != newConfig.ModelPath ||
                oldConfig.EnableGpu != newConfig.EnableGpu;

            if (needsReinitialization)
            {
                _logger.LogInformation("Configuration change requires reinitialization");
                await DisposeProcessorAsync();
                return await InitializeAsync(config);
            }
            else
            {
                // Применяем изменения без полной реинициализации
                _config = newConfig;
                _logger.LogInformation("Configuration reloaded without reinitialization");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload configuration");
            return false;
        }
    }

    public async Task<SpeechEngineCapabilities> GetCapabilitiesAsync()
    {
        await Task.CompletedTask; // Метод синхронный, но интерфейс асинхронный
        
        return new SpeechEngineCapabilities
        {
            SupportsLanguageAutoDetection = true,
            SupportsGpuAcceleration = true, // Whisper.net поддерживает GPU
            SupportsRealTimeProcessing = false, // Whisper работает с полными аудио фрагментами
            RequiresInternetConnection = false, // Локальная обработка
            SupportedSampleRates = new[] { WhisperConstants.Audio.RequiredSampleRate },
            MinAudioDurationMs = WhisperConstants.Audio.MinAudioLengthMs,
            MaxAudioDurationMs = WhisperConstants.Audio.MaxAudioLengthMs
        };
    }

    public async Task<IEnumerable<string>> GetSupportedLanguagesAsync()
    {
        await Task.CompletedTask; // Метод синхронный, но интерфейс асинхронный
        return WhisperConstants.Languages.Supported;
    }

    #endregion

    #region Private Methods

    private async Task<WhisperResult> ProcessWithWhisperAsync(float[] samples, CancellationToken cancellationToken)
    {
        if (_processor == null)
        {
            throw new WhisperInitializationException("Whisper processor is not initialized");
        }

        try
        {
            // Создаем таск с таймаутом
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_config.RecognitionTimeoutSeconds));

            var whisperResult = new WhisperResult();
            var segments = new List<WhisperSegment>();

            // Обрабатываем аудио
            await foreach (var segment in _processor.ProcessAsync(samples, timeoutCts.Token))
            {
                var whisperSegment = new WhisperSegment
                {
                    Text = segment.Text,
                    StartTime = (float)segment.Start.TotalSeconds,
                    EndTime = (float)segment.End.TotalSeconds,
                    Confidence = CalculateConfidence(segment)
                };

                segments.Add(whisperSegment);
                whisperResult.Text += segment.Text;
            }

            whisperResult.Segments = segments;
            whisperResult.Confidence = CalculateOverallConfidence(segments);
            whisperResult.VadUsed = _config.UseVAD;
            whisperResult.SilenceDetected = string.IsNullOrWhiteSpace(whisperResult.Text);

            return whisperResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Пробрасываем отмену от пользователя
        }
        catch (OperationCanceledException)
        {
            throw WhisperRecognitionException.Timeout(TimeSpan.FromSeconds(_config.RecognitionTimeoutSeconds));
        }
        catch (Exception ex)
        {
            throw WhisperRecognitionException.ProcessingFailed(ex, TimeSpan.Zero);
        }
    }

    private float CalculateConfidence(SegmentData segment)
    {
        // Базовый расчет уверенности на основе данных Whisper
        // В реальной реализации можно использовать более сложные алгоритмы
        return 0.8f; // Placeholder - Whisper.net не всегда предоставляет точные данные о confidence
    }

    private float CalculateOverallConfidence(List<WhisperSegment> segments)
    {
        if (segments.Count == 0)
            return 0.0f;

        return segments.Average(s => s.Confidence);
    }

    private void OnRecognitionProgress(SpeechRecognitionProgressEvent eventArgs)
    {
        RecognitionProgress?.Invoke(this, eventArgs);
    }

    private void OnRecognitionError(SpeechRecognitionErrorEvent eventArgs)
    {
        RecognitionError?.Invoke(this, eventArgs);
    }

    private async Task DisposeProcessorAsync()
    {
        if (_processor != null)
        {
            _processor.Dispose();
            _processor = null;
        }

        if (_factory != null)
        {
            _factory.Dispose();
            _factory = null;
        }

        _isInitialized = false;
        await Task.CompletedTask;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                DisposeProcessorAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Whisper processor");
            }

            _disposed = true;
        }
    }

    #endregion
}