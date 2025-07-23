using ChatCaster.Core.Models;

namespace ChatCaster.Core.Updates;

/// <summary>
/// Интерфейс сервиса обновлений приложения
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Проверяет наличие доступных обновлений
    /// </summary>
    /// <param name="currentVersion">Текущая версия приложения</param>
    /// <param name="includePreReleases">Включать ли предварительные релизы</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Результат проверки обновлений</returns>
    Task<UpdateResult> CheckForUpdatesAsync(
        string currentVersion, 
        bool includePreReleases = false, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Получает информацию о последней доступной версии без полной проверки
    /// </summary>
    /// <param name="includePreReleases">Включать ли предварительные релизы</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Информация о последней версии или null, если недоступна</returns>
    Task<UpdateInfo?> GetLatestVersionInfoAsync(
        bool includePreReleases = false, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Проверяет, нужно ли выполнить автоматическую проверку обновлений
    /// на основе настроек и времени последней проверки
    /// </summary>
    /// <param name="updateConfig">Конфигурация обновлений</param>
    /// <returns>True, если нужно проверить обновления</returns>
    bool ShouldCheckForUpdates(UpdateConfig updateConfig);
    
    /// <summary>
    /// Проверяет целостность скачанного файла обновления
    /// </summary>
    /// <param name="filePath">Путь к файлу для проверки</param>
    /// <param name="expectedHash">Ожидаемый хэш файла (если доступен)</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>True, если файл корректен</returns>
    Task<bool> ValidateUpdateFileAsync(
        string filePath, 
        string? expectedHash = null, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Очищает временные файлы обновлений
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    Task CleanupTempFilesAsync(CancellationToken cancellationToken = default);
}