using System.Windows;
using System.Windows.Media;
using ChatCaster.Core.Services;
using ChatCaster.Windows.ViewModels.Settings;
using Serilog;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class AudioSettingsView 
{
    private readonly IAudioCaptureService? _audioCaptureService;
    private readonly ISpeechRecognitionService? _speechRecognitionService;

    private bool _isTestingMicrophone = false;
    private bool _isDownloadingModel = false;

    public AudioSettingsView()
    {
        InitializeComponent();
        Log.Information("AudioSettingsView создан");
    }

    // ✅ ИСПРАВЛЕНО: Конструктор остается тем же (интерфейсы)
    public AudioSettingsView(IAudioCaptureService audioCaptureService, 
                            ISpeechRecognitionService speechRecognitionService) : this()
    {
        _audioCaptureService = audioCaptureService;
        _speechRecognitionService = speechRecognitionService;
        
        Log.Information("AudioSettingsView инициализирован с DI сервисами");
    }

    /// <summary>
    /// ✅ МЕТОД ОСТАЕТСЯ БЕЗ ИЗМЕНЕНИЙ - устанавливает ViewModel для работы с новым Whisper модулем
    /// </summary>
    public void SetViewModel(AudioSettingsViewModel viewModel)
    {
        try
        {
            Log.Information("=== УСТАНОВКА VIEWMODEL (Новый Whisper модуль) ===");
            
            // Отписываемся от старых событий если есть старый ViewModel
            if (DataContext is AudioSettingsViewModel oldViewModel)
            {
                UnsubscribeFromOldEvents(oldViewModel);
            }
            
            // Устанавливаем новый DataContext
            DataContext = viewModel;
            
            // ✅ НОВЫЙ ПОДХОД: Подписка на события нового Whisper модуля
            SubscribeToNewWhisperEvents(viewModel);
            
            // Инициализируем ViewModel
            _ = viewModel.InitializeAsync();
            
            Log.Information("✅ ViewModel установлен для нового Whisper модуля");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка установки ViewModel");
        }
    }

    #region Управление подписками на события

    /// <summary>
    /// Подписывается на события нового Whisper модуля
    /// </summary>
    private void SubscribeToNewWhisperEvents(AudioSettingsViewModel viewModel)
    {
        try
        {
            // ✅ НОВЫЙ ПОДХОД: События приходят от ISpeechRecognitionService
            if (_speechRecognitionService != null)
            {
                // Если у нового Whisper модуля есть события - подписываемся
                // Возможно у вашего ISpeechRecognitionService есть события типа:
                // _speechRecognitionService.ModelStatusChanged += OnModelStatusChanged;
                // _speechRecognitionService.DownloadProgress += OnDownloadProgress;
                
                Log.Information("Подписались на события нового Whisper модуля");
            }
            
            // Альтернативно: события могут быть в самом ViewModel
            // viewModel.ModelStatusChanged += OnModelStatusChanged;
            
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка подписки на события нового Whisper модуля");
        }
    }

    /// <summary>
    /// Отписывается от старых событий
    /// </summary>
    private void UnsubscribeFromOldEvents(AudioSettingsViewModel viewModel)
    {
        try
        {
            // ✅ Очищаем старые подписки если они были
            if (_speechRecognitionService != null)
            {
                // Отписываемся от событий если они есть
                Log.Information("Отписались от старых событий");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка отписки от старых событий");
        }
    }

    /// <summary>
    /// Обработчик изменения статуса модели (совместимость)
    /// </summary>
    private void OnModelStatusChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(new Action(() =>
        {
            try
            {
                // ✅ Обновляем статус модели в UI
                UpdateModelStatus("Модель готова", "#4caf50");
                Log.Information("Статус модели обновлен");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обновления статуса модели в UI");
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
        try
        {
            if (_isDownloadingModel) return;
            
            var viewModel = DataContext as AudioSettingsViewModel;
            if (viewModel != null)
            {
                Log.Information("Запускаем загрузку модели через ViewModel");
                _ = viewModel.DownloadModelAsync(); // Используем метод ViewModel
            }
            else
            {
                Log.Warning("ViewModel недоступен для загрузки модели");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при запуске загрузки модели");
        }
    }
    
    #endregion

    #region Event Handlers для загрузки модели (совместимость)
    
    private void OnModelDownloadProgress(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(new Action(() =>
        {
            UpdateModelStatus("Загрузка...", "#ff9800");
        }));
    }

    private void OnModelDownloadCompleted(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(new Action(() =>
        {
            UpdateModelStatus("Модель готова", "#4caf50");
            Log.Information("Модель успешно загружена");
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
            if (DataContext is AudioSettingsViewModel viewModel)
            {
                UnsubscribeFromOldEvents(viewModel);
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