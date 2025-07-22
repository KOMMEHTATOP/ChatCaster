using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using ChatCaster.Core.Updates;
using ChatCaster.Core.Models;
using ChatCaster.Core.Constants;
using Serilog;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Реализация сервиса обновлений через GitHub Releases API
/// </summary>
public class GitHubUpdateService : IUpdateService, IDisposable
{
    #region Fields

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private bool _isDisposed;

    #endregion

    #region Events

    public event EventHandler<UpdateResult>? ProgressChanged;
    public event EventHandler<UpdateResult>? OperationCompleted;

    #endregion

    #region Constructor

    public GitHubUpdateService()
    {
        _logger = Log.ForContext<GitHubUpdateService>();
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(UpdateConstants.HttpTimeoutSeconds);
        
        // GitHub требует User-Agent заголовок
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            $"{AppConstants.AppName}/{AppConstants.AppVersion}");
    }

    #endregion

    #region Public Methods

    public async Task<UpdateResult> CheckForUpdatesAsync(
        string currentVersion, 
        bool includePreReleases = false, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Information("Проверка обновлений. Текущая версия: {CurrentVersion}", currentVersion);

            var latestRelease = await GetLatestReleaseAsync(includePreReleases, cancellationToken);
            if (latestRelease == null)
            {
                _logger.Information("Релизы не найдены");
                return UpdateResult.NoUpdatesAvailable();
            }

            var updateInfo = MapGitHubReleaseToUpdateInfo(latestRelease);
            if (updateInfo == null)
            {
                _logger.Warning("Не удалось найти Windows исполняемый файл в релизе");
                return UpdateResult.NoUpdatesAvailable();
            }

            if (!updateInfo.IsNewerThan(currentVersion))
            {
                _logger.Information("Доступная версия {AvailableVersion} не новее текущей {CurrentVersion}", 
                    updateInfo.Version, currentVersion);
                return UpdateResult.NoUpdatesAvailable();
            }

            _logger.Information("Найдено обновление: {Version}", updateInfo.Version);
            var result = UpdateResult.Success(UpdateResultType.UpdateAvailable, updateInfo);
            
            OperationCompleted?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Проверка обновлений отменена");
            return UpdateResult.Failure(UpdateResultType.CheckError, "Операция была отменена");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка при проверке обновлений");
            return UpdateResult.Failure(UpdateResultType.CheckError, ex.Message);
        }
    }

    public async Task<UpdateResult> DownloadUpdateAsync(
        UpdateInfo updateInfo, 
        string? downloadPath = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Information("Начинаем скачивание обновления {Version}", updateInfo.Version);

            // Определяем путь для скачивания
            downloadPath ??= Path.Combine(Path.GetTempPath(), UpdateConstants.UpdateFileName);

            // Создаем директорию если не существует
            var directory = Path.GetDirectoryName(downloadPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Скачиваем с отчетом о прогрессе
            using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, 
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? updateInfo.FileSizeBytes;
            var downloadedBytes = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                downloadedBytes += bytesRead;

                // Отправляем прогресс
                if (totalBytes > 0)
                {
                    var percentage = (int)((downloadedBytes * 100) / totalBytes);
                    var progressResult = UpdateResult.Progress(UpdateResultType.DownloadInProgress, percentage, updateInfo);
                    ProgressChanged?.Invoke(this, progressResult);
                }
            }

            _logger.Information("Скачивание завершено: {FilePath}", downloadPath);

            // Проверяем размер файла
            var fileInfo = new FileInfo(downloadPath);
            if (fileInfo.Length != totalBytes && totalBytes > 0)
            {
                _logger.Warning("Размер скачанного файла не соответствует ожидаемому");
            }

            var result = UpdateResult.Success(UpdateResultType.DownloadCompleted, updateInfo, downloadPath);
            OperationCompleted?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Скачивание обновления отменено");
            return UpdateResult.Failure(UpdateResultType.DownloadError, "Скачивание было отменено");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка при скачивании обновления");
            return UpdateResult.Failure(UpdateResultType.DownloadError, ex.Message);
        }
    }

    public async Task<UpdateResult> ApplyUpdateAsync(
        string updateFilePath, 
        bool restartApplication = true, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Information("Применение обновления: {FilePath}", updateFilePath);

            if (!File.Exists(updateFilePath))
            {
                return UpdateResult.Failure(UpdateResultType.ApplyError, "Файл обновления не найден");
            }

            // Проверяем целостность файла
            if (!await ValidateUpdateFileAsync(updateFilePath, cancellationToken: cancellationToken))
            {
                return UpdateResult.Failure(UpdateResultType.ApplyError, "Файл обновления поврежден");
            }

            var currentExecutablePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExecutablePath))
            {
                return UpdateResult.Failure(UpdateResultType.ApplyError, "Не удалось определить путь к текущему исполняемому файлу");
            }

            // Создаем updater процесс
            var updaterPath = CreateUpdaterScript(currentExecutablePath, updateFilePath, restartApplication);
            
            _logger.Information("Запуск updater: {UpdaterPath}", updaterPath);

            // Запускаем updater
            var startInfo = new ProcessStartInfo
            {
                FileName = updaterPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(startInfo);

            _logger.Information("Updater запущен, завершаем приложение");

            var result = UpdateResult.Success(UpdateResultType.UpdateApplied);
            OperationCompleted?.Invoke(this, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка при применении обновления");
            return UpdateResult.Failure(UpdateResultType.ApplyError, ex.Message);
        }
    }

    public async Task<UpdateInfo?> GetLatestVersionInfoAsync(
        bool includePreReleases = false, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latestRelease = await GetLatestReleaseAsync(includePreReleases, cancellationToken);
            return latestRelease != null ? MapGitHubReleaseToUpdateInfo(latestRelease) : null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка получения информации о последней версии");
            return null;
        }
    }

    public bool ShouldCheckForUpdates(UpdateConfig updateConfig)
    {
        if (!updateConfig.EnableAutoCheck)
            return false;

        if (updateConfig.LastCheckTime == null)
            return true;

        var timeSinceLastCheck = DateTime.UtcNow - updateConfig.LastCheckTime.Value;
        return timeSinceLastCheck.TotalHours >= updateConfig.CheckIntervalHours;
    }

    public async Task<bool> ValidateUpdateFileAsync(
        string filePath, 
        string? expectedHash = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;

            var fileInfo = new FileInfo(filePath);
            
            // Проверяем размер файла
            if (fileInfo.Length > UpdateConstants.MaxUpdateFileSizeBytes)
            {
                _logger.Warning("Файл обновления слишком большой: {Size} байт", fileInfo.Length);
                return false;
            }

            if (fileInfo.Length == 0)
            {
                _logger.Warning("Файл обновления пустой");
                return false;
            }

            // Проверяем хэш если предоставлен
            if (!string.IsNullOrEmpty(expectedHash))
            {
                var actualHash = await CalculateFileHashAsync(filePath, cancellationToken);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Warning("Хэш файла не соответствует ожидаемому");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка проверки файла обновления");
            return false;
        }
    }

    public async Task CleanupTempFilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var updateFiles = Directory.GetFiles(tempPath, "ChatCaster-update*.exe");

            foreach (var file in updateFiles)
            {
                try
                {
                    File.Delete(file);
                    _logger.Debug("Удален временный файл: {File}", file);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Не удалось удалить временный файл: {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Ошибка очистки временных файлов");
        }
    }

    #endregion

    #region Private Methods

    private async Task<GitHubRelease?> GetLatestReleaseAsync(bool includePreReleases, CancellationToken cancellationToken)
    {
        var url = includePreReleases 
            ? UpdateConstants.GitHubReleasesApiUrl.Replace("/latest", "")
            : UpdateConstants.GitHubReleasesApiUrl;

        var response = await _httpClient.GetStringAsync(url, cancellationToken);

        if (includePreReleases)
        {
            var releases = JsonSerializer.Deserialize<GitHubRelease[]>(response);
            return releases?.FirstOrDefault();
        }
        else
        {
            return JsonSerializer.Deserialize<GitHubRelease>(response);
        }
    }

    private UpdateInfo? MapGitHubReleaseToUpdateInfo(GitHubRelease release)
    {
        // Ищем Windows исполняемый файл
        var windowsAsset = release.Assets?.FirstOrDefault(a => 
            a.Name.StartsWith(UpdateConstants.WindowsExecutablePrefix) && 
            a.Name.EndsWith(UpdateConstants.WindowsExecutableSuffix));

        if (windowsAsset == null)
            return null;

        return new UpdateInfo
        {
            Version = release.TagName.TrimStart('v'),
            Name = release.Name,
            ReleaseNotes = release.Body,
            ReleaseDate = release.PublishedAt,
            DownloadUrl = windowsAsset.BrowserDownloadUrl,
            FileSizeBytes = windowsAsset.Size,
            IsPreRelease = release.PreRelease
        };
    }

    private string CreateUpdaterScript(string currentExePath, string updateFilePath, bool restartApp)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "ChatCaster-updater.bat");
        
        var script = $@"@echo off
timeout /t 3 /nobreak > nul
move ""{updateFilePath}"" ""{currentExePath}""
{(restartApp ? $"start \"\" \"{currentExePath}\"" : "")}
del ""{scriptPath}""
";

        File.WriteAllText(scriptPath, script);
        return scriptPath;
    }

    private async Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    #endregion

    #region GitHub API Models

    private class GitHubRelease
    {
        public string TagName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public bool PreRelease { get; set; }
        public GitHubAsset[]? Assets { get; set; }
    }

    private class GitHubAsset
    {
        public string Name { get; set; } = string.Empty;
        public string BrowserDownloadUrl { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _httpClient?.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}