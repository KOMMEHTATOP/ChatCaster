using System.Collections.ObjectModel;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using Serilog;

namespace ChatCaster.Windows.ViewModels.Settings.Speech
{
    /// <summary>
    /// Менеджер для управления моделями Whisper (упрощенный - только модели)
    /// </summary>
    public class WhisperModelManager : IDisposable
    {

        #region Events

        public event EventHandler<ModelStatusChangedEventArgs>? ModelStatusChanged;
        public event EventHandler<ModelDownloadButtonStateChangedEventArgs>? DownloadButtonStateChanged;

        #endregion

        #region Private Fields

        private readonly SpeechRecognitionService? _speechRecognitionService;
        private bool _isDownloadingModel = false;
        private bool _isDisposed = false;

        #endregion

        #region Public Properties

        public ObservableCollection<WhisperModelItem> AvailableModels { get; } = new();
        public WhisperModelItem? SelectedModel { get; set; }
        public bool IsDownloadingModel => _isDownloadingModel;

        #endregion

        #region Constructor

        public WhisperModelManager(SpeechRecognitionService? speechRecognitionService)
        {
            Log.Information("[WMM] Конструктор вызван");
            _speechRecognitionService = speechRecognitionService;

            // ✅ ИСПРАВЛЕНИЕ: Подписываемся ТОЛЬКО на фоновые события (убрана двойная подписка)
            if (_speechRecognitionService != null)
            {
                _speechRecognitionService.DownloadProgress += OnDownloadProgress;
                _speechRecognitionService.DownloadCompleted += OnDownloadCompleted;
                Log.Information("[WMM] Подписались на события загрузки");
            }

            InitializeStaticData();
            Log.Information("[WMM] WhisperModelManager создан");
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
                    Log.Warning("[WMM] SpeechRecognitionService или SelectedModel недоступны при проверке статуса модели");
                    return;
                }

                Log.Information("[WMM] === ПРОВЕРКА СТАТУСА МОДЕЛИ ===");
                Log.Information("[WMM] Выбранная модель: {UIModel} ({DisplayName})", SelectedModel.Model,
                    SelectedModel.DisplayName);
                Log.Information("[WMM] Модель в сервисе: {ServiceModel}", _speechRecognitionService.CurrentModel);
                Log.Information("[WMM] Сервис инициализирован: {IsInitialized}", _speechRecognitionService.IsInitialized);

                bool isAvailable = await _speechRecognitionService.IsModelAvailableAsync(SelectedModel.Model);
                Log.Information("[WMM] Модель доступна на диске: {IsAvailable}", isAvailable);

                if (isAvailable)
                {
                    // ✅ ИСПРАВЛЕНО: Проверяем не блокирует ли другой процесс модель
                    if (!_speechRecognitionService.IsInitialized ||
                        _speechRecognitionService.CurrentModel != SelectedModel.Model)
                    {
                        // ✅ ИСПРАВЛЕНИЕ: Проверяем состояние загрузки
                        if (_isDownloadingModel)
                        {
                            Log.Information("[WMM] Модель еще скачивается, ждем завершения");
                            RaiseModelStatusChanged("Скачивается...", "#ff9800");
                            RaiseDownloadButtonStateChanged(
                                symbol: "ArrowClockwise24",
                                tooltip: "Скачивается...",
                                isEnabled: false,
                                appearance: "Secondary"
                            );
                            return;
                        }

                        Log.Information("[WMM] ПЕРЕИНИЦИАЛИЗАЦИЯ! Загружаем модель {Model}", SelectedModel.Model);

                        var config = new WhisperConfig
                        {
                            Model = SelectedModel.Model
                        };
                        bool initResult = await _speechRecognitionService.InitializeAsync(config);

                        if (initResult)
                        {
                            Log.Information("[WMM] ✅ Модель {Model} успешно загружена", SelectedModel.Model);
                            RaiseModelStatusChanged("Модель готова", "#4caf50");
                        }
                        else
                        {
                            Log.Error("[WMM] ❌ Не удалось загрузить модель {Model}", SelectedModel.Model);
                            RaiseModelStatusChanged("Ошибка загрузки модели", "#f44336");
                            RaiseDownloadButtonStateChanged(
                                symbol: "Warning24",
                                tooltip: "Ошибка загрузки",
                                isEnabled: false,
                                appearance: "Caution"
                            );
                            return;
                        }
                    }
                    else
                    {
                        Log.Information("[WMM] ✅ Модель уже загружена");
                        RaiseModelStatusChanged("Модель готова", "#4caf50");
                    }

                    // Кнопка показывает галочку - модель скачана
                    RaiseDownloadButtonStateChanged(
                        symbol: "CheckmarkCircle24",
                        tooltip: "Модель скачана",
                        isEnabled: false,
                        appearance: "Success"
                    );
                }
                else
                {
                    long sizeBytes = await _speechRecognitionService.GetModelSizeAsync(SelectedModel.Model);
                    string sizeText = FormatFileSize(sizeBytes);
                    RaiseModelStatusChanged($"Модель не скачана ({sizeText})", "#ff9800");

                    // Кнопка показывает стрелку загрузки - нужно скачать
                    RaiseDownloadButtonStateChanged(
                        symbol: "ArrowDownload24",
                        tooltip: $"Скачать модель ({sizeText})",
                        isEnabled: true,
                        appearance: "Primary"
                    );

                    Log.Information("[WMM] Модель {ModelName} недоступна, размер: {Size}", SelectedModel.DisplayName,
                        sizeText);
                }

                Log.Information("[WMM] === ПРОВЕРКА ЗАВЕРШЕНА ===");
            }
            catch (Exception ex)
            {
                RaiseModelStatusChanged($"Ошибка проверки: {ex.Message}", "#f44336");

                // При ошибке показываем кнопку с предупреждением
                RaiseDownloadButtonStateChanged(
                    symbol: "Warning24",
                    tooltip: $"Ошибка: {ex.Message}",
                    isEnabled: false,
                    appearance: "Caution"
                );

                Log.Error(ex, "[WMM] Ошибка при проверке статуса модели {ModelName}",
                    SelectedModel?.DisplayName ?? "Unknown");
            }
        }

        /// <summary>
        /// ✅ ИСПРАВЛЕНИЕ: Упрощенная загрузка модели без дублирования подписок
        /// </summary>
        public async Task DownloadModelAsync()
        {
            if (_isDownloadingModel || _speechRecognitionService == null || SelectedModel == null)
            {
                Log.Warning("[WMM] Загрузка невозможна: downloading={Downloading}, service={ServiceOK}, model={ModelOK}",
                    _isDownloadingModel, _speechRecognitionService != null, SelectedModel != null);
                return;
            }

            try
            {
                _isDownloadingModel = true;
                RaiseModelStatusChanged("Начинаем загрузку...", "#ff9800");

                // Кнопка показывает спиннер во время загрузки
                RaiseDownloadButtonStateChanged(
                    symbol: "ArrowClockwise24",
                    tooltip: "Загружается...",
                    isEnabled: false,
                    appearance: "Secondary"
                );

                Log.Information("[WMM] Начинаем загрузку модели: {ModelName}", SelectedModel.DisplayName);

                // ✅ ИСПРАВЛЕНИЕ: Убрана двойная подписка - используем только фоновые обработчики
                // События уже обрабатываются в OnDownloadProgress и OnDownloadCompleted

                // Инициализируем модель (это запустит загрузку если нужно)
                var config = new WhisperConfig
                {
                    Model = SelectedModel.Model
                };
                await _speechRecognitionService.InitializeAsync(config);

                // ✅ ИСПРАВЛЕНИЕ: Сбрасываем флаг после успешной инициализации
                _isDownloadingModel = false;
            }
            catch (Exception ex)
            {
                RaiseModelStatusChanged($"Ошибка загрузки: {ex.Message}", "#f44336");

                // При ошибке загрузки показываем кнопку повтора
                RaiseDownloadButtonStateChanged(
                    symbol: "ArrowDownload24",
                    tooltip: "Повторить загрузку",
                    isEnabled: true,
                    appearance: "Caution"
                );

                Log.Error(ex, "[WMM] Ошибка при загрузке модели {ModelName}", SelectedModel?.DisplayName ?? "Unknown");
                _isDownloadingModel = false;
            }
        }

        /// <summary>
        /// Находит модель по enum значению
        /// </summary>
        public WhisperModelItem? FindModelByEnum(WhisperModel model)
        {
            var result = AvailableModels.FirstOrDefault(m => m.Model == model);
            Log.Information("[WMM] FindModelByEnum({Model}) = {Result}", model, result?.DisplayName ?? "не найдено");
            return result;
        }

        /// <summary>
        /// ✅ ИСПРАВЛЕНИЕ: Реализован IDisposable
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                Cleanup();
                _isDisposed = true;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Инициализирует статические данные моделей
        /// </summary>
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

            // Устанавливаем модель по умолчанию
            SelectedModel = WhisperModelFactory.GetDefaultModel();

            Log.Information("[WMM] AvailableModels: {Models}",
                string.Join(", ", AvailableModels.Select(m => m.Model)));
            Log.Information("[WMM] SelectedModel по умолчанию: {Model}", SelectedModel.DisplayName);
        }

        /// <summary>
        /// ✅ ИСПРАВЛЕНИЕ: Единый обработчик событий загрузки (убрана дублирующая логика)
        /// </summary>
        private void OnDownloadProgress(object? sender, ModelDownloadProgressEvent e)
        {
            // Обновляем статус только если это текущая выбранная модель
            if (SelectedModel != null && SelectedModel.Model == e.Model)
            {
                RaiseModelStatusChanged($"Загрузка {e.ProgressPercentage}%...", "#ff9800");
                RaiseDownloadButtonStateChanged(
                    symbol: "ArrowClockwise24",
                    tooltip: $"Загрузка {e.ProgressPercentage}%...",
                    isEnabled: false,
                    appearance: "Secondary"
                );
                Log.Information("[WMM] Загрузка модели {Model}: {Progress}%", e.Model, e.ProgressPercentage);
            }
        }

        /// <summary>
        /// ✅ ИСПРАВЛЕНИЕ: Единый обработчик завершения загрузки
        /// </summary>
        private void OnDownloadCompleted(object? sender, ModelDownloadCompletedEvent e)
        {
            // Обновляем статус только если это текущая выбранная модель
            if (SelectedModel != null && SelectedModel.Model == e.Model)
            {
                if (e.Success)
                {
                    RaiseModelStatusChanged("Модель готова", "#4caf50");
                    RaiseDownloadButtonStateChanged(
                        symbol: "CheckmarkCircle24",
                        tooltip: "Модель скачана",
                        isEnabled: false,
                        appearance: "Success"
                    );
                    Log.Information("[WMM] ✅ Загрузка модели {Model} завершена успешно", e.Model);
                }
                else
                {
                    RaiseModelStatusChanged($"Ошибка загрузки: {e.ErrorMessage}", "#f44336");
                    RaiseDownloadButtonStateChanged(
                        symbol: "ArrowDownload24",
                        tooltip: "Повторить загрузку",
                        isEnabled: true,
                        appearance: "Caution"
                    );
                    Log.Error("[WMM] ❌ Загрузка модели {Model} не удалась: {Error}", e.Model, e.ErrorMessage);
                }

                // ✅ ИСПРАВЛЕНИЕ: Сбрасываем флаг загрузки в любом случае
                _isDownloadingModel = false;
            }
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

        private void RaiseDownloadButtonStateChanged(string symbol, string tooltip, bool isEnabled, string appearance)
        {
            DownloadButtonStateChanged?.Invoke(this,
                new ModelDownloadButtonStateChangedEventArgs(symbol, tooltip, isEnabled, appearance));
        }

        /// <summary>
        /// ✅ ИСПРАВЛЕНИЕ: Полная очистка ресурсов
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // Отписываемся от всех событий
                if (_speechRecognitionService != null)
                {
                    _speechRecognitionService.DownloadProgress -= OnDownloadProgress;
                    _speechRecognitionService.DownloadCompleted -= OnDownloadCompleted;
                    Log.Information("[WMM] Отписались от событий загрузки");
                }

                SelectedModel = null;
                _isDownloadingModel = false;
                Log.Information("[WMM] Cleanup завершен");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[WMM] Ошибка при cleanup");
            }
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
    /// Аргументы события изменения состояния кнопки загрузки
    /// </summary>
    public class ModelDownloadButtonStateChangedEventArgs : EventArgs
    {
        public string Symbol { get; }
        public string Tooltip { get; }
        public bool IsEnabled { get; }
        public string Appearance { get; }

        public ModelDownloadButtonStateChangedEventArgs(string symbol, string tooltip, bool isEnabled, string appearance)
        {
            Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
            Tooltip = tooltip ?? throw new ArgumentNullException(nameof(tooltip));
            IsEnabled = isEnabled;
            Appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        }
    }
}

#endregion