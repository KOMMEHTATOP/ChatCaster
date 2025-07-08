using System.Windows;
using System.Windows.Media;
using ChatCaster.Core.Services;
using ChatCaster.Windows.ViewModels;
using Serilog;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class AudioSettingsView 
{
    private readonly IAudioCaptureService? _audioCaptureService;
    private bool _isTestingMicrophone;

    public AudioSettingsView()
    {
        InitializeComponent();
        Log.Information("AudioSettingsView создан");
    }

    public AudioSettingsView(IAudioCaptureService audioCaptureService) : this()
    {
        _audioCaptureService = audioCaptureService;
        Log.Information("AudioSettingsView инициализирован с AudioCaptureService");
    }

    /// <summary>
    /// ViewModel для работы с аудио настройками
    /// </summary>
    public void SetViewModel(AudioSettingsViewModel viewModel)
    {
        try
        {
            Log.Information("=== УСТАНОВКА VIEWMODEL ===");
            
            // Устанавливаем новый DataContext
            DataContext = viewModel;
            
            // Инициализируем ViewModel
            _ = viewModel.InitializeAsync();
            
            Log.Information("ViewModel установлен успешно");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка установки ViewModel");
        }
    }

    #region Button Click Handlers

    private async void TestMicrophoneButton_Click(object sender, RoutedEventArgs e)
    {
        try 
        {
            Log.Information("🔄 Начинаем тест микрофона");

            if (_isTestingMicrophone || _audioCaptureService == null) 
            {
                Log.Warning("Тест уже идет или сервис недоступен");
                return;
            }

            await HandleTestMicrophoneAsync();
        
            Log.Information("Тест микрофона завершен БЕЗ ОШИБОК");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "КРИТИЧЕСКАЯ ОШИБКА в тесте микрофона");
            MessageBox.Show($"Ошибка теста: {ex.Message}", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task HandleTestMicrophoneAsync()
    {
        try
        {
            _isTestingMicrophone = true;
            TestMicrophoneButton.IsEnabled = false;
            UpdateMicrophoneStatus("Тестируется...", "#ff9800");

            Log.Information("Начинаем тест микрофона");

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
        try
        {
            if (DataContext is AudioSettingsViewModel viewModel)
            {
                Log.Information("Запускаем загрузку модели через ViewModel");
                _ = viewModel.DownloadModelAsync(); 
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

    #region UI Helper Methods
    
    private void UpdateMicrophoneStatus(string text, string color)
    {
        MicrophoneStatusText.Text = text;
        MicrophoneStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        MicrophoneStatusIcon.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
    
    #endregion
}