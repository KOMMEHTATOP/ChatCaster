using ChatCaster.Core.Models;
using Serilog;

namespace ChatCaster.Windows.Managers.MainPage
{
    /// <summary>
    /// Менеджер для управления статусами записи
    /// Централизует логику форматирования статусов и состояний UI
    /// </summary>
    public class RecordingStatusManager
    {
        /// <summary>
        /// Форматирует статус записи в человеческий текст
        /// </summary>
        public string FormatRecordingStatus(RecordingStatus status, string? reason = null)
        {
            var statusText = status switch
            {
                RecordingStatus.Idle => "Готов к записи",
                RecordingStatus.Recording => "Идет запись...",
                RecordingStatus.Processing => "Обработка...",
                RecordingStatus.Completed => "Готов к записи", // ✅ ДОБАВЛЕНО
                RecordingStatus.Error => $"Ошибка: {reason}",
                _ => "Неизвестный статус"
            };

            Log.Debug("RecordingStatusManager: статус форматирован: {Status} -> {Text}", status, statusText);
            return statusText;
        }

        /// <summary>
        /// Определяет текст кнопки записи по статусу
        /// </summary>
        public string GetRecordButtonText(RecordingStatus status)
        {
            var buttonText = status switch
            {
                RecordingStatus.Recording => "Остановить",
                RecordingStatus.Processing => "Обработка...",
                RecordingStatus.Completed => "Записать", // ✅ ДОБАВЛЕНО
                _ => "Записать"
            };

            Log.Debug("RecordingStatusManager: текст кнопки: {Status} -> {ButtonText}", status, buttonText);
            return buttonText;
        }

        /// <summary>
        /// Определяет цвет статуса по статусу записи
        /// </summary>
        public string GetStatusColor(RecordingStatus status)
        {
            var color = status switch
            {
                RecordingStatus.Idle => "#4caf50",      // Зеленый
                RecordingStatus.Recording => "#ff9800", // Оранжевый
                RecordingStatus.Processing => "#2196f3", // Синий
                RecordingStatus.Completed => "#4caf50",  // Зеленый ✅ ДОБАВЛЕНО
                RecordingStatus.Error => "#f44336",     // Красный
                _ => "#9e9e9e"                          // Серый
            };

            return color;
        }

        /// <summary>
        /// Проверяет, идет ли сейчас запись
        /// </summary>
        public bool IsRecordingActive(RecordingStatus status)
        {
            return status == RecordingStatus.Recording;
        }

        /// <summary>
        /// Проверяет, можно ли начать новую запись
        /// </summary>
        public bool CanStartRecording(RecordingStatus status)
        {
            return status == RecordingStatus.Idle;
        }

        /// <summary>
        /// Проверяет, можно ли остановить запись
        /// </summary>
        public bool CanStopRecording(RecordingStatus status)
        {
            return status == RecordingStatus.Recording;
        }

        /// <summary>
        /// Создает информацию о состоянии записи
        /// </summary>
        public RecordingStateInfo CreateStateInfo(RecordingStatus status, string? reason = null)
        {
            return new RecordingStateInfo
            {
                Status = status,
                StatusText = FormatRecordingStatus(status, reason),
                ButtonText = GetRecordButtonText(status),
                StatusColor = GetStatusColor(status),
                IsRecording = IsRecordingActive(status),
                CanStartRecording = CanStartRecording(status),
                CanStopRecording = CanStopRecording(status)
            };
        }
    }

    #region Helper Classes

    /// <summary>
    /// Информация о состоянии записи для UI
    /// </summary>
    public class RecordingStateInfo
    {
        public RecordingStatus Status { get; set; }
        public string StatusText { get; set; } = "";
        public string ButtonText { get; set; } = "";
        public string StatusColor { get; set; } = "";
        public bool IsRecording { get; set; }
        public bool CanStartRecording { get; set; }
        public bool CanStopRecording { get; set; }
    }

    #endregion
}