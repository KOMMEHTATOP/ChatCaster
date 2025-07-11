using System.Collections.ObjectModel;
using ChatCaster.Core.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace ChatCaster.Windows.ViewModels.Components
{
    /// <summary>
    /// Компонент для управления результатами распознавания речи
    /// Отвечает за отображение текста, истории и метрик распознавания
    /// </summary>
    public partial class RecognitionResultsComponentViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _lastRecognizedText = string.Empty;

        [ObservableProperty]
        private string _resultText = "Здесь появится распознанный текст...";

        [ObservableProperty]
        private string _confidenceText = "";

        [ObservableProperty]
        private string _processingTimeText = "";

        [ObservableProperty]
        private System.Windows.Media.Brush _resultTextBrush = System.Windows.Media.Brushes.Gray;

        [ObservableProperty]
        private System.Windows.FontStyle _resultFontStyle = System.Windows.FontStyles.Italic;

        [ObservableProperty]
        private ObservableCollection<string> _recentRecognitions = new();

        // События для связи с родительской ViewModel
        public event Action<string>? TextRecognized;

        public RecognitionResultsComponentViewModel()
        {
            SetInitialState();
            Log.Debug("RecognitionResultsComponentViewModel инициализирован");
        }

        /// <summary>
        /// Обрабатывает завершение распознавания голоса
        /// </summary>
        public void HandleRecognitionCompleted(VoiceRecognitionCompletedEvent e)
        {
            try
            {
                if (e.Result.Success && !string.IsNullOrEmpty(e.Result.RecognizedText))
                {
                    // Успешное распознавание
                    HandleSuccessfulRecognition(e.Result.RecognizedText);
                }
                else
                {
                    // Ошибка распознавания
                    HandleRecognitionError(e.Result.ErrorMessage);
                }

                Log.Information("RecognitionResultsComponent: обработано завершение распознавания, успех: {Success}", e.Result.Success);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecognitionResultsComponent: ошибка обработки результата распознавания");
                HandleRecognitionError($"Ошибка обработки: {ex.Message}");
            }
        }

        /// <summary>
        /// Обрабатывает успешное распознавание
        /// </summary>
        private void HandleSuccessfulRecognition(string recognizedText)
        {
            LastRecognizedText = recognizedText;
            ResultText = recognizedText;
            ResultTextBrush = System.Windows.Media.Brushes.White;
            ResultFontStyle = System.Windows.FontStyles.Normal;

            // Добавляем в историю
            AddToRecentRecognitions(recognizedText);

            // Устанавливаем метрики (можно расширить в будущем)
            ConfidenceText = "Точность: высокая";
            ProcessingTimeText = "Время: < 1с";

            // Уведомляем родительскую ViewModel
            TextRecognized?.Invoke(recognizedText);

            Log.Information("RecognitionResultsComponent: успешное распознавание: {Text}", recognizedText);
        }

        /// <summary>
        /// Обрабатывает ошибку распознавания
        /// </summary>
        private void HandleRecognitionError(string? errorMessage)
        {
            ResultText = "Не удалось распознать речь";
            ResultTextBrush = System.Windows.Media.Brushes.Red;
            ResultFontStyle = System.Windows.FontStyles.Italic;

            // Очищаем метрики при ошибке
            ConfidenceText = "";
            ProcessingTimeText = "";

            Log.Warning("RecognitionResultsComponent: ошибка распознавания: {Error}", errorMessage ?? "неизвестная ошибка");
        }

        /// <summary>
        /// Добавляет текст в историю распознаваний
        /// </summary>
        private void AddToRecentRecognitions(string text)
        {
            try
            {
                // Добавляем в начало списка
                RecentRecognitions.Insert(0, text);

                // Ограничиваем количество записей (макс. 10)
                while (RecentRecognitions.Count > 10)
                {
                    RecentRecognitions.RemoveAt(RecentRecognitions.Count - 1);
                }

                Log.Debug("RecognitionResultsComponent: добавлен текст в историю: {Text}", text);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecognitionResultsComponent: ошибка добавления текста в историю");
            }
        }

        /// <summary>
        /// Устанавливает начальное состояние
        /// </summary>
        public void SetInitialState()
        {
            try
            {
                ResultText = "Здесь появится распознанный текст...";
                ResultTextBrush = System.Windows.Media.Brushes.Gray;
                ResultFontStyle = System.Windows.FontStyles.Italic;
                LastRecognizedText = string.Empty;
                ConfidenceText = "";
                ProcessingTimeText = "";

                Log.Debug("RecognitionResultsComponent: установлено начальное состояние");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecognitionResultsComponent: ошибка установки начального состояния");
            }
        }

        /// <summary>
        /// Очищает историю распознаваний
        /// </summary>
        public void ClearHistory()
        {
            try
            {
                var count = RecentRecognitions.Count;
                RecentRecognitions.Clear();
                Log.Information("RecognitionResultsComponent: очищена история, удалено {Count} записей", count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecognitionResultsComponent: ошибка очистки истории");
            }
        }

        /// <summary>
        /// Получает последние N распознаваний
        /// </summary>
        public IEnumerable<string> GetRecentRecognitions(int count = 5)
        {
            try
            {
                return RecentRecognitions.Take(Math.Min(count, RecentRecognitions.Count));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecognitionResultsComponent: ошибка получения недавних распознаваний");
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// Очистка ресурсов
        /// </summary>
        public void Dispose()
        {
            try
            {
                RecentRecognitions.Clear();
                Log.Debug("RecognitionResultsComponent: ресурсы освобождены");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecognitionResultsComponent: ошибка освобождения ресурсов");
            }
        }
    }
}