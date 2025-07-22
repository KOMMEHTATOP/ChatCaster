namespace ChatCaster.Core.Updates;

/// <summary>
/// Результат операции обновления
/// </summary>
public class UpdateResult
{
    /// <summary>
    /// Успешность операции
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Сообщение об ошибке (если операция неуспешна)
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Тип результата операции
    /// </summary>
    public UpdateResultType ResultType { get; set; }
    
    /// <summary>
    /// Информация об обновлении (если доступна)
    /// </summary>
    public UpdateInfo? UpdateInfo { get; set; }
    
    /// <summary>
    /// Путь к скачанному файлу обновления (если операция - скачивание)
    /// </summary>
    public string? DownloadedFilePath { get; set; }
    
    /// <summary>
    /// Процент выполнения операции (0-100)
    /// </summary>
    public int ProgressPercentage { get; set; }
    
    /// <summary>
    /// Дополнительные данные операции
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
    
    /// <summary>
    /// Создает успешный результат
    /// </summary>
    public static UpdateResult Success(UpdateResultType resultType, UpdateInfo? updateInfo = null, string? filePath = null)
    {
        return new UpdateResult
        {
            IsSuccess = true,
            ResultType = resultType,
            UpdateInfo = updateInfo,
            DownloadedFilePath = filePath,
            ProgressPercentage = 100
        };
    }
    
    /// <summary>
    /// Создает результат с ошибкой
    /// </summary>
    public static UpdateResult Failure(UpdateResultType resultType, string errorMessage)
    {
        return new UpdateResult
        {
            IsSuccess = false,
            ResultType = resultType,
            ErrorMessage = errorMessage,
            ProgressPercentage = 0
        };
    }
    
    /// <summary>
    /// Создает результат для отсутствия обновлений
    /// </summary>
    public static UpdateResult NoUpdatesAvailable()
    {
        return new UpdateResult
        {
            IsSuccess = true,
            ResultType = UpdateResultType.NoUpdatesAvailable,
            ProgressPercentage = 100
        };
    }
    
    /// <summary>
    /// Создает результат прогресса операции
    /// </summary>
    public static UpdateResult Progress(UpdateResultType resultType, int percentage, UpdateInfo? updateInfo = null)
    {
        return new UpdateResult
        {
            IsSuccess = true,
            ResultType = resultType,
            UpdateInfo = updateInfo,
            ProgressPercentage = Math.Max(0, Math.Min(100, percentage))
        };
    }
}

/// <summary>
/// Типы результатов операций обновления
/// </summary>
public enum UpdateResultType
{
    /// <summary>
    /// Проверка обновлений завершена
    /// </summary>
    CheckCompleted,
    
    /// <summary>
    /// Доступно новое обновление
    /// </summary>
    UpdateAvailable,
    
    /// <summary>
    /// Обновления отсутствуют
    /// </summary>
    NoUpdatesAvailable,
    
    /// <summary>
    /// Скачивание обновления завершено
    /// </summary>
    DownloadCompleted,
    
    /// <summary>
    /// Скачивание в процессе
    /// </summary>
    DownloadInProgress,
    
    /// <summary>
    /// Обновление применено успешно
    /// </summary>
    UpdateApplied,
    
    /// <summary>
    /// Ошибка при проверке обновлений
    /// </summary>
    CheckError,
    
    /// <summary>
    /// Ошибка при скачивании
    /// </summary>
    DownloadError,
    
    /// <summary>
    /// Ошибка при применении обновления
    /// </summary>
    ApplyError,
    
    /// <summary>
    /// Операция отменена пользователем
    /// </summary>
    Cancelled
}