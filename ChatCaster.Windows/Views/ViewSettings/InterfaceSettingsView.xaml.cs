using System.Windows.Controls;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels;
using Serilog;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class InterfaceSettingsView
{
    public InterfaceSettingsView()
    {
        InitializeComponent();
    }

    // Конструктор с сервисами
    public InterfaceSettingsView(OverlayService overlayService, ConfigurationService configurationService, ServiceContext serviceContext) : this()
    {
        try
        {
            // Создаем ViewModel и устанавливаем как DataContext
            var viewModel = new InterfaceSettingsViewModel(configurationService, serviceContext, overlayService);
            DataContext = viewModel;
            
            // Инициализируем ViewModel
            _ = viewModel.InitializeAsync();
            
            Log.Debug("InterfaceSettingsView инициализирован с ViewModel");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при инициализации InterfaceSettingsView");
        }
    }

    // Event handler для обновления позиции overlay
    private void OverlayPositionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is InterfaceSettingsViewModel viewModel && sender is ComboBox comboBox)
        {
            try
            {
                var selectedItem = comboBox.SelectedItem as ComboBoxItem;
                if (selectedItem?.Tag is string tagValue && Enum.TryParse<OverlayPosition>(tagValue, out var position))
                {
                    // Находим соответствующий OverlayPositionItem
                    var positionItem = viewModel.AvailablePositions.FirstOrDefault(p => p.Position == position);
                    if (positionItem != null)
                    {
                        viewModel.SelectedPosition = positionItem;
                        Log.Debug("Позиция overlay изменена на: {Position}", position);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при изменении позиции overlay");
            }
        }
    }
}