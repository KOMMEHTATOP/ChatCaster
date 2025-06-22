using System.Windows;
using System.Windows.Controls;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;

namespace ChatCaster.Windows.Views.ViewSettings;

public partial class InterfaceSettingsView : Page
{
    private readonly OverlayService? _overlayService;
    private readonly ConfigurationService? _configurationService;
    private readonly ServiceContext? _serviceContext;
    
    private bool _isLoadingUI = false; // Флаг чтобы не применять настройки во время загрузки UI

    public InterfaceSettingsView()
    {
        InitializeComponent();
        LoadInitialData();
    }

    // Конструктор с сервисами
    public InterfaceSettingsView(OverlayService overlayService, ConfigurationService configurationService, ServiceContext serviceContext) : this()
    {
        _overlayService = overlayService;
        _configurationService = configurationService;
        _serviceContext = serviceContext;
        _ = LoadCurrentSettings(); // Fire-and-forget для конструктора
    }

    private void LoadInitialData()
    {
        _isLoadingUI = true;
        
        // Устанавливаем значения по умолчанию
        ShowOverlayCheckBox.IsChecked = true;
        OverlayPositionComboBox.SelectedIndex = 2;
        OverlayOpacitySlider.Value = 90;
        OverlayOpacityValueText.Text = "90%";
        
        ShowNotificationsCheckBox.IsChecked = true;
        MinimizeToTrayCheckBox.IsChecked = true; // AllowCompleteExit по умолчанию false
        StartWithWindowsCheckBox.IsChecked = true;
        StartMinimizedCheckBox.IsChecked = false;

        _isLoadingUI = false;
    }

    private async Task LoadCurrentSettings()
    {
        try
        {
            _isLoadingUI = true;
            
            if (_serviceContext?.Config == null) return;

            var config = _serviceContext.Config;

            // Загружаем настройки overlay
            ShowOverlayCheckBox.IsChecked = config.Overlay.IsEnabled;
            SetOverlayPosition(config.Overlay.Position);
            OverlayOpacitySlider.Value = config.Overlay.Opacity * 100; // Конвертируем 0.0-1.0 в 0-100
            OverlayOpacityValueText.Text = $"{(int)(config.Overlay.Opacity * 100)}%";

            // Загружаем системные настройки
            ShowNotificationsCheckBox.IsChecked = config.System.ShowNotifications;
            MinimizeToTrayCheckBox.IsChecked = !config.System.AllowCompleteExit; // Инвертируем логику
            StartWithWindowsCheckBox.IsChecked = config.System.StartWithWindows;
            StartMinimizedCheckBox.IsChecked = config.System.StartMinimized;

            // Подписываемся на события изменения UI
            SubscribeToUIEvents();

            Console.WriteLine("Настройки интерфейса загружены");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки настроек интерфейса: {ex.Message}");
        }
        finally
        {
            _isLoadingUI = false;
        }
    }

    private void SubscribeToUIEvents()
    {
        // Подписываемся на все события изменения настроек
        ShowOverlayCheckBox.Checked += OnSettingChanged;
        ShowOverlayCheckBox.Unchecked += OnSettingChanged;
        OverlayPositionComboBox.SelectionChanged += OnSelectionChanged;
        OverlayOpacitySlider.ValueChanged += OnSliderChanged;
        
        ShowNotificationsCheckBox.Checked += OnSettingChanged;
        ShowNotificationsCheckBox.Unchecked += OnSettingChanged;
        MinimizeToTrayCheckBox.Checked += OnSettingChanged;
        MinimizeToTrayCheckBox.Unchecked += OnSettingChanged;
        StartWithWindowsCheckBox.Checked += OnSettingChanged;
        StartWithWindowsCheckBox.Unchecked += OnSettingChanged;
        StartMinimizedCheckBox.Checked += OnSettingChanged;
        StartMinimizedCheckBox.Unchecked += OnSettingChanged;
    }

    // Обработчики автоматического применения настроек
    private async void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingUI) return;

        await ApplyCurrentSettingsAsync();

        // Если изменили настройку overlay, показываем предварительный просмотр
        if (sender == ShowOverlayCheckBox && ShowOverlayCheckBox.IsChecked == true)
        {
            await ShowOverlayPreview();
        }
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUI) return;
        await ApplyCurrentSettingsAsync();
    }

    private async void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingUI) return;
        
        // Обновляем текст рядом со слайдером
        if (OverlayOpacityValueText != null)
        {
            OverlayOpacityValueText.Text = $"{(int)e.NewValue}%";
        }
        
        await ApplyCurrentSettingsAsync();
    }

    private async Task ApplyCurrentSettingsAsync()
    {
        try
        {
            if (_configurationService == null || _serviceContext?.Config == null) 
                return;

            var config = _serviceContext.Config; // Используем существующий объект

            // Обновляем настройки overlay
            config.Overlay.IsEnabled = ShowOverlayCheckBox.IsChecked ?? false;
            config.Overlay.Position = GetSelectedOverlayPosition();
            config.Overlay.Opacity = (float)(OverlayOpacitySlider.Value / 100.0);
            
            // Обновляем системные настройки
            config.System.ShowNotifications = ShowNotificationsCheckBox.IsChecked ?? false;
            config.System.AllowCompleteExit = !(MinimizeToTrayCheckBox.IsChecked ?? false);
            config.System.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            config.System.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;

            // Сохраняем конфигурацию
            await _configurationService.SaveConfigAsync(config);

            // Применяем к overlay сервису
            if (_overlayService != null)
                await _overlayService.ApplyConfigAsync(config.Overlay);

            Console.WriteLine("Настройки интерфейса автоматически сохранены и применены");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка автоприменения настроек: {ex.Message}");
        }
    }

    private async Task ShowOverlayPreview()
    {
        if (_overlayService == null) return;

        try
        {
            await _overlayService.ShowAsync(RecordingStatus.Idle);
            
            // Скрываем через 3 секунды
            await Task.Delay(3000);
            await _overlayService.HideAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка предварительного просмотра overlay: {ex.Message}");
        }
    }

    private void SetOverlayPosition(OverlayPosition position)
    {
        foreach (ComboBoxItem item in OverlayPositionComboBox.Items)
        {
            if (item.Tag is string tag && Enum.TryParse<OverlayPosition>(tag, out var itemPosition) && itemPosition == position)
            {
                item.IsSelected = true;
                break;
            }
        }
    }

    private OverlayPosition GetSelectedOverlayPosition()
    {
        var selectedItem = OverlayPositionComboBox.SelectedItem as ComboBoxItem;
        if (selectedItem?.Tag is string tag && Enum.TryParse<OverlayPosition>(tag, out var position))
        {
            return position;
        }
        return OverlayPosition.TopRight; // По умолчанию
    }

    // Тестирование overlay (можно оставить кнопку если есть в XAML)
    private async void TestOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_overlayService == null) return;

        try
        {
            await _overlayService.ShowAsync(RecordingStatus.Recording);
            
            MessageBox.Show("Overlay показан на 5 секунд для тестирования", "ChatCaster", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            
            await Task.Delay(5000);
            await _overlayService.HideAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка тестирования overlay: {ex.Message}", "ChatCaster", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // Cleanup при выгрузке страницы
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Скрываем overlay если он показан
            if (_overlayService != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _overlayService.HideAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка скрытия overlay при выгрузке: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при выгрузке InterfaceSettingsView: {ex.Message}");
        }
    }
}