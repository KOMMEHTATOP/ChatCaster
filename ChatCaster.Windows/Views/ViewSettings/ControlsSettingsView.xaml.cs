using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChatCaster.Core.Services;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels;
using ChatCaster.Windows.ViewModels.Settings;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class ControlSettingsView : Page
{
    private ControlSettingsViewModel? _viewModel;

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
        // Создаем ViewModel и устанавливаем как DataContext
        _viewModel = new ControlSettingsViewModel(
            configurationService, 
            serviceContext, 
            gamepadService, 
            systemService);
        
        DataContext = _viewModel;
        
        // Инициализируем ViewModel
        _ = _viewModel.InitializeAsync();
    }

    // Обработчики кликов на поля комбинаций - делегируем в ViewModel
    private async void GamepadComboBorder_Click(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel?.StartGamepadCaptureCommand?.CanExecute(null) == true)
        {
            await _viewModel.StartGamepadCaptureCommand.ExecuteAsync(null);
        }
    }

    private async void KeyboardComboBorder_Click(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel?.StartKeyboardCaptureCommand?.CanExecute(null) == true)
        {
            await _viewModel.StartKeyboardCaptureCommand.ExecuteAsync(null);
        }
    }

    // Cleanup при выгрузке страницы
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel?.Cleanup();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при выгрузке ControlSettingsView: {ex.Message}");
        }
    }
}