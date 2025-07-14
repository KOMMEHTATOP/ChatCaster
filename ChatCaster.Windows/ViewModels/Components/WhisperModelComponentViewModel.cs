using System.Collections.ObjectModel;
using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.Windows.Managers.AudioSettings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Windows;

namespace ChatCaster.Windows.ViewModels.Components
{
    /// <summary>
    /// Компонент для управления моделями Whisper
    /// Содержит UI логику для выбора и загрузки моделей
    /// </summary>
    public partial class WhisperModelComponentViewModel : ObservableObject
    {
        private readonly WhisperModelManager _whisperModelManager;
        public WhisperModelManager ModelManager => _whisperModelManager;


        [ObservableProperty]
        private WhisperModelItem? _selectedModel;

        [ObservableProperty]
        private string _selectedLanguage = "ru";

        [ObservableProperty]
        private bool _isModelReady;

        [ObservableProperty]
        private bool _isModelNotReady = true;

        [ObservableProperty]
        private bool _isModelDownloading;

        [ObservableProperty]
        private string _modelStatusText = "Модель готова";

        [ObservableProperty]
        private string _modelStatusColor = "#4caf50";

        /// <summary>
        /// Коллекция доступных моделей Whisper для привязки к UI
        /// </summary>
        public ObservableCollection<WhisperModelItem> AvailableModels { get; } = new();

        /// <summary>
        /// Топ-5 языков Steam для Whisper
        /// </summary>
        public List<LanguageItem> AvailableLanguages { get; } = new()
        {
            new LanguageItem { Code = "en", Name = "🇺🇸 English" },
            new LanguageItem { Code = "zh", Name = "🇨🇳 简体中文" },
            new LanguageItem { Code = "ru", Name = "🇷🇺 Русский" },
            new LanguageItem { Code = "es", Name = "🇪🇸 Español" },
            new LanguageItem { Code = "de", Name = "🇩🇪 Deutsch" }
        };

        // События для связи с родительской ViewModel
        public event Func<Task>? ModelChanged;
        public event Func<Task>? LanguageChanged;

        public WhisperModelComponentViewModel(WhisperModelManager whisperModelManager)
        {
            _whisperModelManager = whisperModelManager ?? throw new ArgumentNullException(nameof(whisperModelManager));
            
            Log.Debug("WhisperModelComponentViewModel инициализирован");
        }

        
        /// <summary>
        /// Устанавливает выбранную модель из конфигурации
        /// </summary>
        public void SetSelectedModelFromConfig(string? modelSize)
        {
            var targetModelSize = modelSize ?? WhisperConstants.ModelSizes.Tiny;
    
            Log.Information("WhisperModelComponent ищем модель: {ModelSize} в коллекции из {Count} элементов", 
                targetModelSize, AvailableModels.Count);
    
            // Выводим все доступные модели для диагностики
            foreach (var model in AvailableModels)
            {
                Log.Debug("Доступная модель: {ModelSize} ({DisplayName})", model.ModelSize, model.DisplayName);
            }
    
            SelectedModel = AvailableModels.FirstOrDefault(m => m.ModelSize == targetModelSize);
    
            if (SelectedModel != null)
            {
                Log.Information("WhisperModelComponent модель установлена: {ModelSize} -> {DisplayName}", 
                    targetModelSize, SelectedModel.DisplayName);
            }
            else
            {
                Log.Warning("WhisperModelComponent модель НЕ НАЙДЕНА: {ModelSize}, выбираем первую доступную", targetModelSize);
                SelectedModel = AvailableModels.FirstOrDefault();
        
                if (SelectedModel != null)
                {
                    Log.Information("WhisperModelComponent выбрана первая модель: {DisplayName}", SelectedModel.DisplayName);
                }
            }
        }
        
        /// <summary>
        /// Проверяет статус текущей модели
        /// </summary>
        public async Task CheckModelStatusAsync()
        {
            try
            {
                var statusInfo = await _whisperModelManager.CheckModelStatusAsync();
                UpdateModelStatus(statusInfo);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка проверки статуса модели в WhisperModelComponent");
                UpdateModelStatus(new ModelStatusInfo("Ошибка проверки", "#f44336", ModelState.Error));
            }
        }
        
        [RelayCommand]
        private async Task DownloadModel()
        {
            if (SelectedModel == null)
            {
                Log.Warning("WhisperModelComponent модель не выбрана для загрузки");
                return;
            }

            try
            {
                Log.Information("WhisperModelComponent начинаем загрузку модели: {Model}", SelectedModel.ModelSize);
                
                UpdateModelStatus(new ModelStatusInfo("Загрузка...", "#ff9800", ModelState.Downloading));

                var statusInfo = await _whisperModelManager.DownloadModelAsync(SelectedModel.ModelSize);
                UpdateModelStatus(statusInfo);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки модели в WhisperModelComponent");
                UpdateModelStatus(new ModelStatusInfo("Ошибка загрузки", "#f44336", ModelState.Error));
            }
        }

        /// <summary>
        /// Обновляет статус модели для UI
        /// </summary>
        private void UpdateModelStatus(ModelStatusInfo statusInfo)
        {
            ModelStatusText = statusInfo.StatusText;
            ModelStatusColor = statusInfo.StatusColor;
            
            // Обновляем состояния для видимости элементов
            IsModelReady = statusInfo.State == ModelState.Ready;
            IsModelNotReady = statusInfo.State == ModelState.NotDownloaded;
            IsModelDownloading = statusInfo.State == ModelState.Downloading;
            
            Log.Information("WhisperModelComponent UI статус модели обновлен: {Status}, состояние: {State}", 
                statusInfo.StatusText, statusInfo.State);
        }

        private void InitializeAvailableModels()
        {
        }

        /// <summary>
        /// Загружает модели с проверкой статуса загрузки
        /// </summary>
        public async Task LoadModelsWithStatusAsync()
        {
            try
            {
                // Получаем модели в фоновом потоке
                var models = await _whisperModelManager.GetAvailableModelsWithStatusAsync();
        
                // Обновляем коллекцию в UI потоке
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AvailableModels.Clear();
            
                    foreach (var model in models)
                    {
                        AvailableModels.Add(model);
                    }
                });
        
                Log.Information("WhisperModelComponent загружено {Count} моделей со статусами", AvailableModels.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки моделей со статусами");
            }
        }

        
        partial void OnSelectedModelChanged(WhisperModelItem? value)
        {
            Log.Information("WhisperModelComponent модель изменена: {Model} ({DisplayName})",
                value?.ModelSize, value?.DisplayName);

            // Проверяем статус новой модели при изменении
            _ = Task.Run(async () =>
            {
                try
                {
                    await CheckModelStatusAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка проверки статуса модели при изменении");
                }
            });

            // Уведомляем родительскую ViewModel
            ModelChanged?.Invoke();
        }

        partial void OnSelectedLanguageChanged(string value)
        {
            Log.Information("WhisperModelComponent язык изменен: {Language}", value);
            
            // Уведомляем родительскую ViewModel
            LanguageChanged?.Invoke();
        }
    }

    #region Helper Classes

    public class LanguageItem
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
    }

    #endregion
}