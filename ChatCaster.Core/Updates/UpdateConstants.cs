namespace ChatCaster.Core.Updates;

/// <summary>
/// Константы для системы обновлений
/// </summary>
public static class UpdateConstants
{
    /// <summary>
    /// URL GitHub API для проверки последнего релиза
    /// </summary>
    public const string GitHubReleasesApiUrl = "https://api.github.com/repos/KOMMEHTATOP/ChatCaster/releases/latest";
    
    /// <summary>
    /// Префикс для Windows исполняемого файла в релизах
    /// </summary>
    public const string WindowsExecutablePrefix = "ChatCaster-v";
    
    /// <summary>
    /// Суффикс для Windows исполняемого файла в релизах
    /// </summary>
    public const string WindowsExecutableSuffix = "-windows.exe";
    
    /// <summary>
    /// Максимальный размер файла обновления в байтах (100 MB)
    /// </summary>
    public const long MaxUpdateFileSizeBytes = 100 * 1024 * 1024;
    
    /// <summary>
    /// Таймаут для HTTP запросов при проверке/скачивании обновлений
    /// </summary>
    public const int HttpTimeoutSeconds = 30;
    
    /// <summary>
    /// Интервал между проверками обновлений по умолчанию (в часах)
    /// </summary>
    public const int DefaultCheckIntervalHours = 24;
    
    /// <summary>
    /// Имя файла обновлений (временный)
    /// </summary>
    public const string UpdateFileName = "ChatCaster-update.zip";
    
    /// <summary>
    /// Имя внешнего обновляющего приложения
    /// </summary>
    public const string UpdaterExecutableName = "ChatCaster.Updater.exe";
}
