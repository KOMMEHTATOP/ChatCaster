using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels.Base;
using ChatCaster.Windows.ViewModels.Settings.Speech;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using System.Collections.ObjectModel;

namespace ChatCaster.Windows.ViewModels.Settings;

public partial class AudioSettingsViewModel : BaseSettingsViewModel
{

    #region Private Fields

    private readonly WhisperModelManager _whisperModelManager;

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

    // 🔥 НОВЫЕ СВОЙСТВА для индикации статуса модели
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
    public ObservableCollection<WhisperModelItem> AvailableModels => _whisperModelManager.AvailableModels;

    /// <summary>
    /// 🔥 ДОБАВЛЕНО: Публичный доступ к WhisperModelManager для подписки на события
    /// </summary>
    public WhisperModelManager WhisperModelManager => _whisperModelManager;

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

    #endregion

    #region Constructor

    public AudioSettingsViewModel(
        ConfigurationService configurationService,
        ServiceContext serviceContext,
        WhisperModelManager whisperModelManager)
        : base(configurationService, serviceContext)
    {
        Log.Information("[AudioSettingsViewModel] Конструктор вызван");

        _whisperModelManager = whisperModelManager ?? throw new ArgumentNullException(nameof(whisperModelManager));

        Log.Information("[AudioSettingsViewModel] создан с WhisperModelManager");
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
            value?.Model, value?.DisplayName);

        // Синхронизируем с Manager
        _whisperModelManager.SelectedModel = value;

        // 🔥 ДОБАВЛЕНО: Проверяем статус новой модели при изменении
        _ = Task.Run(async () =>
        {
            try
            {
                await _whisperModelManager.CheckModelStatusAsync();
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

            // 🔥 ДОБАВЛЕНО: Проверяем статус модели после загрузки настроек
            Log.Information("[ДИАГНОСТИКА] Проверяем статус модели...");
            await _whisperModelManager.CheckModelStatusAsync();
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

            // ✅ ИСПРАВЛЕНО: Применяем Whisper настройки с правильным fallback на Tiny
            config.Whisper.Model = SelectedModel?.Model ?? WhisperModel.Tiny;
            config.Whisper.Language = SelectedLanguage;

            Log.Information(
                "[AudioSettingsViewModel] Настройки применены: Device={DeviceId}, Model={Model}, Language={Language}, Time={Seconds}s",
                config.Audio.SelectedDeviceId, config.Whisper.Model, config.Whisper.Language,
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
            if (SelectedDevice != null && _serviceContext?.AudioService != null)
            {
                await _serviceContext.AudioService.SetActiveDeviceAsync(SelectedDevice.Id);
                Log.Information("[AudioSettingsViewModel] Активное устройство установлено: {DeviceName}",
                    SelectedDevice.Name);
            }

            // Переинициализируем модель Whisper если изменена
            if (SelectedModel != null)
            {
                await _whisperModelManager.CheckModelStatusAsync();
                Log.Information("[AudioSettingsViewModel] Модель Whisper переинициализирована: {Model}",
                    SelectedModel.DisplayName);
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
        // ✅ УБРАЛИ: Больше не нужно подписываться на UI события
        // Observable свойства автоматически работают через XAML привязки
        Log.Information("[AudioSettingsViewModel] UI события обрабатываются через XAML привязки");
    }

    protected override void CleanupPageSpecific()
    {
        try
        {
            _whisperModelManager?.Cleanup();
            Log.Debug("[AudioSettingsViewModel] WhisperModelManager очищен");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AudioSettingsViewModel] Ошибка очистки");
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Загружает список доступных аудио устройств
    /// </summary>
    private async Task LoadAudioDevicesAsync()
    {
        try
        {
            if (_serviceContext?.AudioService == null)
            {
                Log.Error("[AudioSettingsViewModel] AudioService не инициализирован!");
                AvailableDevices = new List<AudioDevice>();
                return;
            }

            var devices = await _serviceContext.AudioService.GetAvailableDevicesAsync();
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
            var config = _serviceContext!.Config!;

            Log.Information("[AudioSettingsViewModel] Применяем конфиг к свойствам...");

            // ✅ ИСПРАВЛЕНО: Временно отключаем IsLoadingUI чтобы обновить свойства
            var wasLoading = IsLoadingUI;
            IsLoadingUI = false;

            // Применяем аудио настройки
            MaxRecordingSeconds = config.Audio.MaxRecordingSeconds;
            SelectedSampleRate = config.Audio.SampleRate;

            // ✅ ИСПРАВЛЕНО: Находим и устанавливаем выбранное устройство
            if (!string.IsNullOrEmpty(config.Audio.SelectedDeviceId))
            {
                SelectedDevice = AvailableDevices.FirstOrDefault(d => d.Id == config.Audio.SelectedDeviceId);
                Log.Information("[AudioSettingsViewModel] Устройство из конфига: {DeviceId} -> {DeviceName}", 
                    config.Audio.SelectedDeviceId, SelectedDevice?.Name ?? "не найдено");
            }

            // ✅ ИСПРАВЛЕНО: Если устройство не найдено ИЛИ пустое - автовыбор
            if (SelectedDevice == null && AvailableDevices.Any())
            {
                SelectedDevice = AvailableDevices.FirstOrDefault(d => d.IsDefault) 
                                 ?? AvailableDevices.First();
                Log.Information("Автовыбор устройства: {DeviceName}", SelectedDevice.Name);
            }

            // Применяем Whisper настройки
            SelectedModel = _whisperModelManager.FindModelByEnum(config.Whisper.Model)
                            ?? WhisperModelFactory.GetDefaultModel(); // ✅ FALLBACK на Tiny если модель не найдена
            SelectedLanguage = config.Whisper.Language;

            // Устанавливаем выбранную модель в менеджере
            _whisperModelManager.SelectedModel = SelectedModel;

            // ✅ ИСПРАВЛЕНО: Восстанавливаем флаг IsLoadingUI
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
    /// 🔥 НОВЫЙ МЕТОД: Обновляет статус модели для UI
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
    #endregion
    
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

    public class LanguageItem
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
    }


}