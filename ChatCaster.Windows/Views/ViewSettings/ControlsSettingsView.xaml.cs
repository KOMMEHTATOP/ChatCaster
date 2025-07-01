using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services.GamepadService;
using ChatCaster.Windows.ViewModels;
using Serilog;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class ControlSettingsView 
{
    public ControlSettingsView()
    {
        InitializeComponent();
    }

    // ✅ ИСПРАВЛЕНО: Конструктор без ServiceContext
    public ControlSettingsView(
        IGamepadService gamepadService, 
        ISystemIntegrationService systemService, 
        IConfigurationService configurationService,
        AppConfig currentConfig,
        GamepadVoiceCoordinator gamepadVoiceCoordinator) : this()
    {
        try
        {
            // ✅ ИСПРАВЛЕНО: Создаем ViewModel без ServiceContext
            var viewModel = new ControlSettingsViewModel(
                configurationService, 
                currentConfig, 
                gamepadService, 
                systemService,
                gamepadVoiceCoordinator);
            
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
