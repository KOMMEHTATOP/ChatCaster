using System.Windows;
using System.Windows.Input;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels;
using Serilog;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class ControlSettingsView 
{
    private readonly ControlSettingsViewModel _viewModel = null!;

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
            _viewModel = new ControlSettingsViewModel(
                configurationService, 
                serviceContext, 
                gamepadService, 
                systemService);
            
            DataContext = _viewModel;
            
            // Подписываемся на события для отладки
            SubscribeToViewModelEvents();
            
            // Инициализируем ViewModel
            _ = _viewModel.InitializeAsync();
            
            Log.Debug("ControlSettingsView инициализирован с ViewModel");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при инициализации ControlSettingsView");
        }
    }

    #region Подписка на события ViewModel

    private void SubscribeToViewModelEvents()
    {
        try
        {
            _viewModel.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(_viewModel.IsWaitingForKeyboardInput):
                        Log.Debug("IsWaitingForKeyboardInput изменен на: {Value}", _viewModel.IsWaitingForKeyboardInput);
                        break;
                    case nameof(_viewModel.IsWaitingForGamepadInput):
                        Log.Debug("IsWaitingForGamepadInput изменен на: {Value}", _viewModel.IsWaitingForGamepadInput);
                        break;
                    case nameof(_viewModel.KeyboardComboTextColor):
                        Log.Debug("KeyboardComboTextColor изменен на: {Value}", _viewModel.KeyboardComboTextColor);
                        break;
                    case nameof(_viewModel.GamepadComboTextColor):
                        Log.Debug("GamepadComboTextColor изменен на: {Value}", _viewModel.GamepadComboTextColor);
                        break;
                }
            };
            
            Log.Debug("События ViewModel подписаны для ControlSettings");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при подписке на события ViewModel");
        }
    }

    #endregion

    #region Event Handlers

    // Обработчики кликов на поля комбинаций - делегируем в ViewModel
    private void GamepadComboBorder_Click(object sender, MouseButtonEventArgs e)
    {
        Log.Debug("GamepadComboBorder_Click вызван");
        
        // Безопасный fire-and-forget
        _ = HandleGamepadCaptureAsync();
    }

    private void KeyboardComboBorder_Click(object sender, MouseButtonEventArgs e)
    {
        Log.Debug("KeyboardComboBorder_Click вызван");
        
        // Безопасный fire-and-forget
        _ = HandleKeyboardCaptureAsync();
    }

    #endregion

    #region Безопасные async методы

    private async Task HandleGamepadCaptureAsync()
    {
        try
        {
            if (_viewModel.StartGamepadCaptureCommand.CanExecute(null))
            {
                await _viewModel.StartGamepadCaptureCommand.ExecuteAsync(null);
            }
            else
            {
                Log.Debug("StartGamepadCaptureCommand не может быть выполнен");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обработке клика по GamepadComboBorder");
        }
    }

    private async Task HandleKeyboardCaptureAsync()
    {
        try
        {
            if (_viewModel.StartKeyboardCaptureCommand.CanExecute(null))
            {
                await _viewModel.StartKeyboardCaptureCommand.ExecuteAsync(null);
            }
            else
            {
                Log.Debug("StartKeyboardCaptureCommand не может быть выполнен");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при обработке клика по KeyboardComboBorder");
        }
    }

    #endregion

    #region Cleanup

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Cleanup обрабатывается в NavigationManager через DataContext
            Log.Debug("ControlSettingsView выгружен (cleanup управляется NavigationManager)");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при выгрузке ControlSettingsView");
        }
    }

    #endregion
}