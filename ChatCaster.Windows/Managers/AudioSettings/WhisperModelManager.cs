using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using ChatCaster.SpeechRecognition.Whisper.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using System.IO;

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
        /// Инициализирует список доступных моделей со статусом загрузки
        /// </summary>
        public async Task<List<WhisperModelItem>> GetAvailableModelsWithStatusAsync()
        {
            var models = new List<WhisperModelItem>();
            
            foreach (var modelSize in WhisperConstants.ModelSizes.All)
            {
                var isDownloaded = await IsModelDownloadedAsync(modelSize);
                
                models.Add(new WhisperModelItem
                {
                    ModelSize = modelSize,
                    DisplayName = GetModelDisplayName(modelSize),
                    Description = GetModelDescription(modelSize),
                    IsDownloaded = isDownloaded,
                    StatusIcon = isDownloaded ? "✅" : "⬇️"
                });
            }
            
            Log.Information("WhisperModelManager инициализировано {Count} моделей со статусами", models.Count);
            return models;
        }

        /// <summary>
        /// Проверяет существует ли файл модели
        /// </summary>
        private async Task<bool> IsModelDownloadedAsync(string modelSize)
        {
            try
            {
                var modelPath = Path.Combine("models", $"ggml-{modelSize}.bin");
                return await Task.Run(() => File.Exists(modelPath));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка проверки модели {ModelSize}", modelSize);
                return false;
            }
        }
        
        /// <summary>
        /// Применяет текущую конфигурацию к речевому сервису без скачивания
        /// </summary>
        public async Task<bool> ApplyCurrentConfigAsync()
        {
            try
            {
                Log.Information("🔍 [MANAGER] Применяем текущую конфигурацию к речевому сервису");
        
                var speechConfig = _currentConfig.SpeechRecognition;
                var modelSize = speechConfig.EngineSettings.TryGetValue("ModelSize", out var model) 
                    ? model?.ToString() ?? "tiny" 
                    : "tiny";

                // Проверяем что модель скачана
                var isDownloaded = await IsModelDownloadedAsync(modelSize);
        
                if (!isDownloaded)
                {
                    Log.Warning("🔍 [MANAGER] Модель {ModelSize} НЕ СКАЧАНА, переключение пропущено", modelSize);
                    return false;
                }

                // Переключаемся только на скачанную модель
                var result = await _speechRecognitionService.ReloadConfigAsync(speechConfig);
        
                Log.Information("🔍 [MANAGER] Результат применения конфигурации: {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "🔍 [MANAGER] Ошибка применения конфигурации");
                return false;
            }
        }
        
        /// <summary>
        /// Проверяет статус выбранной модели
        /// </summary>
        public Task<ModelStatusInfo> CheckModelStatusAsync()
        {
            try
            {
                return Task.FromResult(_speechRecognitionService.IsInitialized ? new ModelStatusInfo("Модель готова", "#4caf50", ModelState.Ready) : new ModelStatusInfo("Модель не загружена", "#ff9800", ModelState.NotDownloaded));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка проверки статуса модели в WhisperModelManager");
                return Task.FromResult(new ModelStatusInfo("Ошибка проверки", "#f44336", ModelState.Error));
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
                WhisperConstants.ModelSizes.Tiny => "Tiny ⚡ (~76 MB)",
                WhisperConstants.ModelSizes.Base => "Base 🎯 (~144 MB)",
                WhisperConstants.ModelSizes.Small => "Small 💪 (~476 MB)",
                WhisperConstants.ModelSizes.Medium => "Medium 🧠 (~1.5 GB)",
                WhisperConstants.ModelSizes.Large => "Large 🚀 (~3.0 GB)",
                _ => modelSize
            };
        }

        private string GetModelDescription(string modelSize)
        {
            return modelSize switch
            {
                WhisperConstants.ModelSizes.Tiny => "Мгновенно, но тупой",
                WhisperConstants.ModelSizes.Base => "Терпимо, как борщ с пряником",
                WhisperConstants.ModelSizes.Small => "Уже сильно лучше, косяки редкие",
                WhisperConstants.ModelSizes.Medium => "Почти не ошибается, как математик",
                WhisperConstants.ModelSizes.Large => "УльтраМозг, но съест всю твою оперативку",
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

    public partial class WhisperModelItem : ObservableObject
    {
        [ObservableProperty]
        private string _modelSize = "";
    
        [ObservableProperty] 
        private string _displayName = "";
    
        [ObservableProperty]
        private string _description = "";
    
        [ObservableProperty]
        private bool _isDownloaded;
    
        [ObservableProperty]
        private string _statusIcon = "";
        
        /// <summary>
        /// Отображение в закрытом ComboBox - только иконка и название
        /// </summary>
        public override string ToString()
        {
            return $"{StatusIcon} {DisplayName}";
        }

    }

    #endregion
}