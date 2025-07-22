namespace ChatCaster.Core.Updates;

/// <summary>
/// Информация о доступном обновлении
/// </summary>
public class UpdateInfo
{
    /// <summary>
    /// Версия обновления
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// Название релиза
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Описание изменений (Release Notes)
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;
    
    /// <summary>
    /// Дата релиза (UTC)
    /// </summary>
    public DateTime ReleaseDate { get; set; }
    
    /// <summary>
    /// URL для скачивания обновления
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Размер файла обновления в байтах
    /// </summary>
    public long FileSizeBytes { get; set; }
    
    /// <summary>
    /// Является ли это предварительным релизом
    /// </summary>
    public bool IsPreRelease { get; set; }
    
    /// <summary>
    /// Хэш файла для проверки целостности (если доступен)
    /// </summary>
    public string? FileHash { get; set; }
    
    /// <summary>
    /// Является ли это критическим обновлением
    /// </summary>
    public bool IsCritical { get; set; }
    
    /// <summary>
    /// Проверяет, является ли эта версия новее указанной
    /// </summary>
    /// <param name="currentVersion">Текущая версия для сравнения</param>
    /// <returns>True, если это обновление новее</returns>
    public bool IsNewerThan(string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion) || string.IsNullOrWhiteSpace(Version))
            return false;
            
        try
        {
            var current = new Version(currentVersion);
            var update = new Version(Version);
            return update > current;
        }
        catch
        {
            // Если не удается распарсить версии, сравниваем как строки
            return string.Compare(Version, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
    
    /// <summary>
    /// Форматированный размер файла для отображения пользователю
    /// </summary>
    public string FormattedFileSize
    {
        get
        {
            if (FileSizeBytes < 1024)
                return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024)
                return $"{FileSizeBytes / 1024.0:F1} KB";
            if (FileSizeBytes < 1024 * 1024 * 1024)
                return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
            
            return $"{FileSizeBytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }
}