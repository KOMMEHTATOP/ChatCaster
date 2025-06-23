using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels.Base;

namespace ChatCaster.Windows.ViewModels.Settings
{
    public partial class InterfaceSettingsViewModel : BaseSettingsViewModel
    {
        #region Private Services
        private readonly OverlayService? _overlayService;
        #endregion

        #region Observable Properties

        [ObservableProperty]
        private bool _showOverlay = true;

        [ObservableProperty]
        private ObservableCollection<OverlayPositionItem> _availablePositions = new();

        [ObservableProperty]
        private OverlayPositionItem? _selectedPosition;

        [ObservableProperty]
        private double _overlayOpacity = 90;

        [ObservableProperty]
        private string _overlayOpacityText = "90%";

        [ObservableProperty]
        private bool _showNotifications = true;

        [ObservableProperty]
        private bool _minimizeToTray = true;

        [ObservableProperty]
        private bool _startWithWindows = true;

        [ObservableProperty]
        private bool _startMinimized = false;

        #endregion

        #region Commands

        [RelayCommand]
        private async Task TestOverlay()
        {
            if (_overlayService == null) return;

            try
            {
                StatusMessage = "Показываем overlay для тестирования...";
                await _overlayService.ShowAsync(RecordingStatus.Recording);
                
                await Task.Delay(5000);
                await _overlayService.HideAsync();
                StatusMessage = "Тест overlay завершен";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка тестирования overlay: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ShowOverlayPreview()
        {
            if (_overlayService == null || !ShowOverlay) return;

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

        #endregion

        #region Constructor
        public InterfaceSettingsViewModel(
            ConfigurationService? configurationService,
            ServiceContext? serviceContext,
            OverlayService? overlayService) : base(configurationService, serviceContext)
        {
            _overlayService = overlayService;
            
            InitializeStaticData();
        }
        #endregion

        #region BaseSettingsViewModel Implementation

        protected override async Task LoadPageSpecificSettingsAsync()
        {
            if (_serviceContext?.Config == null) return;

            var config = _serviceContext.Config;

            // Загружаем настройки overlay
            ShowOverlay = config.Overlay.IsEnabled;
            SelectedPosition = AvailablePositions.FirstOrDefault(p => p.Position == config.Overlay.Position);
            OverlayOpacity = config.Overlay.Opacity * 100; // Конвертируем 0.0-1.0 в 0-100
            OverlayOpacityText = $"{(int)(config.Overlay.Opacity * 100)}%";

            // Загружаем системные настройки
            ShowNotifications = config.System.ShowNotifications;
            MinimizeToTray = !config.System.AllowCompleteExit; // Инвертируем логику
            StartWithWindows = config.System.StartWithWindows;
            StartMinimized = config.System.StartMinimized;

            Console.WriteLine("Настройки интерфейса загружены");
        }

        protected override async Task ApplySettingsToConfigAsync(AppConfig config)
        {
            // Обновляем настройки overlay
            config.Overlay.IsEnabled = ShowOverlay;
            config.Overlay.Position = SelectedPosition?.Position ?? OverlayPosition.TopRight;
            config.Overlay.Opacity = (float)(OverlayOpacity / 100.0);
            
            // Обновляем системные настройки
            config.System.ShowNotifications = ShowNotifications;
            config.System.AllowCompleteExit = !MinimizeToTray;
            config.System.StartWithWindows = StartWithWindows;
            config.System.StartMinimized = StartMinimized;

            await Task.CompletedTask;
        }

        protected override async Task ApplySettingsToServicesAsync()
        {
            // Применяем к overlay сервису
            if (_overlayService != null && _serviceContext?.Config != null)
            {
                await _overlayService.ApplyConfigAsync(_serviceContext.Config.Overlay);
            }
        }

        protected override async Task InitializePageSpecificDataAsync()
        {
            // Для этой страницы специальной инициализации не требуется
            await Task.CompletedTask;
        }

        public override void SubscribeToUIEvents()
        {
            // Подписываемся на изменения свойств
            PropertyChanged += OnPropertyChanged;
        }

        protected override void UnsubscribeFromUIEvents()
        {
            PropertyChanged -= OnPropertyChanged;
        }

        protected override void CleanupPageSpecific()
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

        #endregion

        #region Private Methods

        private void InitializeStaticData()
        {
            // Инициализируем доступные позиции overlay
            AvailablePositions.Clear();
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.TopLeft, "Верхний левый"));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.TopCenter, "Верхний центр"));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.TopRight, "Верхний правый"));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.MiddleLeft, "Средний левый"));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.MiddleCenter, "Центр"));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.MiddleRight, "Средний правый"));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.BottomLeft, "Нижний левый"));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.BottomCenter, "Нижний центр"));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.BottomRight, "Нижний правый"));

            // Устанавливаем значение по умолчанию
            SelectedPosition = AvailablePositions.FirstOrDefault(p => p.Position == OverlayPosition.TopRight);
        }

        private async void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (IsLoadingUI) return;

            switch (e.PropertyName)
            {
                case nameof(ShowOverlay):
                case nameof(SelectedPosition):
                case nameof(OverlayOpacity):
                case nameof(ShowNotifications):
                case nameof(MinimizeToTray):
                case nameof(StartWithWindows):
                case nameof(StartMinimized):
                    await OnUISettingChangedAsync();
                    
                    // Если изменили настройку overlay, показываем предварительный просмотр
                    if (e.PropertyName == nameof(ShowOverlay) && ShowOverlay)
                    {
                        await ShowOverlayPreview();
                    }
                    break;
            }
        }

        partial void OnOverlayOpacityChanged(double value)
        {
            OverlayOpacityText = $"{(int)value}%";
        }

        #endregion
    }

    #region Helper Classes

    public class OverlayPositionItem
    {
        public OverlayPosition Position { get; }
        public string DisplayName { get; }

        public OverlayPositionItem(OverlayPosition position, string displayName)
        {
            Position = position;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }

    #endregion
}