using System.Collections.ObjectModel;
using ChatCaster.Core.Events;
using ChatCaster.Core.Services.System;
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
        private readonly ILocalizationService _localizationService;

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

        public RecognitionResultsComponentViewModel(ILocalizationService localizationService)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            
            // Подписываемся на смену языка
            _localizationService.LanguageChanged += OnLanguageChanged;
            
            SetInitialState();
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

            // Устанавливаем метрики (локализованные)
            ConfidenceText = _localizationService.GetString("Main_ConfidenceHigh");
            ProcessingTimeText = _localizationService.GetString("Main_ProcessingTimeFast");

            // Уведомляем родительскую ViewModel
            TextRecognized?.Invoke(recognizedText);
        }

        /// <summary>
        /// Обрабатывает ошибку распознавания
        /// </summary>
        private void HandleRecognitionError(string? errorMessage)
        {
            ResultText = _localizationService.GetString("Main_RecognitionFailed");
            ResultTextBrush = System.Windows.Media.Brushes.Red;
            ResultFontStyle = System.Windows.FontStyles.Italic;

            // Очищаем метрики при ошибке
            ConfidenceText = "";
            ProcessingTimeText = "";
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
                ResultText = _localizationService.GetString("Main_PlaceholderText");
                ResultTextBrush = System.Windows.Media.Brushes.Gray;
                ResultFontStyle = System.Windows.FontStyles.Italic;
                LastRecognizedText = string.Empty;
                ConfidenceText = "";
                ProcessingTimeText = "";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecognitionResultsComponent: ошибка установки начального состояния");
            }
        }

        /// <summary>
        /// Обновляет локализацию при смене языка
        /// </summary>
        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            try
            {
                // Если текст еще не был заменен (показывается placeholder), обновляем его
                if (ResultText == _localizationService.GetString("Main_PlaceholderText") || 
                    ResultText == "Здесь появится распознанный текст..." || 
                    ResultText == "Recognition result will appear here...")
                {
                    ResultText = _localizationService.GetString("Main_PlaceholderText");
                }

                // Если показывается ошибка, обновляем ее
                if (ResultText == _localizationService.GetString("Main_RecognitionFailed") ||
                    ResultText == "Не удалось распознать речь" ||
                    ResultText == "Speech recognition failed")
                {
                    ResultText = _localizationService.GetString("Main_RecognitionFailed");
                }

                Log.Debug("RecognitionResultsComponent: обновлена локализация");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecognitionResultsComponent: ошибка обновления локализации");
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
                _localizationService.LanguageChanged -= OnLanguageChanged;
                RecentRecognitions.Clear();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecognitionResultsComponent: ошибка освобождения ресурсов");
            }
        }
    }
}