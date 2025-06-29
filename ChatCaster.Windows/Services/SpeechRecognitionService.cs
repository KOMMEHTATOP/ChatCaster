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

    /// <summary>
    /// Диагностика памяти
    /// </summary>
    private void LogMemoryInfo(string operation)
    {
        var managedMemory = GC.GetTotalMemory(false);
        var workingSet = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
        
        Log.Information("[SRS] {Operation} - Managed: {Managed:N0} байт, WorkingSet: {WorkingSet:N0} байт", 
            operation, managedMemory, workingSet);
    }

    public async Task<bool> InitializeAsync(WhisperConfig config)
    {
        try
        {
            Log.Information("[SRS] Начало инициализации модели: {Model}", config.Model);
            LogMemoryInfo($"До инициализации {config.Model}");
            
            // ✅ ИСПРАВЛЕНИЕ: Освобождаем старую модель ПЕРЕД загрузкой новой
            if (IsInitialized && CurrentModel != config.Model)
            {
                Log.Information("[SRS] Освобождаем старую модель {OldModel} перед загрузкой {NewModel}", CurrentModel, config.Model);
                LogMemoryInfo("До освобождения старой модели");
                await DisposeCurrentModelAsync();
                LogMemoryInfo("После освобождения старой модели");
            }

            // Если уже инициализирована та же модель - не перезагружаем
            if (IsInitialized && CurrentModel == config.Model)
            {
                Log.Information("[SRS] Модель {Model} уже загружена, пропускаем инициализацию", config.Model);
                return true;
            }

            CurrentModel = config.Model;

            // Определяем путь к модели
            string modelPath = GetModelPath(config.Model);
            Log.Information("[SRS] Путь к модели: {ModelPath}", modelPath);

            // Проверяем существует ли модель, если нет - скачиваем
            if (!File.Exists(modelPath))
            {
                Log.Information("[SRS] Модель не найдена, начинаем скачивание...");
                await DownloadModelAsync(config.Model, modelPath);
            }

            // Создаем WhisperFactory
            Log.Information("[SRS] Загружаем модель из файла...");
            _whisperFactory = WhisperFactory.FromPath(modelPath);
            Log.Information("[SRS] WhisperFactory создан успешно");

            _processor = _whisperFactory.CreateBuilder()
                .WithLanguage("ru") // или из config.Language
                .Build();

            IsInitialized = true;
            Log.Information("[SRS] Инициализация завершена успешно");
            LogMemoryInfo($"После инициализации {config.Model}");
            
            // ✅ ИСПРАВЛЕНИЕ: Мягкая сборка мусора после смены модели
            await SoftGarbageCollectionAsync();
            LogMemoryInfo($"После сборки мусора {config.Model}");
            
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SRS] ОШИБКА инициализации модели {Model}", config.Model);
            
            // Очищаем частично инициализированные ресурсы
            await DisposeCurrentModelAsync();
            return false;
        }
    }

    /// <summary>
    /// ✅ ИСПРАВЛЕНИЕ: Простое освобождение с диагностикой
    /// </summary>
    private async Task DisposeCurrentModelAsync()
    {
        try
        {
            Log.Information("[SRS] Начинаем освобождение модели {Model}...", CurrentModel);
            
            if (_processor != null)
            {
                Log.Information("[SRS] Освобождаем WhisperProcessor...");
                _processor.Dispose();
                _processor = null;
                Log.Information("[SRS] WhisperProcessor освобожден");
            }

            if (_whisperFactory != null)
            {
                Log.Information("[SRS] Освобождаем WhisperFactory...");
                _whisperFactory.Dispose();
                _whisperFactory = null;
                Log.Information("[SRS] WhisperFactory освобожден");
            }

            IsInitialized = false;
            
            // ✅ ИСПРАВЛЕНИЕ: Заменили агрессивную очистку на мягкую с диагностикой
            await SoftGarbageCollectionAsync();
            
            Log.Information("[SRS] Модель {Model} освобождена из памяти", CurrentModel);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SRS] Ошибка освобождения модели {Model}", CurrentModel);
        }
    }

    /// <summary>
    /// ✅ ИСПРАВЛЕНИЕ: Мягкая сборка мусора с диагностикой памяти
    /// </summary>
    private async Task SoftGarbageCollectionAsync()
    {
        try
        {
            var memoryBefore = GC.GetTotalMemory(false);
            Log.Information("[SRS] Память до очистки: {MemoryBefore:N0} байт", memoryBefore);
            
            // Выполняем в отдельной задаче чтобы не блокировать UI
            await Task.Run(() =>
            {
                // Мягкая сборка мусора (убрана агрессивная очистка)
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });
            
            var memoryAfter = GC.GetTotalMemory(false);
            var freedMemory = memoryBefore - memoryAfter;
            Log.Information("[SRS] Память после очистки: {MemoryAfter:N0} байт, освобождено: {FreedMemory:N0} байт", 
                memoryAfter, freedMemory);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SRS] Ошибка при сборке мусора");
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
            Log.Information("[SRS] Создаем папку Models...");
            Directory.CreateDirectory("Models");

            var modelType = GetGgmlType(model);
            Log.Information("[SRS] Тип модели для скачивания: {ModelType}", modelType);

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

            Log.Information("[SRS] Начинаем скачивание модели...");

            using var modelStream = await downloader.GetGgmlModelAsync(modelType);
            using var fileStream = File.Create(modelPath);

            // Копируем с отслеживанием прогресса
            await CopyStreamWithProgressAsync(modelStream, fileStream, totalSize, model);

            Log.Information("[SRS] Модель успешно скачана и сохранена");

            // Уведомляем о завершении
            DownloadCompleted?.Invoke(this, new ModelDownloadCompletedEvent
            {
                Model = model, 
                Success = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SRS] Ошибка скачивания модели {Model}", model);

            // ✅ ИСПРАВЛЕНИЕ: Удаляем частично скачанный файл при ошибке
            try
            {
                if (File.Exists(modelPath))
                {
                    File.Delete(modelPath);
                    Log.Information("[SRS] Удален частично скачанный файл: {ModelPath}", modelPath);
                }
            }
            catch (Exception deleteEx)
            {
                Log.Warning(deleteEx, "[SRS] Не удалось удалить частично скачанный файл: {ModelPath}", modelPath);
            }

            // Уведомляем об ошибке
            DownloadCompleted?.Invoke(this, new ModelDownloadCompletedEvent
            {
                Model = model, 
                Success = false, 
                ErrorMessage = ex.Message
            });

            throw new Exception($"Ошибка скачивания модели {model}: {ex.Message}", ex);
        }
    }

    private async Task CopyStreamWithProgressAsync(Stream source, Stream destination, long totalSize, WhisperModel model)
    {
        byte[] buffer = new byte[81920]; // ✅ УВЕЛИЧЕН буфер до 80KB для лучшей производительности
        long totalBytesRead = 0;
        int bytesRead;
        int lastReportedProgress = -1;

        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead);
            totalBytesRead += bytesRead;

            // Вычисляем прогресс и уведомляем каждые 1% (было 5%)
            int currentProgress = (int)((totalBytesRead * 100) / totalSize);

            if (currentProgress != lastReportedProgress)
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
            Log.Error("[SRS] Сервис не инициализирован");
            return new VoiceProcessingResult
            {
                Success = false, 
                ErrorMessage = "Сервис не инициализирован"
            };
        }

        try
        {
            var startTime = DateTime.Now;
            Log.Information("[SRS] Начало распознавания, размер данных: {AudioSize} байт", audioData?.Length ?? 0);

            // Проверяем что данные не пустые
            if (audioData == null || audioData.Length == 0)
            {
                Log.Warning("[SRS] Не получено аудио данных");
                return new VoiceProcessingResult
                {
                    Success = false, 
                    ErrorMessage = "Не получено аудио данных"
                };
            }

            // Минимальная проверка размера (хотя бы 0.5 секунды на 16kHz = 16000 байт)
            if (audioData.Length < 16000)
            {
                Log.Warning("[SRS] Слишком короткая запись ({AudioSize} байт)", audioData.Length);
                return new VoiceProcessingResult
                {
                    Success = false, 
                    ErrorMessage = "Слишком короткая запись"
                };
            }

            Log.Debug("[SRS] Создаем WAV поток в памяти...");
            // Создаем WAV поток в памяти
            using var wavStream = CreateWavMemoryStream(audioData, 16000, 16, 1);
            Log.Debug("[SRS] WAV поток создан, размер: {StreamSize} байт", wavStream.Length);

            // Сбрасываем позицию потока в начало
            wavStream.Position = 0;

            // Распознаем речь из потока
            Log.Information("[SRS] Начинаем обработку через Whisper...");

            await foreach (var result in _processor.ProcessAsync(wavStream, cancellationToken))
            {
                Log.Debug("[SRS] Получен результат: '{Text}', начало: {Start}, конец: {End}", 
                    result.Text, result.Start, result.End);

                if (!string.IsNullOrWhiteSpace(result.Text))
                {
                    var processingTime = DateTime.Now - startTime;
                    Log.Information("[SRS] УСПЕХ: Распознано за {ProcessingTime}мс", processingTime.TotalMilliseconds);

                    return new VoiceProcessingResult
                    {
                        Success = true,
                        RecognizedText = result.Text.Trim(),
                        Confidence = 0.95f,
                        ProcessingTime = processingTime
                    };
                }
            }

            Log.Warning("[SRS] Не удалось распознать речь (пустой результат)");
            return new VoiceProcessingResult
            {
                Success = false, 
                ErrorMessage = "Не удалось распознать речь"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SRS] Критическая ошибка распознавания");
            return new VoiceProcessingResult
            {
                Success = false, 
                ErrorMessage = $"Ошибка распознавания: {ex.Message}"
            };
        }
    }

    private MemoryStream CreateWavMemoryStream(byte[] audioData, int sampleRate, int bitsPerSample, int channels)
    {
        Log.Debug("[SRS] Создание WAV потока: {SampleRate}Hz, {BitsPerSample}bit, {Channels}ch", 
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

        Log.Debug("[SRS] WAV поток создан, итоговый размер: {StreamSize} байт", stream.Length);
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
            Log.Information("[SRS] Смена модели с {OldModel} на {NewModel}", CurrentModel, model);

            // Просто вызываем InitializeAsync - он сам освободит старую модель
            var config = new WhisperConfig { Model = model };
            var result = await InitializeAsync(config);

            Log.Information("[SRS] Смена модели {Result}", result ? "УСПЕШНА" : "НЕУДАЧНА");
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SRS] Ошибка смены модели на {Model}", model);
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
            Log.Information("[SRS] Освобождение ресурсов...");
            
            // ✅ ИСПРАВЛЕНИЕ: Мягкое освобождение для Dispose
            try
            {
                DisposeCurrentModelAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SRS] Ошибка при освобождении ресурсов");
            }
            
            _isDisposed = true;
            Log.Information("[SRS] Ресурсы освобождены");
        }
    }
}