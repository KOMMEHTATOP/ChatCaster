using ChatCaster.Windows.ViewModels;
using Serilog;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class AudioSettingsView 
{

    public AudioSettingsView()
    {
        InitializeComponent();
    }

    public void SetViewModel(AudioSettingsViewModel viewModel)
    {
        try
        {
            DataContext = viewModel;
            _ = viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка установки ViewModel");
        }
    }
}