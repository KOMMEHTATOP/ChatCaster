using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using Serilog;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class AudioSettingsView 
{
    private readonly AudioCaptureService? _audioCaptureService;
    private readonly SpeechRecognitionService? _speechRecognitionService;
    private readonly ConfigurationService? _configService;
    private readonly ServiceContext? _serviceContext;

    private bool _isTestingMicrophone = false;
    private bool _isDownloadingModel = false;
    private bool _isLoadingUi = false; // Флаг чтобы не применять настройки во время загрузки UI

    public AudioSettingsView()
    {
        InitializeComponent();
        LoadInitialData();
    }

    // Конструктор с сервисами
    public AudioSettingsView(AudioCaptureService audioCaptureService, 
                            SpeechRecognitionService speechRecognitionService, 
                            ConfigurationService configService, ServiceContext serviceContext) : this()
    {
        _audioCaptureService = audioCaptureService;
        _speechRecognitionService = speechRecognitionService;
        _configService = configService;
        _serviceContext = serviceContext;
        LoadConfigAndDevices();
        
        Log.Debug("AudioSettingsView инициализирован с сервисами");
    }

    private void LoadInitialData()
    {
        _isLoadingUi = true;
        
        // Устанавливаем значения по умолчанию для UI элементов
        MaxDurationSlider.Value = 30;
        MaxDurationValueText.Text = "30с";
        
        // Загружаем доступные устройства если сервис доступен
        if (_audioCaptureService != null)
        {
            _ = LoadMicrophoneDevicesAsync();
        }

        // Проверяем статус моделей если сервис доступен
        if (_speechRecognitionService != null)
        {
            _ = CheckModelStatusAsync();
        }

        _isLoadingUi = false;
        
        Log.Debug("Загружены значения по умолчанию для AudioSettings");
    }

    private void LoadConfigAndDevices()
    {
        _isLoadingUi = true;
        
        // Загружаем устройства и модели
        _ = LoadConfigAndDevicesAsync();
        
        _isLoadingUi = false;
    }

    private async Task LoadConfigAndDevicesAsync()
    {
        try
        {
            // Загружаем устройства и модели
            await LoadMicrophoneDevicesAsync();
            await CheckModelStatusAsync();
            
            // Применяем настройки из ServiceContext к UI
            ApplyConfigToUI();
            
            // Подписываемся на события изменения UI
            SubscribeToUIEvents();
            
            Log.Debug("Конфигурация и устройства AudioSettings загружены");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при загрузке конфигурации и устройств AudioSettings");
        }
    }

    private void SubscribeToUIEvents()
    {
        // Подписываемся на все события изменения настроек
        MicrophoneComboBox.SelectionChanged += OnSettingChanged;
        WhisperModelComboBox.SelectionChanged += OnSettingChanged;
        LanguageComboBox.SelectionChanged += OnSettingChanged;
        MaxDurationSlider.ValueChanged += OnSliderChanged;
        
        Log.Debug("События UI подписаны для AudioSettings");
    }

    private void ApplyConfigToUI()
    {
        if (_serviceContext?.Config == null)
        {
            Log.Warning("ServiceContext.Config недоступен при применении настроек к UI");
            return;
        }

        try
        {
            var config = _serviceContext.Config;

            // Применяем настройки аудио
            MaxDurationSlider.Value = config.Audio.MaxRecordingSeconds;
            MaxDurationValueText.Text = $"{config.Audio.MaxRecordingSeconds}с";

            // Выбираем сохраненное устройство в ComboBox
            if (!string.IsNullOrEmpty(config.Audio.SelectedDeviceId))
            {
                foreach (ComboBoxItem item in MicrophoneComboBox.Items)
                {
                    if (item.Tag?.ToString() == config.Audio.SelectedDeviceId)
                    {
                        item.IsSelected = true;
                        break;
                    }
                }
            }

            // Выбираем сохраненную модель Whisper
            foreach (ComboBoxItem item in WhisperModelComboBox.Items)
            {
                if (item.Tag?.ToString() == config.Whisper.Model.ToString())
                {
                    item.IsSelected = true;
                    break;
                }
            }

            // Выбираем сохраненный язык
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag?.ToString() == config.Whisper.Language)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            Log.Information("Настройки применены к UI: Device={DeviceId}, Model={Model}, Language={Language}", 
                config.Audio.SelectedDeviceId, config.Whisper.Model, config.Whisper.Language);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка применения настроек к UI");
        }
    }

    // Обработчики автоматического применения настроек
    private void OnSettingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUi) return; // Не применяем во время загрузки UI
        
        _ = HandleSettingChangedAsync(sender);
    }

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingUi) return; // Не применяем во время загрузки UI
        
        _ = HandleSliderChangedAsync(e);
    }

    private async Task HandleSettingChangedAsync(object sender)
    {
        try
        {
            await ApplyCurrentSettingsAsync();
            
            // Проверяем статус модели если изменилась модель Whisper
            if (Equals(sender, WhisperModelComboBox))
            {
                await CheckModelStatusAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка в HandleSettingChangedAsync");
        }
    }

    private async Task HandleSliderChangedAsync(RoutedPropertyChangedEventArgs<double> e)
    {
        try
        {
            // Обновляем текст рядом со слайдером
            if (MaxDurationValueText != null)
            {
                MaxDurationValueText.Text = $"{(int)e.NewValue}с";
            }
            
            await ApplyCurrentSettingsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка в HandleSliderChangedAsync");
        }
    }

    private async Task ApplyCurrentSettingsAsync()
    {
        try
        {
            if (_configService == null || _serviceContext?.Config == null)
            {
                Log.Warning("ConfigService или ServiceContext.Config недоступны для применения настроек");
                return;
            }

            var config = _serviceContext.Config; // Используем существующий объект

            // Собираем данные из UI и обновляем конфигурацию
            var selectedMicrophone = MicrophoneComboBox.SelectedItem as ComboBoxItem;
            if (selectedMicrophone?.Tag is string deviceId)
            {
                config.Audio.SelectedDeviceId = deviceId;
            }

            var selectedModel = WhisperModelComboBox.SelectedItem as ComboBoxItem;
            if (selectedModel?.Tag is string modelTag && Enum.TryParse<WhisperModel>(modelTag, out var model))
            {
                config.Whisper.Model = model;
            }

            var selectedLanguage = LanguageComboBox.SelectedItem as ComboBoxItem;
            if (selectedLanguage?.Tag is string language)
            {
                config.Whisper.Language = language;
            }

            config.Audio.MaxRecordingSeconds = (int)MaxDurationSlider.Value;

            // Сохраняем конфигурацию
            await _configService.SaveConfigAsync(config);

            // Применяем к аудио сервису если выбрано новое устройство
            if (_audioCaptureService != null && selectedMicrophone?.Tag is string newDeviceId)
            {
                await _audioCaptureService.SetActiveDeviceAsync(newDeviceId);
            }

            Log.Debug("Настройки аудио автоматически сохранены и применены");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка автоприменения настроек аудио");
        }
    }

    private async Task LoadMicrophoneDevicesAsync()
    {
        try
        {
            if (_audioCaptureService == null)
            {
                Log.Warning("AudioCaptureService недоступен для загрузки устройств");
                return;
            }

            var devices = (await _audioCaptureService.GetAvailableDevicesAsync()).ToList();
            
            MicrophoneComboBox.Items.Clear();
            foreach (var device in devices)
            {
                var item = new ComboBoxItem
                {
                    Content = device.Name,
                    Tag = device.Id
                };

                // Выбираем устройство по умолчанию или сохраненное из конфигурации
                if ((_serviceContext?.Config != null && device.Id == _serviceContext.Config.Audio.SelectedDeviceId) ||
                    (_serviceContext?.Config == null && device.IsDefault))
                {
                    item.IsSelected = true;
                }

                MicrophoneComboBox.Items.Add(item);
            }

            UpdateMicrophoneStatus("Микрофон готов", "#4caf50");
            Log.Information("Загружено {Count} аудио устройств", devices.Count);
        }
        catch (Exception ex)
        {
            UpdateMicrophoneStatus($"Ошибка загрузки устройств: {ex.Message}", "#f44336");
            Log.Error(ex, "Ошибка загрузки аудио устройств");
        }
    }

    private async Task CheckModelStatusAsync()
    {
        try
        {
            if (_speechRecognitionService == null)
            {
                Log.Warning("SpeechRecognitionService недоступен для проверки модели");
                return;
            }

            // Проверяем доступность текущей выбранной модели
            var selectedItem = WhisperModelComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag is string modelTag && Enum.TryParse<WhisperModel>(modelTag, out var model))
            {
                bool isAvailable = await _speechRecognitionService.IsModelAvailableAsync(model);
                
                if (isAvailable)
                {
                    UpdateModelStatus("Модель готова", "#4caf50");
                    DownloadModelButton.Visibility = Visibility.Collapsed;
                    Log.Debug("Модель {Model} доступна", model);
                }
                else
                {
                    long sizeBytes = await _speechRecognitionService.GetModelSizeAsync(model);
                    string sizeText = FormatFileSize(sizeBytes);
                    UpdateModelStatus($"Модель не скачана ({sizeText})", "#ff9800");
                    DownloadModelButton.Visibility = Visibility.Visible;
                    Log.Debug("Модель {Model} не доступна, размер: {Size}", model, sizeText);
                }
            }
        }
        catch (Exception ex)
        {
            UpdateModelStatus($"Ошибка проверки модели: {ex.Message}", "#f44336");
            Log.Error(ex, "Ошибка проверки статуса модели");
        }
    }

    private void TestMicrophoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isTestingMicrophone || _audioCaptureService == null) return;

        _ = HandleTestMicrophoneAsync();
    }

    private async Task HandleTestMicrophoneAsync()
    {
        try
        {
            _isTestingMicrophone = true;
            TestMicrophoneButton.IsEnabled = false;
            UpdateMicrophoneStatus("Тестируется...", "#ff9800");

            // Устанавливаем выбранное устройство
            var selectedItem = MicrophoneComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag is string deviceId)
            {
                await _audioCaptureService!.SetActiveDeviceAsync(deviceId);
            }

            // Тестируем микрофон
            bool testResult = await _audioCaptureService!.TestMicrophoneAsync();

            if (testResult)
            {
                UpdateMicrophoneStatus("Микрофон работает", "#4caf50");
                Log.Information("Тест микрофона прошел успешно");
            }
            else
            {
                UpdateMicrophoneStatus("Проблема с микрофоном", "#f44336");
                Log.Warning("Тест микрофона не прошел");
            }
        }
        catch (Exception ex)
        {
            UpdateMicrophoneStatus($"Ошибка тестирования: {ex.Message}", "#f44336");
            Log.Error(ex, "Ошибка тестирования микрофона");
        }
        finally
        {
            _isTestingMicrophone = false;
            TestMicrophoneButton.IsEnabled = true;
        }
    }

    private void DownloadModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloadingModel || _speechRecognitionService == null) return;

        _ = HandleDownloadModelAsync();
    }

    private async Task HandleDownloadModelAsync()
    {
        try
        {
            _isDownloadingModel = true;
            DownloadModelButton.IsEnabled = false;

            var selectedItem = WhisperModelComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag is string modelTag && Enum.TryParse<WhisperModel>(modelTag, out var model))
            {
                UpdateModelStatus("Начинаем загрузку...", "#ff9800");
                Log.Information("Начинаем загрузку модели {Model}", model);

                // Подписываемся на события загрузки
                _speechRecognitionService!.DownloadProgress += OnModelDownloadProgress;
                _speechRecognitionService.DownloadCompleted += OnModelDownloadCompleted;

                // Инициализируем модель (это запустит загрузку если нужно)
                var config = new WhisperConfig { Model = model };
                await _speechRecognitionService.InitializeAsync(config);
            }
        }
        catch (Exception ex)
        {
            UpdateModelStatus($"Ошибка загрузки: {ex.Message}", "#f44336");
            Log.Error(ex, "Ошибка загрузки модели");
            _isDownloadingModel = false;
            DownloadModelButton.IsEnabled = true;
        }
    }

    private void OnModelDownloadProgress(object? sender, Core.Events.ModelDownloadProgressEvent e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateModelStatus($"Загрузка {e.ProgressPercentage}%...", "#ff9800");
        });
    }

    private void OnModelDownloadCompleted(object? sender, Core.Events.ModelDownloadCompletedEvent e)
    {
        Dispatcher.Invoke(() =>
        {
            // Отписываемся от событий
            if (_speechRecognitionService != null)
            {
                _speechRecognitionService.DownloadProgress -= OnModelDownloadProgress;
                _speechRecognitionService.DownloadCompleted -= OnModelDownloadCompleted;
            }

            if (e.Success)
            {
                UpdateModelStatus("Модель готова", "#4caf50");
                DownloadModelButton.Visibility = Visibility.Collapsed;
                Log.Information("Модель успешно загружена");
            }
            else
            {
                UpdateModelStatus($"Ошибка загрузки: {e.ErrorMessage}", "#f44336");
                Log.Error("Ошибка загрузки модели: {Error}", e.ErrorMessage);
            }

            _isDownloadingModel = false;
            DownloadModelButton.IsEnabled = true;
        });
    }

    private void UpdateMicrophoneStatus(string text, string color)
    {
        MicrophoneStatusText.Text = text;
        MicrophoneStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        MicrophoneStatusIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private void UpdateModelStatus(string text, string color)
    {
        ModelStatusText.Text = text;
        ModelStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        ModelStatusIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
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

    // Cleanup при выгрузке страницы - ВАЖНО оставить для отписки от событий
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Отписываемся от событий загрузки модели если подписаны
            if (_speechRecognitionService != null)
            {
                _speechRecognitionService.DownloadProgress -= OnModelDownloadProgress;
                _speechRecognitionService.DownloadCompleted -= OnModelDownloadCompleted;
                Log.Debug("Отписались от событий SpeechRecognitionService");
            }
            
            Log.Debug("AudioSettingsView выгружен");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при выгрузке AudioSettingsView");
        }
    }
}