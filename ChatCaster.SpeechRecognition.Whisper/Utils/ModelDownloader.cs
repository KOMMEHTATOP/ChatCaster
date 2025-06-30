using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.SpeechRecognition.Whisper.Exceptions;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ChatCaster.SpeechRecognition.Whisper.Utils;

/// <summary>
/// Утилита для загрузки моделей Whisper с официальных источников
/// </summary>
public class ModelDownloader
{
    private readonly ILogger<ModelDownloader> _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _downloadSemaphore;

    // Официальные URL для загрузки моделей Whisper
    private static readonly Dictionary<string, ModelInfo> ModelUrls = new()
    {
        [WhisperConstants.ModelSizes.Tiny] = new ModelInfo
        {
            Size = WhisperConstants.ModelSizes.Tiny,
            Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
            ExpectedSizeBytes = 39_000_000, // ~39MB
            Sha256Hash = "bd577a113a864445d4c299885e0cb97d4ba92b5f"
        },
        [WhisperConstants.ModelSizes.Base] = new ModelInfo
        {
            Size = WhisperConstants.ModelSizes.Base,
            Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
            ExpectedSizeBytes = 148_000_000, // ~148MB
            Sha256Hash = "465678afde2af4b1446e1b2be5e1b80dd7b5c6d4"
        },
        [WhisperConstants.ModelSizes.Small] = new ModelInfo
        {
            Size = WhisperConstants.ModelSizes.Small,
            Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
            ExpectedSizeBytes = 488_000_000, // ~488MB
            Sha256Hash = "55356645c2b361a969dfd0ef2c5a50d530afd8c5"
        },
        [WhisperConstants.ModelSizes.Medium] = new ModelInfo
        {
            Size = WhisperConstants.ModelSizes.Medium,
            Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin",
            ExpectedSizeBytes = 1_533_000_000, // ~1.5GB
            Sha256Hash = "fd9727b6e1217c2f614f9b698455c4ffd82463b4"
        },
        [WhisperConstants.ModelSizes.Large] = new ModelInfo
        {
            Size = WhisperConstants.ModelSizes.Large,
            Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large.bin",
            ExpectedSizeBytes = 3_094_000_000, // ~3GB
            Sha256Hash = "0f4c8e34f21cf1a914c59d8b3ce882345ad349d6"
        },
        [WhisperConstants.ModelSizes.LargeV2] = new ModelInfo
        {
            Size = WhisperConstants.ModelSizes.LargeV2,
            Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v2.bin",
            ExpectedSizeBytes = 3_094_000_000, // ~3GB
            Sha256Hash = "0f4c8e34f21cf1a914c59d8b3ce882345ad349d6"
        },
        [WhisperConstants.ModelSizes.LargeV3] = new ModelInfo
        {
            Size = WhisperConstants.ModelSizes.LargeV3,
            Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin",
            ExpectedSizeBytes = 3_094_000_000, // ~3GB
            Sha256Hash = "ad82bf6a9043ceed055076d0fd39f5f186ff8062"
        }
    };

    public ModelDownloader(ILogger<ModelDownloader> logger, HttpClient? httpClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient();
        _downloadSemaphore = new SemaphoreSlim(1, 1); // Только одна загрузка одновременно
        
        // Настройки HTTP клиента
        _httpClient.Timeout = TimeSpan.FromMinutes(30); // Долгий таймаут для больших моделей
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"ChatCaster/{WhisperConstants.EngineVersion}");
    }

    /// <summary>
    /// Событие прогресса загрузки
    /// </summary>
    public event EventHandler<DownloadProgressEventArgs>? DownloadProgress;

    /// <summary>
    /// Загружает модель если она отсутствует
    /// </summary>
    /// <param name="modelSize">Размер модели</param>
    /// <param name="destinationDirectory">Директория для сохранения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Путь к загруженному файлу модели</returns>
    public async Task<string> DownloadModelIfNeededAsync(
        string modelSize, 
        string destinationDirectory, 
        CancellationToken cancellationToken = default)
    {
        if (!ModelUrls.ContainsKey(modelSize))
        {
            throw WhisperConfigurationException.InvalidModelSize(modelSize);
        }

        var modelInfo = ModelUrls[modelSize];
        var fileName = WhisperConstants.Paths.GetModelFileName(modelSize);
        var filePath = Path.Combine(destinationDirectory, fileName);

        // Проверяем существует ли файл и валиден ли он
        if (await IsModelValidAsync(filePath, modelInfo, cancellationToken))
        {
            _logger.LogDebug("Model {ModelSize} already exists and is valid: {Path}", modelSize, filePath);
            return filePath;
        }

        // Загружаем модель
        _logger.LogInformation("Downloading Whisper model: {ModelSize} ({Size:F1}MB)", 
            modelSize, modelInfo.ExpectedSizeBytes / 1024.0 / 1024.0);

        await _downloadSemaphore.WaitAsync(cancellationToken);
        try
        {
            await DownloadModelAsync(modelInfo, filePath, cancellationToken);
            return filePath;
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    /// <summary>
    /// Проверяет доступность модели для загрузки
    /// </summary>
    /// <param name="modelSize">Размер модели</param>
    /// <returns>true если модель можно загрузить</returns>
    public async Task<bool> IsModelAvailableForDownloadAsync(string modelSize)
    {
        if (!ModelUrls.ContainsKey(modelSize))
        {
            return false;
        }

        try
        {
            var modelInfo = ModelUrls[modelSize];
            var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, modelInfo.Url),
                HttpCompletionOption.ResponseHeadersRead);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check model availability: {ModelSize}", modelSize);
            return false;
        }
    }

    /// <summary>
    /// Получает информацию о модели
    /// </summary>
    /// <param name="modelSize">Размер модели</param>
    /// <returns>Информация о модели или null если не найдена</returns>
    public ModelInfo? GetModelInfo(string modelSize)
    {
        return ModelUrls.GetValueOrDefault(modelSize);
    }

    /// <summary>
    /// Получает список всех доступных для загрузки моделей
    /// </summary>
    /// <returns>Список размеров моделей</returns>
    public IEnumerable<string> GetAvailableModelSizes()
    {
        return ModelUrls.Keys;
    }

    /// <summary>
    /// Удаляет модель из локального хранилища
    /// </summary>
    /// <param name="modelSize">Размер модели</param>
    /// <param name="directory">Директория с моделями</param>
    /// <returns>true если модель была удалена</returns>
    public async Task<bool> DeleteModelAsync(string modelSize, string directory)
    {
        try
        {
            var fileName = WhisperConstants.Paths.GetModelFileName(modelSize);
            var filePath = Path.Combine(directory, fileName);

            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
                _logger.LogInformation("Deleted model: {ModelSize} from {Path}", modelSize, filePath);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete model: {ModelSize}", modelSize);
            return false;
        }
    }

    #region Private Methods

    private async Task<bool> IsModelValidAsync(string filePath, ModelInfo modelInfo, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            var fileInfo = new FileInfo(filePath);
            
            // Проверяем размер файла (с погрешностью ±10%)
            var sizeDifference = Math.Abs(fileInfo.Length - modelInfo.ExpectedSizeBytes);
            var sizeToleranceBytes = modelInfo.ExpectedSizeBytes * 0.1; // 10% погрешность
            
            if (sizeDifference > sizeToleranceBytes)
            {
                _logger.LogWarning("Model file size mismatch: expected ~{Expected}MB, got {Actual}MB", 
                    modelInfo.ExpectedSizeBytes / 1024.0 / 1024.0,
                    fileInfo.Length / 1024.0 / 1024.0);
                return false;
            }

            // Проверяем хеш (опционально, так как это медленно для больших файлов)
            if (!string.IsNullOrEmpty(modelInfo.Sha256Hash))
            {
                var computedHash = await ComputeFileHashAsync(filePath, cancellationToken);
                if (!string.Equals(computedHash, modelInfo.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Model file hash mismatch: expected {Expected}, got {Actual}", 
                        modelInfo.Sha256Hash, computedHash);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate model file: {FilePath}", filePath);
            return false;
        }
    }

    private async Task DownloadModelAsync(ModelInfo modelInfo, string destinationPath, CancellationToken cancellationToken)
    {
        // Создаем директорию если не существует
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Временный файл для загрузки
        var tempPath = destinationPath + ".tmp";

        try
        {
            using var response = await _httpClient.GetAsync(modelInfo.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? modelInfo.ExpectedSizeBytes;
            
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;
            var lastProgressReport = DateTime.Now;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalBytesRead += bytesRead;

                // Отправляем прогресс каждые 500мс
                if (DateTime.Now - lastProgressReport > TimeSpan.FromMilliseconds(500))
                {
                    var progressPercentage = (double)totalBytesRead / totalBytes * 100;
                    OnDownloadProgress(new DownloadProgressEventArgs
                    {
                        ModelSize = modelInfo.Size,
                        BytesDownloaded = totalBytesRead,
                        TotalBytes = totalBytes,
                        ProgressPercentage = progressPercentage
                    });
                    lastProgressReport = DateTime.Now;
                }
            }

            // Переименовываем временный файл
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
            File.Move(tempPath, destinationPath);

            _logger.LogInformation("Successfully downloaded model {ModelSize}: {Path} ({Size:F1}MB)", 
                modelInfo.Size, destinationPath, totalBytesRead / 1024.0 / 1024.0);

            // Финальное уведомление о прогрессе
            OnDownloadProgress(new DownloadProgressEventArgs
            {
                ModelSize = modelInfo.Size,
                BytesDownloaded = totalBytesRead,
                TotalBytes = totalBytes,
                ProgressPercentage = 100,
                IsCompleted = true
            });
        }
        catch (Exception ex)
        {
            // Удаляем временный файл при ошибке
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }

            _logger.LogError(ex, "Failed to download model {ModelSize}", modelInfo.Size);
            throw new WhisperInitializationException($"Failed to download model {modelInfo.Size}", ex);
        }
    }

    private async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void OnDownloadProgress(DownloadProgressEventArgs e)
    {
        DownloadProgress?.Invoke(this, e);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _httpClient?.Dispose();
        _downloadSemaphore?.Dispose();
    }

    #endregion
}

/// <summary>
/// Информация о модели Whisper
/// </summary>
public class ModelInfo
{
    public string Size { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long ExpectedSizeBytes { get; set; }
    public string? Sha256Hash { get; set; }

    public double SizeMB => ExpectedSizeBytes / 1024.0 / 1024.0;
    public double SizeGB => ExpectedSizeBytes / 1024.0 / 1024.0 / 1024.0;
}

/// <summary>
/// Аргументы события прогресса загрузки
/// </summary>
public class DownloadProgressEventArgs : EventArgs
{
    public string ModelSize { get; set; } = string.Empty;
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double ProgressPercentage { get; set; }
    public bool IsCompleted { get; set; }

    public string GetProgressText()
    {
        return $"Downloading {ModelSize}: {BytesDownloaded / 1024.0 / 1024.0:F1}MB / {TotalBytes / 1024.0 / 1024.0:F1}MB ({ProgressPercentage:F1}%)";
    }
}