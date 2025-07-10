using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.Input;
using ChatCaster.Core.Services.System;
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

    public ControlSettingsView(
        IGamepadService gamepadService, 
        ISystemIntegrationService systemService, 
        IConfigurationService configurationService,
        AppConfig currentConfig,
        GamepadVoiceCoordinator gamepadVoiceCoordinator) : this()
    {
        try
        {
            var viewModel = new ControlSettingsViewModel(
                configurationService, 
                currentConfig, 
                gamepadService, 
                systemService,
                gamepadVoiceCoordinator);
            
            DataContext = viewModel;
            
            _ = viewModel.InitializeAsync();
            
            Log.Debug("ControlSettingsView инициализирован с ViewModel");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при инициализации ControlSettingsView");
        }
    }
}
