using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels;
using Serilog;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class ControlSettingsView 
{
    public ControlSettingsView()
    {
        InitializeComponent();
    }

    // Конструктор с сервисами
    public ControlSettingsView(Services.GamepadService.MainGamepadService gamepadService, 
        SystemIntegrationService systemService, 
        ConfigurationService configurationService,
        ServiceContext serviceContext) : this()
    {
        try
        {
            // Гарантированно инициализируем _viewModel
            var viewModel = new ControlSettingsViewModel(
                configurationService, 
                serviceContext, 
                gamepadService, 
                systemService);
            
            DataContext = viewModel;
            
            // Инициализируем ViewModel
            _ = viewModel.InitializeAsync();
            
            Log.Debug("ControlSettingsView инициализирован с ViewModel");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при инициализации ControlSettingsView");
        }
    }
}
