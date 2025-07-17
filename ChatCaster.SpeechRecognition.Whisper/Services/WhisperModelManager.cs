using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.SpeechRecognition.Whisper.Exceptions;
using ChatCaster.SpeechRecognition.Whisper.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ChatCaster.SpeechRecognition.Whisper.Services;

/// <summary>
/// Менеджер для управления моделями Whisper - загрузка, кэширование, валидация
/// </summary>
public class WhisperModelManager : IDisposable
{
    private readonly ILogger<WhisperModelManager> _logger;
    private readonly ModelDownloader _modelDownloader;
    private readonly ConcurrentDictionary<string, ModelCacheEntry> _modelCache;
    private readonly SemaphoreSlim _initializationSemaphore;
    private bool _disposed;

    public WhisperModelManager(ILogger<WhisperModelManager> logger, ModelDownloader modelDownloader)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modelDownloader = modelDownloader ?? throw new ArgumentNullException(nameof(modelDownloader));
        _modelCache = new ConcurrentDictionary<string, ModelCacheEntry>();
        _initializationSemaphore = new SemaphoreSlim(1, 1);

        // Подписываемся на события загрузки
        _modelDownloader.DownloadProgress += OnDownloadProgress;
    }

    /// <summary>
    /// События прогресса подготовки модели
    /// </summary>
    public event EventHandler<ModelPreparationProgressEventArgs>? PreparationProgress;

    /// <summary>
    /// Подготавливает модель к использованию (загружает если нужно, валидирует)
    /// </summary>
    /// <param name="modelSize">Размер модели</param>
    /// <param name="modelDirectory">Директория для моделей</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Путь к готовой модели</returns>
    public async Task<string> PrepareModelAsync(
        string modelSize, 
        string modelDirectory, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelSize))
        {
            throw new ArgumentException("Model size cannot be empty", nameof(modelSize));
        }

        if (!WhisperConstants.ModelSizes.All.Contains(modelSize))
        {
            throw WhisperConfigurationException.InvalidModelSize(modelSize);
        }
        
        await _initializationSemaphore.WaitAsync(cancellationToken);
        try
        {
            var cacheKey = GetCacheKey(modelSize, modelDirectory);
            
            // Проверяем кэш
            if (_modelCache.TryGetValue(cacheKey, out var cachedEntry) && 
                cachedEntry.IsValid && 
                File.Exists(cachedEntry.FilePath))
            {
                OnPreparationProgress(new ModelPreparationProgressEventArgs
                {
                    ModelSize = modelSize,
                    Status = ModelPreparationStatus.Ready,
                    ProgressPercentage = 100,
                    Message = "Model ready from cache"
                });
                return cachedEntry.FilePath;
            }

            // Создаем директорию если не существует
            EnsureDirectoryExists(modelDirectory);

            OnPreparationProgress(new ModelPreparationProgressEventArgs
            {
                ModelSize = modelSize,
                Status = ModelPreparationStatus.Checking,
                ProgressPercentage = 10,
                Message = "Checking model availability"
            });

            // Получаем путь к файлу модели
            var modelFilePath = WhisperConstants.Paths.GetModelPath(modelDirectory, modelSize);

            // Проверяем существует ли модель локально
            var localModelInfo = await GetLocalModelInfoAsync(modelFilePath, cancellationToken);
            
            if (localModelInfo.IsValid)
            {
                var cacheEntry = new ModelCacheEntry
                {
                    ModelSize = modelSize,
                    FilePath = modelFilePath,
                    SizeBytes = localModelInfo.SizeBytes,
                    LastValidated = DateTime.Now,
                    IsValid = true
                };
                
                _modelCache.AddOrUpdate(cacheKey, cacheEntry, (_, _) => cacheEntry);
                
                OnPreparationProgress(new ModelPreparationProgressEventArgs
                {
                    ModelSize = modelSize,
                    Status = ModelPreparationStatus.Ready,
                    ProgressPercentage = 100,
                    Message = "Local model validated"
                });

                return modelFilePath;
            }

            // Нужно загружать модель
            OnPreparationProgress(new ModelPreparationProgressEventArgs
            {
                ModelSize = modelSize,
                Status = ModelPreparationStatus.Downloading,
                ProgressPercentage = 20,
                Message = "Starting model download"
            });

            var downloadedPath = await _modelDownloader.DownloadModelIfNeededAsync(
                modelSize, modelDirectory, cancellationToken);

            // Проверяем загруженную модель
            OnPreparationProgress(new ModelPreparationProgressEventArgs
            {
                ModelSize = modelSize,
                Status = ModelPreparationStatus.Validating,
                ProgressPercentage = 90,
                Message = "Validating downloaded model"
            });

            var downloadedModelInfo = await GetLocalModelInfoAsync(downloadedPath, cancellationToken);
            if (!downloadedModelInfo.IsValid)
            {
                throw WhisperInitializationException.ModelLoadFailed(downloadedPath, 
                    new InvalidOperationException("Downloaded model failed validation"));
            }

            // Добавляем в кэш
            var finalCacheEntry = new ModelCacheEntry
            {
                ModelSize = modelSize,
                FilePath = downloadedPath,
                SizeBytes = downloadedModelInfo.SizeBytes,
                LastValidated = DateTime.Now,
                IsValid = true
            };
            
            _modelCache.AddOrUpdate(cacheKey, finalCacheEntry, (_, _) => finalCacheEntry);

            OnPreparationProgress(new ModelPreparationProgressEventArgs
            {
                ModelSize = modelSize,
                Status = ModelPreparationStatus.Ready,
                ProgressPercentage = 100,
                Message = "Model ready"
            });

            return downloadedPath;
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    /// <summary>
    /// Проверяет доступность модели (локально или для загрузки)
    /// </summary>
    /// <param name="modelSize">Размер модели</param>
    /// <param name="modelDirectory">Директория моделей</param>
    /// <returns>Информация о доступности модели</returns>
    public async Task<ModelAvailabilityInfo> CheckModelAvailabilityAsync(
        string modelSize, 
        string modelDirectory)
    {
        var result = new ModelAvailabilityInfo { ModelSize = modelSize };

        try
        {
            // Проверяем локальную модель
            var localPath = WhisperConstants.Paths.GetModelPath(modelDirectory, modelSize);
            var localInfo = await GetLocalModelInfoAsync(localPath, CancellationToken.None);
            
            result.IsAvailableLocally = localInfo.IsValid;
            result.LocalPath = localInfo.IsValid ? localPath : null;
            result.LocalSizeBytes = localInfo.SizeBytes;

            // Проверяем доступность для загрузки
            result.IsAvailableForDownload = await _modelDownloader.IsModelAvailableForDownloadAsync(modelSize);
            
            if (result.IsAvailableForDownload)
            {
                var downloadInfo = _modelDownloader.GetModelInfo(modelSize);
                result.DownloadSizeBytes = downloadInfo?.ExpectedSizeBytes ?? 0;
                result.DownloadUrl = downloadInfo?.Url;
            }

            result.IsSupported = WhisperConstants.ModelSizes.All.Contains(modelSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check model availability: {ModelSize}", modelSize);
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Очищает кэш моделей
    /// </summary>
    /// <param name="olderThan">Очистить записи старше указанного времени (по умолчанию 1 час)</param>
    public void ClearCache(TimeSpan? olderThan = null)
    {
        var threshold = DateTime.Now - (olderThan ?? TimeSpan.FromHours(1));
        var keysToRemove = _modelCache
            .Where(kvp => kvp.Value.LastValidated < threshold)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _modelCache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Получает информацию о всех кэшированных моделях
    /// </summary>
    /// <returns>Список кэшированных моделей</returns>
    public IEnumerable<ModelCacheEntry> GetCachedModels()
    {
        return _modelCache.Values.ToList();
    }

    /// <summary>
    /// Удаляет модель из локального хранилища и кэша
    /// </summary>
    /// <param name="modelSize">Размер модели</param>
    /// <param name="modelDirectory">Директория моделей</param>
    /// <returns>true если модель была удалена</returns>
    public async Task<bool> DeleteModelAsync(string modelSize, string modelDirectory)
    {
        var cacheKey = GetCacheKey(modelSize, modelDirectory);
        _modelCache.TryRemove(cacheKey, out _);

        return await _modelDownloader.DeleteModelAsync(modelSize, modelDirectory);
    }

    #region Private Methods

    private async Task<LocalModelInfo> GetLocalModelInfoAsync(string filePath, CancellationToken cancellationToken)
    {
        var result = new LocalModelInfo();

        try
        {
            if (!File.Exists(filePath))
            {
                return result;
            }

            var fileInfo = new FileInfo(filePath);
            result.SizeBytes = fileInfo.Length;
            result.LastModified = fileInfo.LastWriteTime;

            // Базовая проверка: файл должен быть больше 1MB и иметь расширение .bin
            result.IsValid = fileInfo.Length > 1_000_000 && 
                           fileInfo.Extension.Equals(".bin", StringComparison.OrdinalIgnoreCase);

            // Дополнительная проверка: попробуем прочитать начало файла
            if (result.IsValid)
            {
                result.IsValid = await ValidateModelFileHeaderAsync(filePath, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get local model info: {FilePath}", filePath);
        }

        return result;
    }

    private async Task<bool> ValidateModelFileHeaderAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var buffer = new byte[16];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

            // Простая проверка: файлы Whisper начинаются с определенной сигнатуры
            // Это базовая проверка, более детальную можно добавить позже
            return bytesRead >= 4 && buffer[0] != 0 && buffer[1] != 0;
        }
        catch
        {
            return false;
        }
    }

    private void EnsureDirectoryExists(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private string GetCacheKey(string modelSize, string directory)
    {
        return $"{modelSize}:{Path.GetFullPath(directory)}";
    }

    private void OnDownloadProgress(object? sender, DownloadProgressEventArgs e)
    {
        OnPreparationProgress(new ModelPreparationProgressEventArgs
        {
            ModelSize = e.ModelSize,
            Status = ModelPreparationStatus.Downloading,
            ProgressPercentage = Math.Min(90, 20 + e.ProgressPercentage * 0.7), // 20-90% для загрузки
            Message = e.GetProgressText()
        });
    }

    private void OnPreparationProgress(ModelPreparationProgressEventArgs e)
    {
        PreparationProgress?.Invoke(this, e);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!_disposed)
        {
            _modelDownloader.DownloadProgress -= OnDownloadProgress;
            _initializationSemaphore?.Dispose();
            _disposed = true;
        }
    }

    #endregion
}

/// <summary>
/// Запись в кэше моделей
/// </summary>
public class ModelCacheEntry
{
    public string ModelSize { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastValidated { get; set; }
    public bool IsValid { get; set; }

    public double SizeMB => SizeBytes / 1024.0 / 1024.0;
}

/// <summary>
/// Информация о локальной модели
/// </summary>
public class LocalModelInfo
{
    public bool IsValid { get; set; }
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
}

/// <summary>
/// Информация о доступности модели
/// </summary>
public class ModelAvailabilityInfo
{
    public string ModelSize { get; set; } = string.Empty;
    public bool IsAvailableLocally { get; set; }
    public bool IsAvailableForDownload { get; set; }
    public bool IsSupported { get; set; }
    public string? LocalPath { get; set; }
    public string? DownloadUrl { get; set; }
    public long LocalSizeBytes { get; set; }
    public long DownloadSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }

    public bool IsReady => IsAvailableLocally;
    public bool RequiresDownload => !IsAvailableLocally && IsAvailableForDownload;
}

/// <summary>
/// Аргументы события прогресса подготовки модели
/// </summary>
public class ModelPreparationProgressEventArgs : EventArgs
{
    public string ModelSize { get; set; } = string.Empty;
    public ModelPreparationStatus Status { get; set; }
    public double ProgressPercentage { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Статус подготовки модели
/// </summary>
public enum ModelPreparationStatus
{
    Checking,
    Downloading,
    Validating,
    Ready,
    Error
}