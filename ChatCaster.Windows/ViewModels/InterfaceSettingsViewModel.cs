using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.Overlay;
using ChatCaster.Core.Services.System;
using ChatCaster.Windows.ViewModels.Base;
using Serilog;
using System.ComponentModel;

namespace ChatCaster.Windows.ViewModels
{
    public partial class InterfaceSettingsViewModel : BaseSettingsViewModel
    {
        #region Private Services

        private readonly IOverlayService _overlayService;
        private bool _isUpdatingLanguage;

        #endregion
        private readonly ILocalizationService _localizationService;

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
        private bool _startMinimized;

        // Локализованные свойства
        [ObservableProperty]
        private string _pageTitle = "Интерфейс";

        [ObservableProperty]
        private string _pageDescription = "Настройка overlay индикатора и системных параметров";

        [ObservableProperty]
        private string _overlayIndicatorTitle = "Overlay индикатор";

        [ObservableProperty]
        private string _showOverlayText = "Показывать overlay во время записи";

        [ObservableProperty]
        private string _positionLabel = "Позиция:";

        [ObservableProperty]
        private string _visibilityLabel = "Видимость:";

        [ObservableProperty]
        private string _showNotificationsText = "Показывать уведомления";

        [ObservableProperty]
        private string _minimizeToTrayText = "Кнопка закрытия (X) сворачивает в трей";

        [ObservableProperty]
        private string _startWithWindowsText = "Запускать с Windows";

        [ObservableProperty]
        private string _startMinimizedText = "Запускать свернутым в трей";

        [ObservableProperty]
        private string _positionTopLeft = "Верхний левый";

        [ObservableProperty]
        private string _positionTopCenter = "Верхний центр";

        [ObservableProperty]
        private string _positionTopRight = "Верхний правый";

        [ObservableProperty]
        private string _positionMiddleLeft = "Средний левый";

        [ObservableProperty]
        private string _positionMiddleCenter = "Центр";

        [ObservableProperty]
        private string _positionMiddleRight = "Средний правый";

        [ObservableProperty]
        private string _positionBottomLeft = "Нижний левый";

        [ObservableProperty]
        private string _positionBottomCenter = "Нижний центр";

        [ObservableProperty]
        private string _positionBottomRight = "Нижний правый";

        #endregion

        #region Commands

        [RelayCommand]
        private async Task TestOverlay()
        {
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
            if (!ShowOverlay)
            {
                Log.Debug("ShowOverlay выключен");
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
            IConfigurationService configurationService,
            AppConfig currentConfig,
            IOverlayService overlayService,
            ILocalizationService localizationService) : base(configurationService, currentConfig)
        {
            _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
            _localizationService = localizationService;
            _localizationService.LanguageChanged += OnLanguageChanged;
            UpdateLocalizedStrings();

            // ВАЖНО: сначала инициализируем статические данные
            InitializeStaticData();
            
            Log.Debug("InterfaceSettingsViewModel инициализирован");
        }

        #endregion

        #region BaseSettingsViewModel Implementation

        protected override Task LoadPageSpecificSettingsAsync()
        {
            // Загружаем настройки overlay
            ShowOverlay = _currentConfig.Overlay.IsEnabled;
            
            // ВАЖНО: загружаем позицию из конфигурации
            SelectedPosition = AvailablePositions.FirstOrDefault(p => p.Position == _currentConfig.Overlay.Position);
            if (SelectedPosition == null)
            {
                // Fallback только если позиция из конфигурации не найдена
                SelectedPosition = AvailablePositions.FirstOrDefault(p => p.Position == OverlayPosition.TopRight);
                Log.Warning("Позиция из конфигурации {ConfigPosition} не найдена, используем TopRight", _currentConfig.Overlay.Position);
            }
            
            OverlayOpacity = _currentConfig.Overlay.Opacity * 100; // Конвертируем 0.0-1.0 в 0-100
            OverlayOpacityText = $"{(int)(_currentConfig.Overlay.Opacity * 100)}%";

            // Загружаем системные настройки
            ShowNotifications = _currentConfig.System.ShowNotifications;
            MinimizeToTray = !_currentConfig.System.AllowCompleteExit; // Инвертируем логику
            StartWithWindows = _currentConfig.System.StartWithSystem; // Исправлено имя свойства
            StartMinimized = _currentConfig.System.StartMinimized;
            
            Log.Information("Настройки интерфейса загружены: Overlay={IsEnabled}, Position={Position}, Notifications={ShowNotifications}", 
                _currentConfig.Overlay.IsEnabled, SelectedPosition?.DisplayName, _currentConfig.System.ShowNotifications);
                
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
            config.System.StartWithSystem = StartWithWindows; 
            config.System.StartMinimized = StartMinimized;

            config.System.SelectedLanguage = _currentConfig.System.SelectedLanguage;
            
            Log.Debug("Настройки интерфейса применены к конфигурации, язык сохранен: {Language}", 
                config.System.SelectedLanguage);
            return Task.CompletedTask;
        }

        protected override async Task ApplySettingsToServicesAsync()
        {
            // Применяем к overlay сервису
            await _overlayService.ApplyConfigAsync(_currentConfig.Overlay);
            Log.Debug("Настройки применены к OverlayService");
        }

        protected override Task InitializePageSpecificDataAsync()
        {
            // Принудительно применяем конфигурацию к OverlayService при первой загрузке
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
            _ = Task.Run(async () =>
            {
                try
                {
                    await _overlayService.HideAsync();
                    _localizationService.LanguageChanged -= OnLanguageChanged;
                    base.CleanupPageSpecific();

                    Log.Debug("Overlay скрыт при cleanup InterfaceSettingsViewModel");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка скрытия overlay при cleanup");
                }
            });
            
            Log.Debug("Cleanup InterfaceSettingsViewModel завершен");
        }

        #endregion

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingLanguage) return; 
        
            _isUpdatingLanguage = true;
            try
            {
                UpdateLocalizedStrings();
            }
            finally
            {
                _isUpdatingLanguage = false;
            }
        }

        private void UpdateLocalizedStrings()
        {
            PageTitle = _localizationService.GetString("Interface_PageTitle");
            PageDescription = _localizationService.GetString("Interface_PageDescription");
            OverlayIndicatorTitle = _localizationService.GetString("Interface_OverlayIndicator");
            ShowOverlayText = _localizationService.GetString("Interface_ShowOverlay");
            PositionLabel = _localizationService.GetString("Interface_Position");
            VisibilityLabel = _localizationService.GetString("Interface_Visibility");
            ShowNotificationsText = _localizationService.GetString("Interface_ShowNotifications");
            MinimizeToTrayText = _localizationService.GetString("Interface_MinimizeToTray");
            StartWithWindowsText = _localizationService.GetString("Interface_StartWithWindows");
            StartMinimizedText = _localizationService.GetString("Interface_StartMinimized");
            
            PositionTopLeft = _localizationService.GetString("Interface_Position_TopLeft");
            PositionTopCenter = _localizationService.GetString("Interface_Position_TopCenter");
            PositionTopRight = _localizationService.GetString("Interface_Position_TopRight");
            PositionMiddleLeft = _localizationService.GetString("Interface_Position_MiddleLeft");
            PositionMiddleCenter = _localizationService.GetString("Interface_Position_MiddleCenter");
            PositionMiddleRight = _localizationService.GetString("Interface_Position_MiddleRight");
            PositionBottomLeft = _localizationService.GetString("Interface_Position_BottomLeft");
            PositionBottomCenter = _localizationService.GetString("Interface_Position_BottomCenter");
            PositionBottomRight = _localizationService.GetString("Interface_Position_BottomRight");
            UpdateOverlayPositions();

        }

        
        #region Private Methods

        private void InitializeStaticData()
        {
            // Инициализируем доступные позиции overlay
            AvailablePositions.Clear();
            // Используем локализованные строки
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.TopLeft, PositionTopLeft));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.TopCenter, PositionTopCenter));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.TopRight, PositionTopRight));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.MiddleLeft, PositionMiddleLeft));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.MiddleCenter, PositionMiddleCenter));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.MiddleRight, PositionMiddleRight));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.BottomLeft, PositionBottomLeft));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.BottomCenter, PositionBottomCenter));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.BottomRight, PositionBottomRight));

            // SelectedPosition будет установлен в LoadPageSpecificSettingsAsync()
            Log.Debug("Статические данные для InterfaceSettings инициализированы: {Count} позиций", 
                AvailablePositions.Count);
        }
        
        private void UpdateOverlayPositions()
        {
            if (_isUpdatingLanguage) return;
            
            // Сохраняем текущую выбранную позицию
            var currentPosition = SelectedPosition?.Position;
    
            // Очищаем и заново создаем список с новыми локализованными названиями
            AvailablePositions.Clear();
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.TopLeft, PositionTopLeft));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.TopCenter, PositionTopCenter));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.TopRight, PositionTopRight));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.MiddleLeft, PositionMiddleLeft));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.MiddleCenter, PositionMiddleCenter));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.MiddleRight, PositionMiddleRight));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.BottomLeft, PositionBottomLeft));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.BottomCenter, PositionBottomCenter));
            AvailablePositions.Add(new OverlayPositionItem(OverlayPosition.BottomRight, PositionBottomRight));
    
            // Восстанавливаем выбранную позицию
            if (currentPosition.HasValue)
            {
                SelectedPosition = AvailablePositions.FirstOrDefault(p => p.Position == currentPosition.Value);
            }
    
            Log.Debug("Позиции overlay обновлены для нового языка");
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IsLoadingUI) return;

            // Безопасный fire-and-forget
            _ = HandlePropertyChangedAsync(e);
        }

        private async Task HandlePropertyChangedAsync(PropertyChangedEventArgs e)
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