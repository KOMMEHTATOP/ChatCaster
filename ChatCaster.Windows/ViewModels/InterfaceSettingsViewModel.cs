using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.ViewModels.Base;
using Serilog;

namespace ChatCaster.Windows.ViewModels
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
            if (_overlayService == null)
            {
                Log.Warning("OverlayService недоступен для тестирования");
                return;
            }

            try
            {
                StatusMessage = "Показываем overlay для тестирования...";
                Log.Debug("Начинаем тест overlay");
                
                await _overlayService.ShowAsync(RecordingStatus.Recording);
                await Task.Delay(5000);
                await _overlayService.HideAsync();
                
                StatusMessage = "Тест overlay завершен";
                Log.Information("Тест overlay успешно завершен");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка тестирования overlay: {ex.Message}";
                Log.Error(ex, "Ошибка тестирования overlay");
            }
        }

        [RelayCommand]
        private async Task ShowOverlayPreview()
        {
            if (_overlayService == null || !ShowOverlay)
            {
                Log.Debug("OverlayService недоступен или ShowOverlay выключен");
                return;
            }

            try
            {
                Log.Debug("Показываем предварительный просмотр overlay");
                await _overlayService.ShowAsync(RecordingStatus.Idle);

                // Скрываем через 3 секунды
                await Task.Delay(3000);
                await _overlayService.HideAsync();
                
                Log.Debug("Предварительный просмотр overlay завершен");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка предварительного просмотра overlay");
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

            // ВАЖНО: сначала инициализируем статические данные
            InitializeStaticData();
            
            Log.Debug("InterfaceSettingsViewModel инициализирован");
        }

        #endregion

        #region BaseSettingsViewModel Implementation

        protected override Task LoadPageSpecificSettingsAsync()
        {
            if (_serviceContext?.Config == null)
            {
                Log.Warning("ServiceContext.Config недоступен при загрузке настроек интерфейса");
                // Устанавливаем fallback значение только если конфигурация недоступна
                SelectedPosition = AvailablePositions.FirstOrDefault(p => p.Position == OverlayPosition.TopRight);
                return Task.CompletedTask;
            }

            var config = _serviceContext.Config;

            // Загружаем настройки overlay
            ShowOverlay = config.Overlay.IsEnabled;
            
            // ВАЖНО: загружаем позицию из конфигурации, а НЕ устанавливаем по умолчанию
            SelectedPosition = AvailablePositions.FirstOrDefault(p => p.Position == config.Overlay.Position);
            if (SelectedPosition == null)
            {
                // Fallback только если позиция из конфигурации не найдена
                SelectedPosition = AvailablePositions.FirstOrDefault(p => p.Position == OverlayPosition.TopRight);
                Log.Warning("Позиция из конфигурации {ConfigPosition} не найдена, используем TopRight", config.Overlay.Position);
            }
            
            OverlayOpacity = config.Overlay.Opacity * 100; // Конвертируем 0.0-1.0 в 0-100
            OverlayOpacityText = $"{(int)(config.Overlay.Opacity * 100)}%";

            // Загружаем системные настройки
            ShowNotifications = config.System.ShowNotifications;
            MinimizeToTray = !config.System.AllowCompleteExit; // Инвертируем логику
            StartWithWindows = config.System.StartWithWindows;
            StartMinimized = config.System.StartMinimized;
            
            Log.Information("Настройки интерфейса загружены: Overlay={IsEnabled}, Position={Position}, Notifications={ShowNotifications}", 
                config.Overlay.IsEnabled, SelectedPosition?.DisplayName, config.System.ShowNotifications);
                
            return Task.CompletedTask;
        }

        protected override Task ApplySettingsToConfigAsync(AppConfig config)
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

            Log.Debug("Настройки интерфейса применены к конфигурации");
            return Task.CompletedTask;
        }

        protected override async Task ApplySettingsToServicesAsync()
        {
            // Применяем к overlay сервису
            if (_overlayService != null && _serviceContext?.Config != null)
            {
                await _overlayService.ApplyConfigAsync(_serviceContext.Config.Overlay);
                Log.Debug("Настройки применены к OverlayService");
            }
            else
            {
                Log.Warning("OverlayService или ServiceContext.Config недоступны");
            }
        }

        protected override Task InitializePageSpecificDataAsync()
        {
            // Для этой страницы специальной инициализации не требуется
            Log.Debug("Специальная инициализация для InterfaceSettings не требуется");
            
            // ВАЖНО: Принудительно применяем конфигурацию к OverlayService при первой загрузке
            _ = ApplySettingsToServicesAsync();
            
            return Task.CompletedTask;
        }

        public override void SubscribeToUIEvents()
        {
            // Подписываемся на изменения свойств
            PropertyChanged += OnPropertyChanged;
            Log.Debug("События UI подписаны для InterfaceSettingsViewModel");
        }

        protected override void UnsubscribeFromUIEvents()
        {
            PropertyChanged -= OnPropertyChanged;
            Log.Debug("События UI отписаны для InterfaceSettingsViewModel");
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
                        Log.Debug("Overlay скрыт при cleanup InterfaceSettingsViewModel");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Ошибка скрытия overlay при cleanup");
                    }
                });
            }
            
            Log.Debug("Cleanup InterfaceSettingsViewModel завершен");
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

            // НЕ устанавливаем значение по умолчанию здесь - это будет перебивать загруженную конфигурацию!
            // SelectedPosition будет установлен в LoadPageSpecificSettingsAsync()
            
            Log.Debug("Статические данные для InterfaceSettings инициализированы: {Count} позиций", 
                AvailablePositions.Count);
        }

        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (IsLoadingUI) return;

            // Безопасный fire-and-forget
            _ = HandlePropertyChangedAsync(e);
        }

        private async Task HandlePropertyChangedAsync(System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
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
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка в HandlePropertyChangedAsync для свойства {PropertyName}", e.PropertyName);
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