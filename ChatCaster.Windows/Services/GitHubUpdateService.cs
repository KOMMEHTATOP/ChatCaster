using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using ChatCaster.Core.Updates;
using ChatCaster.Core.Models;
using ChatCaster.Core.Constants;
using Serilog;
using System.Text.Json.Serialization;

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

    public async Task<UpdateResult> CheckForUpdatesAsync(string currentVersion, bool includePreReleases = false, CancellationToken cancellationToken = default)
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
            var updateFiles = Directory.GetFiles(tempPath, "ChatCaster-update*.zip");

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

        _logger.Information("Запрашиваем URL: {Url}", url);

        var response = await _httpClient.GetStringAsync(url, cancellationToken);

        _logger.Information("Ответ GitHub API: {Response}", response.Length > 1000 ? response.Substring(0, 1000) + "..." : response);

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
        // Ищем Windows файл - поддерживаем как .exe, так и .zip
        var windowsAsset = release.Assets?.FirstOrDefault(a => 
            a.Name.StartsWith(UpdateConstants.WindowsExecutablePrefix) && 
            (a.Name.EndsWith(UpdateConstants.WindowsExecutableSuffix) || a.Name.EndsWith("-windows.zip")));

        _logger.Information("Ищем файл с префиксом: {Prefix}", UpdateConstants.WindowsExecutablePrefix);
        _logger.Information("Доступные файлы в релизе: {Assets}", 
            string.Join(", ", release.Assets?.Select(a => a.Name) ?? Array.Empty<string>()));

        if (windowsAsset == null)
        {
            _logger.Debug("Доступные файлы в релизе: {Assets}", 
                string.Join(", ", release.Assets?.Select(a => a.Name) ?? Array.Empty<string>()));
            return null;
        }

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
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
        
        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }
        
        [JsonPropertyName("prerelease")]
        public bool PreRelease { get; set; }
        
        [JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("size")]
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