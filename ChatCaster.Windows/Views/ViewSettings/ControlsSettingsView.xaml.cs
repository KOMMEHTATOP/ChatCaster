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
        
        // ✅ ДОБАВИМ отладку свойств для анимации
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsWaitingForKeyboardInput))
            {
                System.Diagnostics.Debug.WriteLine($"🎨 IsWaitingForKeyboardInput изменен на: {_viewModel.IsWaitingForKeyboardInput}");
            }
            if (e.PropertyName == nameof(_viewModel.IsWaitingForGamepadInput))
            {
                System.Diagnostics.Debug.WriteLine($"🎨 IsWaitingForGamepadInput изменен на: {_viewModel.IsWaitingForGamepadInput}");
            }
            if (e.PropertyName == nameof(_viewModel.KeyboardComboTextColor))
            {
                System.Diagnostics.Debug.WriteLine($"🎨 KeyboardComboTextColor изменен на: {_viewModel.KeyboardComboTextColor}");
            }
            if (e.PropertyName == nameof(_viewModel.GamepadComboTextColor))
            {
                System.Diagnostics.Debug.WriteLine($"🎨 GamepadComboTextColor изменен на: {_viewModel.GamepadComboTextColor}");
            }
        };
        
        // Инициализируем ViewModel
        _ = _viewModel.InitializeAsync();
    }

    // Обработчики кликов на поля комбинаций - делегируем в ViewModel
    private async void GamepadComboBorder_Click(object sender, MouseButtonEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"🎨 GamepadComboBorder_Click вызван");
        if (_viewModel?.StartGamepadCaptureCommand?.CanExecute(null) == true)
        {
            await _viewModel.StartGamepadCaptureCommand.ExecuteAsync(null);
        }
    }

    private async void KeyboardComboBorder_Click(object sender, MouseButtonEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"🎨 KeyboardComboBorder_Click вызван");
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