using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels.Base;
using ChatCaster.Windows.ViewModels.Settings.Speech;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ChatCaster.Windows.ViewModels;

public partial class AudioSettingsViewModel : BaseSettingsViewModel
{
    #region Private Fields
    
    private readonly WhisperModelManager _whisperModelManager;
    
    // UI Controls
    private ComboBox? _deviceComboBox;
    private ComboBox? _modelComboBox;
    private ComboBox? _languageComboBox;
    private Slider? _maxRecordingSecondsSlider;
    
    // Флаг для предотвращения циклических вызовов при программной установке значений
    private bool _isUpdatingUI = false;
    
    #endregion

    #region Observable Properties

    [ObservableProperty]
    private List<AudioDevice> _availableDevices = new();

    [ObservableProperty]
    private AudioDevice? _selectedDevice;

    [ObservableProperty]
    private WhisperModelItem? _selectedModel;

    [ObservableProperty]
    private string _selectedLanguage = "ru";

    [ObservableProperty]
    private int _maxRecordingSeconds = 30;

    [ObservableProperty]
    private List<int> _availableSampleRates = new() { 8000, 16000, 22050, 44100, 48000 };

    [ObservableProperty]
    private int _selectedSampleRate = 16000;

    #endregion

    #region Public Properties for UI Binding

    /// <summary>
    /// Коллекция доступных моделей Whisper для привязки к UI
    /// </summary>
    public ObservableCollection<WhisperModelItem> AvailableModels => _whisperModelManager.AvailableModels;

    #endregion

    #region Constructor

    public AudioSettingsViewModel(
        ConfigurationService configurationService,
        ServiceContext serviceContext,
        WhisperModelManager whisperModelManager) 
        : base(configurationService, serviceContext)
    {
        Log.Information("[VM] Конструктор AudioSettingsViewModel вызван");

        _whisperModelManager = whisperModelManager ?? throw new ArgumentNullException(nameof(whisperModelManager));
        
        Log.Information("AudioSettingsViewModel создан с WhisperModelManager");
    }

    #endregion

    #region Observable Property Changed Handlers

    /// <summary>
    /// Обработчик изменения выбранной модели
    /// </summary>
    partial void OnSelectedModelChanged(WhisperModelItem? value)
    {
        // Синхронизируем с Manager
        _whisperModelManager.SelectedModel = value;
        
        Log.Information("SelectedModel изменен на: {Model} ({DisplayName})", 
            value?.Model, value?.DisplayName);
    }

    #endregion

    #region UI Controls Setup

    /// <summary>
    /// Связывает UI элементы с ViewModel
    /// </summary>
    public void SetUIControls(
        ComboBox deviceComboBox,
        ComboBox modelComboBox, 
        ComboBox languageComboBox,
        Slider maxRecordingSecondsSlider)
    {
        _deviceComboBox = deviceComboBox;
        _modelComboBox = modelComboBox;
        _languageComboBox = languageComboBox;
        _maxRecordingSecondsSlider = maxRecordingSecondsSlider;
        
        Log.Information("UI Controls связаны с ViewModel");
        
        // ВАЖНО: Применяем настройки сразу после связывания UI элементов
        _ = Task.Run(async () =>
        {
            // Небольшая задержка для завершения инициализации UI
            await Task.Delay(100);
            
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Log.Information("[VM] Повторная попытка применения настроек к UI после связывания элементов");
                ApplyConfigToUI();
            });
        });
    }

    #endregion

    #region BaseSettingsViewModel Implementation

    protected override async Task LoadPageSpecificSettingsAsync()
    {
        Log.Information("[VM] LoadPageSpecificSettingsAsync вызван");
        try
        {
            Log.Information("Загружаем настройки Audio страницы...");
            
            // Загружаем аудио устройства (через ServiceContext)
            await LoadAudioDevicesAsync().ConfigureAwait(false);
            
            // Применяем настройки из конфига к UI (должно быть в UI потоке)
            await Application.Current.Dispatcher.InvokeAsync(() => 
            {
                if (IsReadyForOperation())
                {
                    Log.Information("[VM] UI элементы готовы, применяем настройки");
                    ApplyConfigToUI();
                }
                else
                {
                    Log.Warning("[VM] UI элементы еще не готовы, настройки будут применены после связывания UI");
                }
            });
            
            Log.Information("Настройки Audio страницы загружены");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка загрузки настроек Audio страницы");
        }
    }

    protected override async Task ApplySettingsToConfigAsync(AppConfig config)
    {
        try
        {
            // Переключаемся на background поток для работы с конфигом
            await Task.Yield();
            
            Log.Information("Применяем настройки Audio к конфигурации...");
            
            // Применяем аудио настройки
            config.Audio.SelectedDeviceId = SelectedDevice?.Id ?? "";
            config.Audio.SampleRate = SelectedSampleRate;
            config.Audio.MaxRecordingSeconds = MaxRecordingSeconds;
            
            // Применяем Whisper настройки
            config.Whisper.Model = SelectedModel?.Model ?? WhisperModel.Base;
            config.Whisper.Language = SelectedLanguage;
            
            Log.Information("Настройки применены к конфигурации: Device={DeviceId}, Model={Model}, Language={Language}", 
                config.Audio.SelectedDeviceId, config.Whisper.Model, config.Whisper.Language);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка применения настроек к конфигурации");
            throw;
        }
    }

    protected override async Task ApplySettingsToServicesAsync()
    {
        try
        {
            Log.Information("Применяем настройки к сервисам...");
            
            // Проверяем и переинициализируем модель через WhisperModelManager
            if (SelectedModel != null)
            {
                _whisperModelManager.SelectedModel = SelectedModel;
                await _whisperModelManager.CheckModelStatusAsync().ConfigureAwait(false);
            }
            
            Log.Information("Настройки применены к сервисам");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка применения настроек к сервисам");
            throw;
        }
    }

    public override void SubscribeToUIEvents()
    {
        try
        {
            Log.Information("=== ПОДПИСКА НА UI СОБЫТИЯ ===");
            Log.Information("ModelComboBox: {IsNotNull}", _modelComboBox != null);
            Log.Information("DeviceComboBox: {IsNotNull}", _deviceComboBox != null);
            Log.Information("LanguageComboBox: {IsNotNull}", _languageComboBox != null);
            
            if (_modelComboBox != null)
            {
                _modelComboBox.SelectionChanged += OnModelSelectionChanged;
                Log.Information("✅ Подписались на ModelComboBox.SelectionChanged");
            }
            else
            {
                Log.Warning("❌ ModelComboBox is null - не можем подписаться на события");
            }

            if (_deviceComboBox != null)
            {
                _deviceComboBox.SelectionChanged += OnDeviceSelectionChanged;
                Log.Information("✅ Подписались на DeviceComboBox.SelectionChanged");
            }

            if (_languageComboBox != null)
            {
                _languageComboBox.SelectionChanged += OnLanguageSelectionChanged;
                Log.Information("✅ Подписались на LanguageComboBox.SelectionChanged");
            }

            if (_maxRecordingSecondsSlider != null)
            {
                _maxRecordingSecondsSlider.ValueChanged += OnMaxRecordingSecondsSliderChanged;
                Log.Information("✅ Подписались на MaxRecordingSecondsSlider.ValueChanged");
            }

            Log.Information("События UI подписаны для AudioSettings");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка подписки на UI события AudioSettings");
        }
    }

    protected override void UnsubscribeFromUIEvents()
    {
        try
        {
            if (_modelComboBox != null)
                _modelComboBox.SelectionChanged -= OnModelSelectionChanged;

            if (_deviceComboBox != null)
                _deviceComboBox.SelectionChanged -= OnDeviceSelectionChanged;

            if (_languageComboBox != null)
                _languageComboBox.SelectionChanged -= OnLanguageSelectionChanged;

            if (_maxRecordingSecondsSlider != null)
                _maxRecordingSecondsSlider.ValueChanged -= OnMaxRecordingSecondsSliderChanged;

            Log.Debug("События UI отписаны для AudioSettings");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка отписки от UI событий AudioSettings");
        }
    }

    protected override void CleanupPageSpecific()
    {
        try
        {
            _whisperModelManager?.Cleanup();
            Log.Debug("WhisperModelManager очищен");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка очистки WhisperModelManager");
        }
    }

    #endregion

    #region Event Handlers

    private void OnModelSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoadingUI || _isUpdatingUI) return;

        Log.Information("🔄 OnModelSelectionChanged ВЫЗВАН!");
        Log.Information("Sender: {Sender}", sender?.GetType().Name);
        
        if (sender is ComboBox comboBox && comboBox.SelectedItem is WhisperModelItem selectedModel)
        {
            Log.Information("Выбранная модель: {Model} ({DisplayName})", 
                selectedModel.Model, selectedModel.DisplayName);
            
            SelectedModel = selectedModel;
            _ = OnModelSelectionChangedAsync();
        }
        else
        {
            Log.Warning("Неожиданный тип SelectedItem: {Type}", 
                ((ComboBox?)sender)?.SelectedItem?.GetType().Name ?? "null");
        }
    }

    private void OnDeviceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoadingUI || _isUpdatingUI) return;
        
        Log.Information("🔄 OnDeviceSelectionChanged ВЫЗВАН!");
        
        if (sender is ComboBox comboBox && comboBox.SelectedItem is AudioDevice device)
        {
            Log.Information("Выбранное устройство: {DeviceId} ({DeviceName})", device.Id, device.Name);
            SelectedDevice = device;
            _ = OnUISettingChangedAsync();
        }
    }

    private void OnLanguageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoadingUI || _isUpdatingUI) return;
        
        Log.Information("🔄 OnLanguageSelectionChanged ВЫЗВАН!");
        
        if (sender is ComboBox comboBox && comboBox.SelectedItem is string language)
        {
            Log.Information("Выбранный язык: {Language}", language);
            SelectedLanguage = language;
            _ = OnUISettingChangedAsync();
        }
    }

    private void OnMaxRecordingSecondsSliderChanged(object? sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsLoadingUI || _isUpdatingUI) return;
        
        Log.Information("🔄 OnMaxRecordingSecondsSliderChanged ВЫЗВАН!");
        Log.Information("Новое значение: {NewValue}s", (int)e.NewValue);
        
        MaxRecordingSeconds = (int)e.NewValue;
        _ = OnUISettingChangedAsync();
    }

    private async Task OnModelSelectionChangedAsync()
    {
        try
        {
            Log.Information("=== СМЕНА МОДЕЛИ В UI ===");
            Log.Information("Новая выбранная модель: {Model} ({DisplayName})", 
                SelectedModel?.Model, SelectedModel?.DisplayName);
            
            await OnUISettingChangedAsync();
            
            // Проверяем и переинициализируем модель
            if (SelectedModel != null)
            {
                Log.Information("Вызываем CheckModelStatusAsync для переинициализации...");
                
                try
                {
                    await _whisperModelManager.CheckModelStatusAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка при проверке статуса модели");
                }
            }
            
            Log.Information("Модель изменена на {ModelName}", SelectedModel?.DisplayName ?? "None");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при изменении модели");
        }
    }

    #endregion

    #region Private Methods

    private async Task LoadAudioDevicesAsync()
    {
        try
        {
            if (_serviceContext.AudioService == null)
            {
                Log.Error("AudioService в ServiceContext не инициализирован!");
                return;
            }

            // Загружаем устройства через ServiceContext
            var devices = await _serviceContext!.AudioService.GetAvailableDevicesAsync();
            AvailableDevices = devices.ToList();

            Log.Information("Загружено {Count} аудио устройств", AvailableDevices.Count);
            
            // Логируем каждое устройство для отладки
            foreach (var device in AvailableDevices)
            {
                Log.Information("[VM] Устройство: {DeviceId} - {DeviceName}", device.Id, device.Name);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка загрузки аудио устройств");
            AvailableDevices = new List<AudioDevice>();
        }
    }
    
    private void ApplyConfigToUI()
    {
        Log.Information("[VM] ApplyConfigToUI вызван");

        try
        {
            // Устанавливаем флаг для предотвращения циклических вызовов
            _isUpdatingUI = true;
            
            var config = _serviceContext!.Config!;
            
            // Применяем аудио настройки
            MaxRecordingSeconds = config.Audio.MaxRecordingSeconds;
            SelectedSampleRate = config.Audio.SampleRate;
            
            // Находим и устанавливаем выбранное устройство
            if (!string.IsNullOrEmpty(config.Audio.SelectedDeviceId))
            {
                SelectedDevice = AvailableDevices.FirstOrDefault(d => d.Id == config.Audio.SelectedDeviceId);
                Log.Information("[VM] Устройство из конфига: {DeviceId} -> {Device}", 
                    config.Audio.SelectedDeviceId, SelectedDevice?.Name ?? "не найдено");
            }
            else
            {
                Log.Warning("[VM] В конфиге нет ID устройства, устройство не выбрано");
            }
            
            // Применяем Whisper настройки
            Log.Information("[VM] AvailableModels: " + string.Join(", ", _whisperModelManager.AvailableModels.Select(m => m.Model)));
            Log.Information("[VM] Config.Model: " + config.Whisper.Model);
            SelectedModel = _whisperModelManager.FindModelByEnum(config.Whisper.Model);
            Log.Information("[VM] FindModelByEnum result: " + SelectedModel?.DisplayName);

            SelectedLanguage = config.Whisper.Language;
            
            // Устанавливаем выбранную модель в менеджере
            if (SelectedModel != null)
            {
                _whisperModelManager.SelectedModel = SelectedModel;
            }
            
            // СИНХРОНИЗИРУЕМ UI ЭЛЕМЕНТЫ С ОБНОВЛЕННЫМИ СВОЙСТВАМИ
            SyncUIElementsWithProperties();
            
            Log.Information("Настройки применены к UI: Device={DeviceId}, Model={Model}, Language={Language}", 
                config.Audio.SelectedDeviceId, config.Whisper.Model, config.Whisper.Language);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка применения настроек к UI");
        }
        finally
        {
            // Снимаем флаг
            _isUpdatingUI = false;
        }
    }

    /// <summary>
    /// Синхронизирует UI элементы с текущими значениями свойств ViewModel
    /// </summary>
    private void SyncUIElementsWithProperties()
    {
        try
        {
            Log.Information("[VM] Синхронизируем UI элементы с свойствами...");
            
            // Устройство
            if (_deviceComboBox != null && SelectedDevice != null)
            {
                _deviceComboBox.SelectedItem = SelectedDevice;
                Log.Information("[VM] DeviceComboBox.SelectedItem = {Device}", SelectedDevice.Name);
            }
            else if (_deviceComboBox != null && SelectedDevice == null)
            {
                _deviceComboBox.SelectedIndex = -1; // Ничего не выбрано
                Log.Information("[VM] DeviceComboBox.SelectedIndex = -1 (устройство не найдено)");
            }
            
            // Модель
            if (_modelComboBox != null && SelectedModel != null)
            {
                _modelComboBox.SelectedItem = SelectedModel;
                Log.Information("[VM] ModelComboBox.SelectedItem = {Model}", SelectedModel.DisplayName);
            }
            
            // Язык
            if (_languageComboBox != null)
            {
                _languageComboBox.SelectedItem = SelectedLanguage;
                Log.Information("[VM] LanguageComboBox.SelectedItem = {Language}", SelectedLanguage);
            }
            
            // Слайдер времени записи
            if (_maxRecordingSecondsSlider != null)
            {
                _maxRecordingSecondsSlider.Value = MaxRecordingSeconds;
                Log.Information("[VM] MaxRecordingSecondsSlider.Value = {Seconds}", MaxRecordingSeconds);
            }
            
            Log.Information("[VM] Синхронизация UI элементов завершена");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка синхронизации UI элементов");
        }
    }

    #endregion
}