using System.Collections.ObjectModel;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels.Settings.Audio;
using Serilog;

namespace ChatCaster.Windows.ViewModels.Settings.Speech
{
    /// <summary>
    /// Менеджер для управления моделями Whisper
    /// </summary>
    public class WhisperModelManager
    {
        #region Events
        public event EventHandler<ModelStatusChangedEventArgs>? ModelStatusChanged;
        public event EventHandler<ModelDownloadButtonVisibilityChangedEventArgs>? DownloadButtonVisibilityChanged;
        #endregion

        #region Private Fields
        private readonly SpeechRecognitionService? _speechRecognitionService;
        private bool _isDownloadingModel = false;
        #endregion

        #region Public Properties
        public ObservableCollection<WhisperModelItem> AvailableModels { get; } = new();
        public ObservableCollection<LanguageItem> AvailableLanguages { get; } = new();
        public WhisperModelItem? SelectedModel { get; set; }
        public LanguageItem? SelectedLanguage { get; set; }
        public bool IsDownloadingModel => _isDownloadingModel;
        #endregion

        #region Constructor
        public WhisperModelManager(SpeechRecognitionService? speechRecognitionService)
        {
            Log.Information("[WMM] Конструктор вызван");
            _speechRecognitionService = speechRecognitionService;
            InitializeStaticData();
            Log.Debug("WhisperModelManager инициализирован");
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Проверяет статус выбранной модели
        /// </summary>
        public async Task CheckModelStatusAsync()
        {
            try
            {
                if (_speechRecognitionService == null || SelectedModel == null)
                {
                    Log.Warning("SpeechRecognitionService или SelectedModel недоступны при проверке статуса модели");
                    return;
                }

                // ✅ ДИАГНОСТИКА: Логируем процесс проверки
                Log.Information("=== ПРОВЕРКА СТАТУСА МОДЕЛИ ===");
                Log.Information("Выбранная модель в UI: {UIModel} ({DisplayName})", SelectedModel.Model, SelectedModel.DisplayName);
                Log.Information("Модель в SpeechRecognition: {ServiceModel}", _speechRecognitionService.CurrentModel);
                Log.Information("Инициализирован ли сервис: {IsInitialized}", _speechRecognitionService.IsInitialized);

                bool isAvailable = await _speechRecognitionService.IsModelAvailableAsync(SelectedModel.Model);
                Log.Information("Модель доступна на диске: {IsAvailable}", isAvailable);

                if (isAvailable)
                {
                    // ✅ КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: Проверяем нужна ли переинициализация
                    if (!_speechRecognitionService.IsInitialized || 
                        _speechRecognitionService.CurrentModel != SelectedModel.Model)
                    {
                        Log.Warning("НУЖНА ПЕРЕИНИЦИАЛИЗАЦИЯ! Инициализируем модель {Model}", SelectedModel.Model);
                        
                        var config = new WhisperConfig { Model = SelectedModel.Model };
                        bool initResult = await _speechRecognitionService.InitializeAsync(config);
                        
                        Log.Information("Результат переинициализации: {InitResult}", initResult);
                        
                        if (initResult)
                        {
                            Log.Information("✅ Модель {Model} успешно загружена в сервис", SelectedModel.Model);
                        }
                        else
                        {
                            Log.Error("❌ Не удалось загрузить модель {Model} в сервис", SelectedModel.Model);
                            RaiseModelStatusChanged("Ошибка загрузки модели", "#f44336");
                            return;
                        }
                    }
                    else
                    {
                        Log.Information("✅ Нужная модель уже инициализирована");
                    }

                    RaiseModelStatusChanged("Модель готова", "#4caf50");
                    RaiseDownloadButtonVisibilityChanged(false);
                    Log.Debug("Модель {ModelName} доступна", SelectedModel.DisplayName);
                }
                else
                {
                    long sizeBytes = await _speechRecognitionService.GetModelSizeAsync(SelectedModel.Model);
                    string sizeText = FormatFileSize(sizeBytes);
                    RaiseModelStatusChanged($"Модель не скачана ({sizeText})", "#ff9800");
                    RaiseDownloadButtonVisibilityChanged(true);
                    Log.Debug("Модель {ModelName} недоступна, размер: {Size}", SelectedModel.DisplayName, sizeText);
                }
                
                Log.Information("=== ПРОВЕРКА ЗАВЕРШЕНА ===");
            }
            catch (Exception ex)
            {
                RaiseModelStatusChanged($"Ошибка проверки модели: {ex.Message}", "#f44336");
                Log.Error(ex, "Ошибка при проверке статуса модели {ModelName}", SelectedModel?.DisplayName ?? "Unknown");
            }
        }
        /// <summary>
        /// Загружает выбранную модель
        /// </summary>
        public async Task DownloadModelAsync()
        {
            if (_isDownloadingModel || _speechRecognitionService == null || SelectedModel == null) return;

            try
            {
                _isDownloadingModel = true;
                RaiseModelStatusChanged("Начинаем загрузку...", "#ff9800");
                Log.Debug("Начинаем загрузку модели: {ModelName}", SelectedModel.DisplayName);

                // Подписываемся на события загрузки
                _speechRecognitionService.DownloadProgress += OnModelDownloadProgress;
                _speechRecognitionService.DownloadCompleted += OnModelDownloadCompleted;

                // Инициализируем модель (это запустит загрузку если нужно)
                var config = new WhisperConfig { Model = SelectedModel.Model };
                await _speechRecognitionService.InitializeAsync(config);
            }
            catch (Exception ex)
            {
                RaiseModelStatusChanged($"Ошибка загрузки: {ex.Message}", "#f44336");
                Log.Error(ex, "Ошибка при загрузке модели {ModelName}", SelectedModel?.DisplayName ?? "Unknown");
                _isDownloadingModel = false;
            }
        }

        /// <summary>
        /// Находит модель по enum значению
        /// </summary>
        public WhisperModelItem? FindModelByEnum(WhisperModel model)
        {
            return AvailableModels.FirstOrDefault(m => m.Model == model);
        }

        /// <summary>
        /// Находит язык по коду
        /// </summary>
        public LanguageItem? FindLanguageByCode(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode)) return null;
            return AvailableLanguages.FirstOrDefault(l => l.Code == languageCode);
        }

        /// <summary>
        /// Получает конфигурацию для Whisper
        /// </summary>
        public WhisperConfig GetWhisperConfig()
        {
            return new WhisperConfig
            {
                Model = SelectedModel?.Model ?? WhisperModel.Base,
                Language = SelectedLanguage?.Code ?? "ru"
            };
        }

        /// <summary>
        /// Очистка ресурсов
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // Отписываемся от событий загрузки модели если подписаны
                if (_speechRecognitionService != null)
                {
                    _speechRecognitionService.DownloadProgress -= OnModelDownloadProgress;
                    _speechRecognitionService.DownloadCompleted -= OnModelDownloadCompleted;
                }
                Log.Debug("WhisperModelManager cleanup завершен");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при cleanup WhisperModelManager");
            }
        }

        #endregion

        #region Private Methods

        private void InitializeStaticData()
        {
            Log.Information("[WMM] InitializeStaticData вызван");

            // Инициализируем доступные модели Whisper
            AvailableModels.Clear();
            var models = WhisperModelFactory.CreateAvailableModels();
            foreach (var model in models)
            {
                AvailableModels.Add(model);
            }

            // Инициализируем доступные языки
            AvailableLanguages.Clear();
            AvailableLanguages.Add(new LanguageItem("ru", "🇷🇺 Русский"));
            AvailableLanguages.Add(new LanguageItem("en", "🇺🇸 English"));

            // Устанавливаем значения по умолчанию
            SelectedModel = WhisperModelFactory.GetDefaultModel();
            SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == "ru");
            Log.Information("[WMM] AvailableModels после инициализации: " + string.Join(", ", AvailableModels.Select(m => m.Model)));

        }

        private void OnModelDownloadProgress(object? sender, ModelDownloadProgressEvent e)
        {
            RaiseModelStatusChanged($"Загрузка {e.ProgressPercentage}%...", "#ff9800");
        }

        private void OnModelDownloadCompleted(object? sender, ModelDownloadCompletedEvent e)
        {
            // Отписываемся от событий
            if (_speechRecognitionService != null)
            {
                _speechRecognitionService.DownloadProgress -= OnModelDownloadProgress;
                _speechRecognitionService.DownloadCompleted -= OnModelDownloadCompleted;
            }

            if (e.Success)
            {
                RaiseModelStatusChanged("Модель готова", "#4caf50");
                RaiseDownloadButtonVisibilityChanged(false);
                Log.Information("Загрузка модели успешно завершена");
            }
            else
            {
                RaiseModelStatusChanged($"Ошибка загрузки: {e.ErrorMessage}", "#f44336");
                Log.Error("Ошибка загрузки модели: {ErrorMessage}", e.ErrorMessage);
            }

            _isDownloadingModel = false;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes >= 1_073_741_824) // GB
                return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) // MB
                return $"{bytes / 1_048_576.0:F0} MB";
            if (bytes >= 1024) // KB
                return $"{bytes / 1024.0:F0} KB";
            return $"{bytes} bytes";
        }

        private void RaiseModelStatusChanged(string status, string colorHex)
        {
            ModelStatusChanged?.Invoke(this, new ModelStatusChangedEventArgs(status, colorHex));
        }

        private void RaiseDownloadButtonVisibilityChanged(bool isVisible)
        {
            DownloadButtonVisibilityChanged?.Invoke(this, new ModelDownloadButtonVisibilityChangedEventArgs(isVisible));
        }

        #endregion
    }

    #region Event Args
    /// <summary>
    /// Аргументы события изменения статуса модели
    /// </summary>
    public class ModelStatusChangedEventArgs : EventArgs
    {
        public string Status { get; }
        public string ColorHex { get; }

        public ModelStatusChangedEventArgs(string status, string colorHex)
        {
            Status = status ?? throw new ArgumentNullException(nameof(status));
            ColorHex = colorHex ?? throw new ArgumentNullException(nameof(colorHex));
        }
    }

    /// <summary>
    /// Аргументы события изменения видимости кнопки загрузки
    /// </summary>
    public class ModelDownloadButtonVisibilityChangedEventArgs : EventArgs
    {
        public bool IsVisible { get; }

        public ModelDownloadButtonVisibilityChangedEventArgs(bool isVisible)
        {
            IsVisible = isVisible;
        }
    }
    #endregion
}