using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using ChatCaster.Windows.ViewModels.Base;
using ChatCaster.SpeechRecognition.Whisper.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Collections.ObjectModel;

namespace ChatCaster.Windows.ViewModels;

public partial class AudioSettingsViewModel : BaseSettingsViewModel
{
    #region Private Fields

    private readonly ISpeechRecognitionService _speechRecognitionService;
    private readonly IAudioCaptureService _audioService;

    #endregion

    #region Observable Properties with Immediate Apply

    [ObservableProperty]
    private List<AudioDevice> _availableDevices = new();

    [ObservableProperty]
    private AudioDevice? _selectedDevice;

    [ObservableProperty]
    private WhisperModelItem? _selectedModel;

    [ObservableProperty]
    private string _selectedLanguage = "ru";

    [ObservableProperty]
    private int _maxRecordingSeconds = 10;

    [ObservableProperty]
    private int _selectedSampleRate = 16000;

    [ObservableProperty]
    private bool _isModelReady = false;

    [ObservableProperty]
    private bool _isModelNotReady = true;

    [ObservableProperty]
    private bool _isModelDownloading = false;

    [ObservableProperty]
    private string _modelStatusText = "Модель готова";

    [ObservableProperty]
    private string _modelStatusColor = "#4caf50";

    #endregion

    #region Public Properties for UI Binding

    /// <summary>
    /// Коллекция доступных моделей Whisper для привязки к UI
    /// </summary>
    public ObservableCollection<WhisperModelItem> AvailableModels { get; } = new();

    /// <summary>
    /// Доступные частоты дискретизации
    /// </summary>
    public List<int> AvailableSampleRates { get; } = new()
    {
        8000,
        16000,
        22050,
        44100,
        48000
    };

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

    #endregion

    #region Commands

    [RelayCommand]
    private async Task DownloadModel()
    {
        await DownloadModelAsync();
    }

    #endregion

    #region Constructor

    public AudioSettingsViewModel(
        IConfigurationService configurationService,
        AppConfig currentConfig,
        ISpeechRecognitionService speechRecognitionService)
        : base(configurationService, currentConfig)
    {
        Log.Information("[AudioSettingsViewModel] Конструктор вызван (Whisper модуль)");

        _speechRecognitionService = speechRecognitionService ?? throw new ArgumentNullException(nameof(speechRecognitionService));
        
        // Инициализируем доступные модели
        InitializeAvailableModels();

        Log.Information("[AudioSettingsViewModel] создан с новым Whisper модулем");
    }

    // ✅ ДОПОЛНИТЕЛЬНЫЙ КОНСТРУКТОР с AudioService
    public AudioSettingsViewModel(
        IConfigurationService configurationService,
        AppConfig currentConfig,
        ISpeechRecognitionService speechRecognitionService,
        IAudioCaptureService audioService)
        : this(configurationService, currentConfig, speechRecognitionService)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
    }

    #endregion

    #region Observable Property Changed Handlers - IMMEDIATE APPLY

    /// <summary>
    /// Immediate Apply: Выбранное устройство изменено
    /// </summary>
    partial void OnSelectedDeviceChanged(AudioDevice? value)
    {
        if (IsLoadingUI) return;

        Log.Information("🔄 Устройство изменено: {DeviceName} ({DeviceId})",
            value?.Name ?? "не выбрано", value?.Id ?? "");

        // Immediate Apply
        _ = OnUISettingChangedAsync();
    }

    /// <summary>
    /// Immediate Apply: Модель Whisper изменена
    /// </summary>
    partial void OnSelectedModelChanged(WhisperModelItem? value)
    {
        if (IsLoadingUI) return;

        Log.Information("🔄 Модель изменена: {Model} ({DisplayName})",
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

        // Immediate Apply
        _ = OnUISettingChangedAsync();
    }

    /// <summary>
    /// Immediate Apply: Язык изменен
    /// </summary>
    partial void OnSelectedLanguageChanged(string value)
    {
        if (IsLoadingUI) return;

        Log.Information("🔄 Язык изменен: {Language}", value);

        // Immediate Apply
        _ = OnUISettingChangedAsync();
    }

    /// <summary>
    /// Immediate Apply: Время записи изменено
    /// </summary>
    partial void OnMaxRecordingSecondsChanged(int value)
    {
        if (IsLoadingUI) return;

        Log.Information("🔄 Время записи изменено: {Seconds}с", value);

        // Immediate Apply - ОСНОВНАЯ ФИЧА!
        _ = OnUISettingChangedAsync();
    }

    /// <summary>
    /// Immediate Apply: Частота дискретизации изменена
    /// </summary>
    partial void OnSelectedSampleRateChanged(int value)
    {
        if (IsLoadingUI) return;

        Log.Information("🔄 Частота дискретизации изменена: {SampleRate}Hz", value);

        // Immediate Apply
        _ = OnUISettingChangedAsync();
    }

    #endregion

    #region BaseSettingsViewModel Implementation

    protected override async Task LoadPageSpecificSettingsAsync()
    {
        Log.Information("[ДИАГНОСТИКА] LoadPageSpecificSettingsAsync НАЧАТ");

        try
        {
            Log.Information("[ДИАГНОСТИКА] Вызываем LoadAudioDevicesAsync()");
            await LoadAudioDevicesAsync();
            Log.Information("[ДИАГНОСТИКА] LoadAudioDevicesAsync ЗАВЕРШЕН");

            Log.Information("[ДИАГНОСТИКА] ПЕРЕД ApplyConfigToProperties()");
            ApplyConfigToProperties();
            Log.Information("[ДИАГНОСТИКА] ПОСЛЕ ApplyConfigToProperties()");

            // Проверяем статус модели после загрузки настроек
            Log.Information("[ДИАГНОСТИКА] Проверяем статус модели...");
            await CheckModelStatusAsync();
            Log.Information("[ДИАГНОСТИКА] Проверка статуса модели завершена");

            Log.Information("[ДИАГНОСТИКА] LoadPageSpecificSettingsAsync ЗАВЕРШЕН");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ДИАГНОСТИКА] ИСКЛЮЧЕНИЕ в LoadPageSpecificSettingsAsync");
        }
    }

    protected override async Task ApplySettingsToConfigAsync(AppConfig config)
    {
        try
        {
            Log.Information("[AudioSettingsViewModel] Применяем настройки к конфигурации...");

            // Применяем аудио настройки
            config.Audio.SelectedDeviceId = SelectedDevice?.Id ?? "";
            config.Audio.SampleRate = SelectedSampleRate;
            config.Audio.MaxRecordingSeconds = MaxRecordingSeconds;

            // Применяем Whisper настройки через EngineSettings
            config.SpeechRecognition.Language = SelectedLanguage;
            config.SpeechRecognition.EngineSettings["ModelSize"] = SelectedModel?.ModelSize ?? WhisperConstants.ModelSizes.Base;

            Log.Information(
                "[AudioSettingsViewModel] Настройки применены: Device={DeviceId}, Model={Model}, Language={Language}, Time={Seconds}s",
                config.Audio.SelectedDeviceId, SelectedModel?.ModelSize, config.SpeechRecognition.Language,
                config.Audio.MaxRecordingSeconds);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AudioSettingsViewModel] Ошибка применения настроек к конфигурации");
            throw;
        }
    }
    
    protected override async Task ApplySettingsToServicesAsync()
    {
        try
        {
            Log.Information("[AudioSettingsViewModel] Применяем настройки к сервисам...");

            // Устанавливаем активное устройство
            if (SelectedDevice != null)
            {
                await _audioService.SetActiveDeviceAsync(SelectedDevice.Id);
                Log.Information("[AudioSettingsViewModel] Активное устройство установлено: {DeviceName}",
                    SelectedDevice.Name);
            }

            // Переинициализируем Whisper модуль если модель изменена
            if (SelectedModel != null)
            {
                var speechConfig = _currentConfig.SpeechRecognition;
                speechConfig.EngineSettings["ModelSize"] = SelectedModel.ModelSize;
                
                var result = await _speechRecognitionService.ReloadConfigAsync(speechConfig);
                Log.Information("[AudioSettingsViewModel] Whisper модуль переинициализирован: {Success}, Модель: {Model}",
                    result, SelectedModel.DisplayName);
            }

            Log.Information("[AudioSettingsViewModel] Настройки применены к сервисам");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AudioSettingsViewModel] Ошибка применения настроек к сервисам");
            throw;
        }
    }

    public override void SubscribeToUIEvents()
    {
        // Observable свойства автоматически работают через XAML привязки
        Log.Information("[AudioSettingsViewModel] UI события обрабатываются через XAML привязки");
    }

    protected override void CleanupPageSpecific()
    {
        try
        {
            // Очистка если нужна
            Log.Debug("[AudioSettingsViewModel] Очистка завершена");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AudioSettingsViewModel] Ошибка очистки");
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Загружает модель Whisper
    /// </summary>
    public async Task DownloadModelAsync()
    {
        try
        {
            if (SelectedModel == null)
            {
                Log.Warning("Модель не выбрана для загрузки");
                return;
            }

            Log.Information("Начинаем загрузку модели: {Model}", SelectedModel.ModelSize);
            
            UpdateModelStatus("Загрузка...", "#ff9800", ModelState.Downloading);

            // Загрузка через новый Whisper модуль
            // Обновляем конфигурацию и перезагружаем модуль
            var speechConfig = _currentConfig.SpeechRecognition;
            speechConfig.EngineSettings["ModelSize"] = SelectedModel.ModelSize;
            
            var result = await _speechRecognitionService.ReloadConfigAsync(speechConfig);
            
            if (result)
            {
                UpdateModelStatus("Модель готова", "#4caf50", ModelState.Ready);
                Log.Information("Модель успешно загружена: {Model}", SelectedModel.ModelSize);
            }
            else
            {
                UpdateModelStatus("Ошибка загрузки", "#f44336", ModelState.Error);
                Log.Error("Ошибка загрузки модели: {Model}", SelectedModel.ModelSize);
            }
        }
        catch (Exception ex)
        {
            UpdateModelStatus("Ошибка загрузки", "#f44336", ModelState.Error);
            Log.Error(ex, "Ошибка загрузки модели");
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Инициализирует доступные модели Whisper
    /// </summary>
    private void InitializeAvailableModels()
    {
        AvailableModels.Clear();
        
        foreach (var modelSize in WhisperConstants.ModelSizes.All)
        {
            AvailableModels.Add(new WhisperModelItem
            {
                ModelSize = modelSize,
                DisplayName = GetModelDisplayName(modelSize),
                Description = GetModelDescription(modelSize)
            });
        }
        
        Log.Information("Инициализировано {Count} моделей Whisper", AvailableModels.Count);
    }

    /// <summary>
    /// Загружает список доступных аудио устройств
    /// </summary>
    private async Task LoadAudioDevicesAsync()
    {
        try
        {
            var devices = await _audioService.GetAvailableDevicesAsync();
            AvailableDevices = devices.ToList();

            Log.Information("[AudioSettingsViewModel] Загружено {Count} аудио устройств", AvailableDevices.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AudioSettingsViewModel] Ошибка загрузки аудио устройств");
            AvailableDevices = new List<AudioDevice>();
        }
    }

    /// <summary>
    /// Применяет настройки из конфига к Observable свойствам
    /// </summary>
    private void ApplyConfigToProperties()
    {
        try
        {
            Log.Information("[AudioSettingsViewModel] Применяем конфиг к свойствам...");

            // Временно отключаем IsLoadingUI чтобы обновить свойства
            var wasLoading = IsLoadingUI;
            IsLoadingUI = false;

            // Применяем аудио настройки
            MaxRecordingSeconds = _currentConfig.Audio.MaxRecordingSeconds;
            SelectedSampleRate = _currentConfig.Audio.SampleRate;

            // Находим и устанавливаем выбранное устройство
            if (!string.IsNullOrEmpty(_currentConfig.Audio.SelectedDeviceId))
            {
                SelectedDevice = AvailableDevices.FirstOrDefault(d => d.Id == _currentConfig.Audio.SelectedDeviceId);
                Log.Information("[AudioSettingsViewModel] Устройство из конфига: {DeviceId} -> {DeviceName}", 
                    _currentConfig.Audio.SelectedDeviceId, SelectedDevice?.Name ?? "не найдено");
            }

            // Если устройство не найдено ИЛИ пустое - автовыбор
            if (SelectedDevice == null && AvailableDevices.Any())
            {
                SelectedDevice = AvailableDevices.FirstOrDefault(d => d.IsDefault) 
                                 ?? AvailableDevices.First();
                Log.Information("Автовыбор устройства: {DeviceName}", SelectedDevice.Name);
            }

            // Применяем Whisper настройки из EngineSettings
            var modelSize = _currentConfig.SpeechRecognition.EngineSettings.TryGetValue("ModelSize", out var modelObj) 
                ? modelObj?.ToString() 
                : WhisperConstants.ModelSizes.Base;

            SelectedModel = AvailableModels.FirstOrDefault(m => m.ModelSize == modelSize)
                           ?? AvailableModels.First(); // Fallback на первую модель

            SelectedLanguage = _currentConfig.SpeechRecognition.Language;

            // Восстанавливаем флаг IsLoadingUI
            IsLoadingUI = wasLoading;

            Log.Information(
                "[AudioSettingsViewModel] Конфиг применен: Device={DeviceName}, Model={Model}, Language={Language}, Time={Seconds}s",
                SelectedDevice?.Name, SelectedModel?.DisplayName, SelectedLanguage, MaxRecordingSeconds);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AudioSettingsViewModel] Ошибка применения конфига к свойствам");
        }
    }

    /// <summary>
    /// Проверяет статус выбранной модели
    /// </summary>
    private async Task CheckModelStatusAsync()
    {
        try
        {
            if (SelectedModel == null)
            {
                UpdateModelStatus("Модель не выбрана", "#f44336", ModelState.Error);
                return;
            }

            // Проверка через ISpeechRecognitionService
            if (_speechRecognitionService.IsInitialized)
            {
                UpdateModelStatus("Модель готова", "#4caf50", ModelState.Ready);
            }
            else
            {
                UpdateModelStatus("Модель не загружена", "#ff9800", ModelState.NotDownloaded);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка проверки статуса модели");
            UpdateModelStatus("Ошибка проверки", "#f44336", ModelState.Error);
        }
    }

    /// <summary>
    /// Обновляет статус модели для UI
    /// </summary>
    public void UpdateModelStatus(string status, string color, ModelState state)
    {
        ModelStatusText = status;
        ModelStatusColor = color;
        
        // Обновляем состояния для видимости элементов
        IsModelReady = state == ModelState.Ready;
        IsModelNotReady = state == ModelState.NotDownloaded;
        IsModelDownloading = state == ModelState.Downloading;
        
        Log.Information("UI статус модели обновлен: {Status}, состояние: {State}", status, state);
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

    #endregion

    #region Helper Classes

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

    public class LanguageItem
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class WhisperModelItem
    {
        public string ModelSize { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
    }

    #endregion
}