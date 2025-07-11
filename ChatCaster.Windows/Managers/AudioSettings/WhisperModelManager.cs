using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using ChatCaster.SpeechRecognition.Whisper.Constants;
using Serilog;

namespace ChatCaster.Windows.Managers.AudioSettings
{
    /// <summary>
    /// Менеджер для работы с моделями Whisper
    /// Централизует логику загрузки, проверки и управления моделями
    /// </summary>
    public class WhisperModelManager
    {
        private readonly ISpeechRecognitionService _speechRecognitionService;
        private readonly AppConfig _currentConfig;

        public WhisperModelManager(
            ISpeechRecognitionService speechRecognitionService,
            AppConfig currentConfig)
        {
            _speechRecognitionService = speechRecognitionService ?? throw new ArgumentNullException(nameof(speechRecognitionService));
            _currentConfig = currentConfig ?? throw new ArgumentNullException(nameof(currentConfig));
        }

        /// <summary>
        /// Инициализирует список доступных моделей Whisper
        /// </summary>
        public List<WhisperModelItem> GetAvailableModels()
        {
            var models = new List<WhisperModelItem>();
            
            foreach (var modelSize in WhisperConstants.ModelSizes.All)
            {
                models.Add(new WhisperModelItem
                {
                    ModelSize = modelSize,
                    DisplayName = GetModelDisplayName(modelSize),
                    Description = GetModelDescription(modelSize)
                });
            }
            
            Log.Information("WhisperModelManager инициализировано {Count} моделей", models.Count);
            return models;
        }

        /// <summary>
        /// Проверяет статус выбранной модели
        /// </summary>
        public async Task<ModelStatusInfo> CheckModelStatusAsync()
        {
            try
            {
                if (_speechRecognitionService.IsInitialized)
                {
                    return new ModelStatusInfo("Модель готова", "#4caf50", ModelState.Ready);
                }
                else
                {
                    return new ModelStatusInfo("Модель не загружена", "#ff9800", ModelState.NotDownloaded);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка проверки статуса модели в WhisperModelManager");
                return new ModelStatusInfo("Ошибка проверки", "#f44336", ModelState.Error);
            }
        }

        /// <summary>
        /// Загружает модель Whisper
        /// </summary>
        public async Task<ModelStatusInfo> DownloadModelAsync(string modelSize)
        {
            try
            {
                Log.Information("WhisperModelManager начинаем загрузку модели: {Model}", modelSize);
                
                // Обновляем конфигурацию и перезагружаем модуль
                var speechConfig = _currentConfig.SpeechRecognition;
                speechConfig.EngineSettings["ModelSize"] = modelSize;
                
                var result = await _speechRecognitionService.ReloadConfigAsync(speechConfig);
                
                if (result)
                {
                    Log.Information("WhisperModelManager модель успешно загружена: {Model}", modelSize);
                    return new ModelStatusInfo("Модель готова", "#4caf50", ModelState.Ready);
                }
                else
                {
                    Log.Error("WhisperModelManager ошибка загрузки модели: {Model}", modelSize);
                    return new ModelStatusInfo("Ошибка загрузки", "#f44336", ModelState.Error);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "WhisperModelManager исключение при загрузке модели: {Model}", modelSize);
                return new ModelStatusInfo("Ошибка загрузки", "#f44336", ModelState.Error);
            }
        }

        private string GetModelDisplayName(string modelSize)
        {
            return modelSize switch
            {
                WhisperConstants.ModelSizes.Tiny => "Tiny (быстрая)",
                WhisperConstants.ModelSizes.Base => "Base (рекомендуемая)",
                WhisperConstants.ModelSizes.Small => "Small (хорошая)",
                WhisperConstants.ModelSizes.Medium => "Medium (точная)",
                WhisperConstants.ModelSizes.Large => "Large (очень точная)",
                _ => modelSize
            };
        }

        private string GetModelDescription(string modelSize)
        {
            return modelSize switch
            {
                WhisperConstants.ModelSizes.Tiny => "~39 MB, быстро",
                WhisperConstants.ModelSizes.Base => "~142 MB, оптимально",
                WhisperConstants.ModelSizes.Small => "~466 MB, хорошо",
                WhisperConstants.ModelSizes.Medium => "~1.5 GB, точно",
                WhisperConstants.ModelSizes.Large => "~3.0 GB, очень точно",
                _ => "Неизвестная модель"
            };
        }
    }

    #region Helper Classes

    /// <summary>
    /// Информация о статусе модели
    /// </summary>
    public class ModelStatusInfo
    {
        public string StatusText { get; }
        public string StatusColor { get; }
        public ModelState State { get; }

        public ModelStatusInfo(string statusText, string statusColor, ModelState state)
        {
            StatusText = statusText;
            StatusColor = statusColor;
            State = state;
        }
    }

    /// <summary>
    /// Состояния модели для UI
    /// </summary>
    public enum ModelState
    {
        Ready,
        NotDownloaded,
        Downloading,
        Error
    }

    public class WhisperModelItem
    {
        public string ModelSize { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
    }

    #endregion
}