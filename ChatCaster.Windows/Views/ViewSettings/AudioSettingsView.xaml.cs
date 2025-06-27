using System.Windows;
using System.Windows.Media;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels.Settings;
using ChatCaster.Windows.ViewModels.Settings.Speech;
using Wpf.Ui.Controls;
using Serilog;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class AudioSettingsView 
{
    private readonly AudioCaptureService? _audioCaptureService;
    private readonly SpeechRecognitionService? _speechRecognitionService;

    private bool _isTestingMicrophone = false;
    private bool _isDownloadingModel = false;

    public AudioSettingsView()
    {
        InitializeComponent();
        Log.Information("AudioSettingsView создан");
    }

    // Конструктор с сервисами
    public AudioSettingsView(AudioCaptureService audioCaptureService, 
                            SpeechRecognitionService speechRecognitionService) : this()
    {
        _audioCaptureService = audioCaptureService;
        _speechRecognitionService = speechRecognitionService;
        
        Log.Information("AudioSettingsView инициализирован с сервисами");
    }

    /// <summary>
    /// Устанавливает ViewModel И подписывается на события WhisperModelManager
    /// </summary>
    public void SetViewModel(AudioSettingsViewModel viewModel)
    {
        try
        {
            Log.Information("=== УСТАНОВКА VIEWMODEL ===");
            
            // Отписываемся от старых событий если есть старый ViewModel
            if (DataContext is AudioSettingsViewModel oldViewModel)
            {
                UnsubscribeFromModelEvents(oldViewModel);
            }
            
            // Устанавливаем новый DataContext
            DataContext = viewModel;
            
            // Подписка на события WhisperModelManager
            SubscribeToModelEvents(viewModel);
            
            // Инициализируем ViewModel
            _ = viewModel.InitializeAsync();
            
            Log.Information("✅ ViewModel установлен с подпиской на события");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка установки ViewModel");
        }
    }

    #region Управление подписками на события

    /// <summary>
    /// Подписывается на события WhisperModelManager
    /// </summary>
    private void SubscribeToModelEvents(AudioSettingsViewModel viewModel)
    {
        try
        {
            viewModel.WhisperModelManager.ModelStatusChanged += OnModelStatusChanged;
            viewModel.WhisperModelManager.DownloadButtonStateChanged += OnDownloadButtonStateChanged;
            
            Log.Information("Подписались на события WhisperModelManager");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка подписки на события WhisperModelManager");
        }
    }

    /// <summary>
    /// Отписывается от событий WhisperModelManager
    /// </summary>
    private void UnsubscribeFromModelEvents(AudioSettingsViewModel viewModel)
    {
        try
        {
            viewModel.WhisperModelManager.ModelStatusChanged -= OnModelStatusChanged;
            viewModel.WhisperModelManager.DownloadButtonStateChanged -= OnDownloadButtonStateChanged;
            
            Log.Information("Отписались от событий WhisperModelManager");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка отписки от событий WhisperModelManager");
        }
    }

    /// <summary>
    /// Обработчик изменения статуса модели
    /// </summary>
    private void OnModelStatusChanged(object? sender, ModelStatusChangedEventArgs e)
    {
        Dispatcher.Invoke(new Action(() =>
        {
            try
            {
                UpdateModelStatus(e.Status, e.ColorHex);
                Log.Information("Статус модели обновлен: {Status}", e.Status);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обновления статуса модели в UI");
            }
        }));
    }

    /// <summary>
    /// Обработчик изменения состояния кнопки загрузки
    /// </summary>
    private void OnDownloadButtonStateChanged(object? sender, ModelDownloadButtonStateChangedEventArgs e)
    {
        Dispatcher.Invoke(new Action(() =>
        {
            try
            {
                // Обновляем иконку кнопки
                if (Enum.TryParse<SymbolRegular>(e.Symbol, out var symbolEnum))
                {
                    DownloadButtonIcon.Symbol = symbolEnum;
                }
                
                if (e.Symbol == "CheckmarkCircle24")
                {
                    DownloadButtonIcon.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // #4caf50
                    DownloadButtonIcon.FontSize = 18;
                    DownloadButtonIcon.FontWeight = FontWeights.ExtraBold; // Делаем крупнее
                }
                
                // Обновляем состояние кнопки
                DownloadModelButton.IsEnabled = e.IsEnabled;
                DownloadModelButton.ToolTip = e.Tooltip;
                
                // Обновляем внешний вид кнопки
                DownloadModelButton.Appearance = e.Appearance switch
                {
                    "Primary" => ControlAppearance.Primary,
                    "Success" => ControlAppearance.Primary,  
                    "Caution" => ControlAppearance.Caution,
                    "Danger" => ControlAppearance.Danger,
                    _ => ControlAppearance.Secondary
                };
                
                Log.Information("Состояние кнопки загрузки обновлено: {Symbol}, {Tooltip}, Enabled={IsEnabled}", 
                    e.Symbol, e.Tooltip, e.IsEnabled);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обновления состояния кнопки загрузки в UI");
            }
        }));
    }

    #endregion
    
    #region Button Click Handlers

    private async void TestMicrophoneButton_Click(object sender, RoutedEventArgs e)
    {
        try 
        {
            Log.Information("🔄 Начинаем тест микрофона");

            if (_isTestingMicrophone || _audioCaptureService == null) 
            {
                Log.Warning("⚠️ Тест уже идет или сервис недоступен");
                return;
            }

            await HandleTestMicrophoneAsync();
        
            Log.Information("✅ Тест микрофона завершен БЕЗ ОШИБОК");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ КРИТИЧЕСКАЯ ОШИБКА в тесте микрофона");
            System.Windows.MessageBox.Show($"Ошибка теста: {ex.Message}", "Ошибка", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task HandleTestMicrophoneAsync()
    {
        try
        {
            _isTestingMicrophone = true;
            TestMicrophoneButton.IsEnabled = false;
            UpdateMicrophoneStatus("Тестируется...", "#ff9800");

            Log.Information("🔄 Начинаем тест микрофона");

            var viewModel = DataContext as AudioSettingsViewModel;
            var selectedDevice = viewModel?.SelectedDevice;
            
            if (selectedDevice != null)
            {
                Log.Information("Устанавливаем активное устройство: {DeviceId} ({DeviceName})", 
                    selectedDevice.Id, selectedDevice.Name);
            
                await _audioCaptureService!.SetActiveDeviceAsync(selectedDevice.Id);
            }
            else
            {
                Log.Warning("Устройство не выбрано для тестирования");
                UpdateMicrophoneStatus("Выберите устройство", "#ff9800");
                return;
            }

            bool testResult = await _audioCaptureService!.TestMicrophoneAsync();

            if (testResult)
            {
                UpdateMicrophoneStatus("Микрофон работает", "#4caf50");
                Log.Information("✅ Тест микрофона прошел успешно");
            }
            else
            {
                UpdateMicrophoneStatus("Проблема с микрофоном", "#f44336");
                Log.Warning("❌ Тест микрофона не прошел");
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
        
        // Используем WhisperModelManager для загрузки
        var viewModel = DataContext as AudioSettingsViewModel;
        if (viewModel?.WhisperModelManager != null)
        {
            _ = viewModel.WhisperModelManager.DownloadModelAsync();
        }
    }
    
    #endregion

    #region Event Handlers для загрузки модели (для совместимости)
    
    private void OnModelDownloadProgress(object? sender, Core.Events.ModelDownloadProgressEvent e)
    {
        Dispatcher.Invoke(new Action(() =>
        {
            UpdateModelStatus($"Загрузка {e.ProgressPercentage}%...", "#ff9800");
        }));
    }

    private void OnModelDownloadCompleted(object? sender, Core.Events.ModelDownloadCompletedEvent e)
    {
        Dispatcher.Invoke(new Action(() =>
        {
            if (_speechRecognitionService != null)
            {
                _speechRecognitionService.DownloadProgress -= OnModelDownloadProgress;
                _speechRecognitionService.DownloadCompleted -= OnModelDownloadCompleted;
            }

            if (e.Success)
            {
                UpdateModelStatus("Модель готова", "#4caf50");
                Log.Information("Модель успешно загружена");
            }
            else
            {
                UpdateModelStatus($"Ошибка загрузки: {e.ErrorMessage}", "#f44336");
                Log.Error("Ошибка загрузки модели: {Error}", e.ErrorMessage);
            }

            _isDownloadingModel = false;
        }));
    }

    #endregion

    #region UI Helper Methods
    
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

    #endregion

    #region Cleanup
    
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_speechRecognitionService != null)
            {
                _speechRecognitionService.DownloadProgress -= OnModelDownloadProgress;
                _speechRecognitionService.DownloadCompleted -= OnModelDownloadCompleted;
            }
            
            if (DataContext is AudioSettingsViewModel viewModel)
            {
                UnsubscribeFromModelEvents(viewModel);
                viewModel.Cleanup();
            }
            
            Log.Information("AudioSettingsView выгружен");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при выгрузке AudioSettingsView");
        }
    }

    #endregion
}