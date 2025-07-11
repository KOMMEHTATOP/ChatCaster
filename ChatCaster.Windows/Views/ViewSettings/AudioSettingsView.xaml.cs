using System.Windows;
using System.Windows.Media;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Windows.ViewModels;
using Serilog;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class AudioSettingsView 
{

    public AudioSettingsView()
    {
        InitializeComponent();
        Log.Information("AudioSettingsView создан");
    }

    public void SetViewModel(AudioSettingsViewModel viewModel)
    {
        try
        {
            Log.Information("=== УСТАНОВКА VIEWMODEL ===");
            DataContext = viewModel;
            _ = viewModel.InitializeAsync();
            Log.Information("ViewModel установлен успешно");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка установки ViewModel");
        }
    }

    private void DownloadModelButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is AudioSettingsViewModel viewModel)
            {
                Log.Information("Запускаем загрузку модели через ViewModel");
                _ = viewModel.WhisperModelComponent.DownloadModelCommand.ExecuteAsync(null); 
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при запуске загрузки модели");
        }
    }
}