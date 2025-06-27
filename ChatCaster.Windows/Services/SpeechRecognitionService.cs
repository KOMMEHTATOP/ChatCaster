using ChatCaster.Core.Events;
using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using System.IO;
using System.Net.Http;
using System.Diagnostics;
using Whisper.net;
using Whisper.net.Ggml;
using Serilog;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Реализация распознавания речи через Whisper.net
/// </summary>
public class SpeechRecognitionService : ISpeechRecognitionService, IDisposable
{
    private WhisperFactory? _whisperFactory;
    private WhisperProcessor? _processor;
    private bool _isDisposed;
    public event EventHandler<ModelDownloadProgressEvent>? DownloadProgress;
    public event EventHandler<ModelDownloadCompletedEvent>? DownloadCompleted;

    public bool IsInitialized { get; private set; }
    public WhisperModel CurrentModel { get; private set; } = WhisperModel.Tiny;

    public async Task<bool> InitializeAsync(WhisperConfig config)
    {
        try
        {
            Debug.WriteLine($"[SpeechRecognition] Начало инициализации модели: {config.Model}");
            
            // ✅ ИСПРАВЛЕНИЕ: Освобождаем старую модель ПЕРЕД загрузкой новой
            if (IsInitialized && CurrentModel != config.Model)
            {
                Debug.WriteLine($"[SpeechRecognition] Освобождаем старую модель {CurrentModel} перед загрузкой {config.Model}");
                DisposeCurrentModel();
            }

            // Если уже инициализирована та же модель - не перезагружаем
            if (IsInitialized && CurrentModel == config.Model)
            {
                Debug.WriteLine($"[SpeechRecognition] Модель {config.Model} уже загружена, пропускаем инициализацию");
                return true;
            }

            CurrentModel = config.Model;

            // Определяем путь к модели
            string modelPath = GetModelPath(config.Model);
            Debug.WriteLine($"[SpeechRecognition] Путь к модели: {modelPath}");

            // Проверяем существует ли модель, если нет - скачиваем
            if (!File.Exists(modelPath))
            {
                Debug.WriteLine($"[SpeechRecognition] Модель не найдена, начинаем скачивание...");
                await DownloadModelAsync(config.Model, modelPath);
            }

            // Создаем WhisperFactory
            Debug.WriteLine($"[SpeechRecognition] Загружаем модель из файла...");
            _whisperFactory = WhisperFactory.FromPath(modelPath);
            Debug.WriteLine($"[SpeechRecognition] WhisperFactory создан успешно");

            _processor = _whisperFactory.CreateBuilder()
                .WithLanguage("ru") // или из config.Language
                .Build();

            IsInitialized = true;
            Debug.WriteLine($"[SpeechRecognition] Инициализация завершена успешно");
            
            // ✅ ДОПОЛНИТЕЛЬНО: Принудительная сборка мусора после смены модели
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SpeechRecognition] ОШИБКА инициализации: {ex.Message}");
            
            // Очищаем частично инициализированные ресурсы
            DisposeCurrentModel();
            return false;
        }
    }

    /// <summary>
    /// ✅ НОВЫЙ МЕТОД: Освобождает текущую модель из памяти
    /// </summary>
    private void DisposeCurrentModel()
    {
        try
        {
            if (_processor != null)
            {
                Log.Information("Освобождаем WhisperProcessor...");
                _processor.Dispose();
                _processor = null;
            }

            if (_whisperFactory != null)
            {
                Log.Information("Освобождаем WhisperFactory...");
                _whisperFactory.Dispose();
                _whisperFactory = null;
            }

            IsInitialized = false;
            Log.Information("Модель {Model} освобождена из памяти", CurrentModel);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка освобождения модели {Model}", CurrentModel);
        }
    }

    private string GetModelPath(WhisperModel model)
    {
        string modelName = model.ToString().ToLower();
        return Path.Combine("Models", $"ggml-{modelName}.bin");
    }

    // Метод проверки наличия модели
    public async Task<bool> IsModelAvailableAsync(WhisperModel model)
    {
        return await Task.Run(() =>
        {
            string modelPath = GetModelPath(model);
            return File.Exists(modelPath);
        });
    }

    private async Task DownloadModelAsync(WhisperModel model, string modelPath)
    {
        try
        {
            Log.Information("Создаем папку Models...");
            Directory.CreateDirectory("Models");

            var modelType = GetGgmlType(model);
            Log.Information("Тип модели для скачивания: {ModelType}", modelType);

            // Получаем размер модели для расчета прогресса
            long totalSize = await GetModelSizeAsync(model);

            // Уведомляем о начале загрузки
            DownloadProgress?.Invoke(this, new ModelDownloadProgressEvent
            {
                Model = model,
                BytesReceived = 0,
                TotalBytes = totalSize,
                ProgressPercentage = 0,
                Status = "Начинаем загрузку..."
            });

            using var httpClient = new HttpClient();
            var downloader = new WhisperGgmlDownloader(httpClient);

            Log.Information("Начинаем скачивание модели...");

            using var modelStream = await downloader.GetGgmlModelAsync(modelType);
            using var fileStream = File.Create(modelPath);

            // Копируем с отслеживанием прогресса
            await CopyStreamWithProgressAsync(modelStream, fileStream, totalSize, model);

            Log.Information("Модель успешно скачана и сохранена");

            // Уведомляем о завершении
            DownloadCompleted?.Invoke(this, new ModelDownloadCompletedEvent
            {
                Model = model, Success = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка скачивания модели {Model}", model);

            // Уведомляем об ошибке
            DownloadCompleted?.Invoke(this, new ModelDownloadCompletedEvent
            {
                Model = model, Success = false, ErrorMessage = ex.Message
            });

            throw new Exception($"Ошибка скачивания модели {model}: {ex.Message}", ex);
        }
    }

    private async Task CopyStreamWithProgressAsync(Stream source, Stream destination, long totalSize, WhisperModel model)
    {
        byte[] buffer = new byte[8192]; // 8KB буфер
        long totalBytesRead = 0;
        int bytesRead;
        int lastReportedProgress = -1;

        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead);
            totalBytesRead += bytesRead;

            // Вычисляем прогресс и уведомляем каждые 5%
            int currentProgress = (int)((totalBytesRead * 100) / totalSize);

            if (currentProgress != lastReportedProgress && currentProgress % 5 == 0)
            {
                lastReportedProgress = currentProgress;

                DownloadProgress?.Invoke(this, new ModelDownloadProgressEvent
                {
                    Model = model,
                    BytesReceived = totalBytesRead,
                    TotalBytes = totalSize,
                    ProgressPercentage = currentProgress,
                    Status = $"Загружено {currentProgress}%..."
                });
            }
        }
    }

    private GgmlType GetGgmlType(WhisperModel model)
    {
        return model switch
        {
            WhisperModel.Tiny => GgmlType.Tiny,
            WhisperModel.Base => GgmlType.Base,
            WhisperModel.Small => GgmlType.Small,
            WhisperModel.Medium => GgmlType.Medium,
            WhisperModel.Large => GgmlType.LargeV3,
            _ => GgmlType.Tiny
        };
    }

    public async Task<VoiceProcessingResult> RecognizeAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized || _processor == null)
        {
            Log.Error("Сервис не инициализирован");
            return new VoiceProcessingResult
            {
                Success = false, ErrorMessage = "Сервис не инициализирован"
            };
        }

        try
        {
            var startTime = DateTime.Now;
            Log.Information("Начало распознавания, размер данных: {AudioSize} байт", audioData?.Length ?? 0);

            // Проверяем что данные не пустые
            if (audioData == null || audioData.Length == 0)
            {
                Log.Warning("Не получено аудио данных");
                return new VoiceProcessingResult
                {
                    Success = false, ErrorMessage = "Не получено аудио данных"
                };
            }

            // Минимальная проверка размера (хотя бы 0.5 секунды на 16kHz = 16000 байт)
            if (audioData.Length < 16000)
            {
                Log.Warning("Слишком короткая запись ({AudioSize} байт)", audioData.Length);
                return new VoiceProcessingResult
                {
                    Success = false, ErrorMessage = "Слишком короткая запись"
                };
            }

            Log.Debug("Создаем WAV поток в памяти...");
            // Создаем WAV поток в памяти
            using var wavStream = CreateWavMemoryStream(audioData, 16000, 16, 1);
            Log.Debug("WAV поток создан, размер: {StreamSize} байт", wavStream.Length);

            // Сбрасываем позицию потока в начало
            wavStream.Position = 0;

            // Распознаем речь из потока
            Log.Information("Начинаем обработку через Whisper...");

            await foreach (var result in _processor.ProcessAsync(wavStream, cancellationToken))
            {
                Log.Debug("Получен результат: '{Text}', начало: {Start}, конец: {End}", 
                    result.Text, result.Start, result.End);

                if (!string.IsNullOrWhiteSpace(result.Text))
                {
                    var processingTime = DateTime.Now - startTime;
                    Log.Information("УСПЕХ: Распознано за {ProcessingTime}мс", processingTime.TotalMilliseconds);

                    return new VoiceProcessingResult
                    {
                        Success = true,
                        RecognizedText = result.Text.Trim(),
                        Confidence = 0.95f,
                        ProcessingTime = processingTime
                    };
                }
            }

            Log.Warning("Не удалось распознать речь (пустой результат)");
            return new VoiceProcessingResult
            {
                Success = false, ErrorMessage = "Не удалось распознать речь"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Критическая ошибка распознавания");
            return new VoiceProcessingResult
            {
                Success = false, ErrorMessage = $"Ошибка распознавания: {ex.Message}"
            };
        }
    }

    private MemoryStream CreateWavMemoryStream(byte[] audioData, int sampleRate, int bitsPerSample, int channels)
    {
        Log.Debug("Создание WAV потока: {SampleRate}Hz, {BitsPerSample}bit, {Channels}ch", 
            sampleRate, bitsPerSample, channels);

        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        var dataLength = audioData.Length;
        var fileSize = 36 + dataLength;

        // WAV заголовок
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(fileSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // размер fmt секции
        writer.Write((short)1); // PCM формат
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8); // байт в секунду
        writer.Write((short)(channels * bitsPerSample / 8)); // байт на семпл
        writer.Write((short)bitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        writer.Write(audioData);

        Log.Debug("WAV поток создан, итоговый размер: {StreamSize} байт", stream.Length);
        return stream;
    }

    /// <summary>
    /// ✅ УПРОЩЕНО: Теперь просто вызывает InitializeAsync с новой моделью
    /// InitializeAsync сам освободит старую модель
    /// </summary>
    public async Task<bool> ChangeModelAsync(WhisperModel model)
    {
        try
        {
            Log.Information("Смена модели с {OldModel} на {NewModel}", CurrentModel, model);

            // Просто вызываем InitializeAsync - он сам освободит старую модель
            var config = new WhisperConfig { Model = model };
            var result = await InitializeAsync(config);

            Log.Information("Смена модели {Result}", result ? "УСПЕШНА" : "НЕУДАЧНА");
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка смены модели на {Model}", model);
            return false;
        }
    }

    public async Task<long> GetModelSizeAsync(WhisperModel model)
    {
        return await Task.Run(() =>
        {
            // Реальные размеры моделей на основе ваших данных
            return model switch
            {
                WhisperModel.Tiny => 75_871L * 1024, // ~75.8 MB
                WhisperModel.Base => 144_484L * 1024, // ~144.5 MB  
                WhisperModel.Small => 476_174L * 1024, // ~476.2 MB
                WhisperModel.Medium => 1_497_816L * 1024, // ~1.5 GB
                WhisperModel.Large => 3_022_494L * 1024, // ~3.0 GB
                _ => 75_871L * 1024 // по умолчанию Tiny
            };
        });
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Log.Information("Освобождение ресурсов...");
            DisposeCurrentModel();
            _isDisposed = true;
            Log.Information("Ресурсы освобождены");
        }
    }
}