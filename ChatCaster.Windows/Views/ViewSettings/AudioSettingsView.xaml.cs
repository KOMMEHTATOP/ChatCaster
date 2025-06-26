using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels;
using ChatCaster.Windows.ViewModels.Settings;
using Serilog;
using AudioSettingsViewModel = ChatCaster.Windows.ViewModels.AudioSettingsViewModel;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class AudioSettingsView 
{
    private readonly AudioCaptureService? _audioCaptureService;
    private readonly SpeechRecognitionService? _speechRecognitionService;
    private readonly ConfigurationService? _configService;
    private readonly ServiceContext? _serviceContext;

    private bool _isTestingMicrophone = false;
    private bool _isDownloadingModel = false;

    public AudioSettingsView()
    {
        InitializeComponent();
    }

    // Конструктор с сервисами
    public AudioSettingsView(AudioCaptureService audioCaptureService, 
                            SpeechRecognitionService speechRecognitionService, 
                            ConfigurationService configService, 
                            ServiceContext serviceContext) : this()
    {
        _audioCaptureService = audioCaptureService;
        _speechRecognitionService = speechRecognitionService;
        _configService = configService;
        _serviceContext = serviceContext;
        
        Log.Debug("AudioSettingsView инициализирован с сервисами");
    }

    // ✅ НОВЫЙ МЕТОД: Устанавливает ViewModel и связывает UI элементы
    public void SetViewModel(AudioSettingsViewModel viewModel)
    {
        try
        {
            Log.Information("=== УСТАНОВКА VIEWMODEL ===");
            
            // Устанавливаем DataContext
            DataContext = viewModel;
            
            // Связываем UI элементы с ViewModel
            viewModel.SetUIControls(
                MicrophoneComboBox,
                WhisperModelComboBox,
                LanguageComboBox,
                MaxDurationSlider
            );
            
            Log.Information("ViewModel установлен и UI элементы связаны");
            
            // Инициализируем ViewModel
            _ = viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка установки ViewModel");
        }
    }

    // ✅ УБРАЛИ: SubscribeToUIEvents() - теперь это делает ViewModel
    // ✅ УБРАЛИ: OnSettingChanged() - теперь это делает ViewModel
    // ✅ УБРАЛИ: ApplyCurrentSettingsAsync() - теперь это делает ViewModel

    // ========== КНОПКИ ОСТАЮТСЯ В VIEW (UI логика) ==========
    
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

    // ========== УТИЛИТЫ ОСТАЮТСЯ ==========
    
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

    // Cleanup при выгрузке страницы
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