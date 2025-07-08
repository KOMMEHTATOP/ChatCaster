using System.Windows;
using System.Windows.Media;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.Core.Services.Overlay;
using Serilog;

namespace ChatCaster.Windows.Services.OverlayService;

/// <summary>
/// Windows WPF реализация overlay сервиса
/// Тонкая обертка над Core компонентами
/// </summary>
public class WindowsOverlayService : IOverlayService, IOverlayDisplay, IDisposable
{
    private readonly static ILogger _logger = Log.ForContext<WindowsOverlayService>();

    public event EventHandler<OverlayPositionChangedEvent>? PositionChanged;

    // Core компоненты
    private readonly OverlayEventCoordinator _eventCoordinator;
    
    // Windows-специфичные компоненты
    private OverlayWindow? _overlayWindow;
    private OverlayConfig? _currentConfig;
    private bool _isDisposed;

    public bool IsVisible => _overlayWindow?.IsVisible == true;
    public (int X, int Y) CurrentPosition => _overlayWindow != null 
        ? ((int)_overlayWindow.Left, (int)_overlayWindow.Top) 
        : (0, 0);

    public WindowsOverlayService(IVoiceRecordingService voiceService, IConfigurationService configService)
    {
        _eventCoordinator = new OverlayEventCoordinator(this); // this реализует IOverlayDisplay
        
        // Автоматическая подписка через DI
        SubscribeToVoiceService(voiceService, configService);
        
        _logger.Debug("WindowsOverlayService создан с Core компонентами");
    }

    #region IOverlayService Implementation

    public void SubscribeToVoiceService(IVoiceRecordingService voiceService, IConfigurationService configService)
    {
        _eventCoordinator.SubscribeToVoiceService(voiceService, configService);
        _logger.Debug("Подписка делегирована OverlayEventCoordinator");
    }

    public async Task<bool> ApplyConfigAsync(OverlayConfig config)
    {
        // Валидация через Core
        var screenWidth = (int)SystemParameters.PrimaryScreenWidth;
        var screenHeight = (int)SystemParameters.PrimaryScreenHeight;
        
        if (!OverlayPositionCalculator.ValidateConfig(config, screenWidth, screenHeight))
        {
            _logger.Warning("Невалидная конфигурация overlay, используем дефолтную");
            config = OverlayPositionCalculator.CreateDefaultConfig();
        }

        _currentConfig = config;

        await DispatcherHelper.InvokeOnUIAsync(() =>
        {
            if (_overlayWindow != null)
            {
                ApplyConfigToWindow(config);
                _logger.Debug("Конфигурация применена к существующему окну");
            }
        });

        _logger.Information("Конфигурация overlay применена успешно");
        return true;
    }

    public async Task UpdatePositionAsync(int x, int y)
    {
        await DispatcherHelper.InvokeOnUIAsync(() =>
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Left = x;
                _overlayWindow.Top = y;
                
                PositionChanged?.Invoke(this, new OverlayPositionChangedEvent
                {
                    NewX = x,
                    NewY = y,
                    Source = "manual"
                });
                
                _logger.Debug("Позиция overlay обновлена: ({X}, {Y})", x, y);
            }
        });
    }

    #endregion

    #region IOverlayDisplay Implementation (для Core)

    public async Task ShowAsync(RecordingStatus status)
    {
        await DispatcherHelper.InvokeOnUIAsync(() =>
        {
            EnsureOverlayWindowExists();
            
            if (_overlayWindow != null)
            {
                UpdateOverlayStatus(status);
                
                if (!_overlayWindow.IsVisible)
                {
                    _overlayWindow.Show();
                    _logger.Debug("Overlay показан для статуса: {Status}", status);
                }
            }
        });
    }

    public async Task HideAsync()
    {
        await DispatcherHelper.InvokeOnUIAsync(() =>
        {
            if (_overlayWindow?.IsVisible == true)
            {
                _overlayWindow.Hide();
                _logger.Debug("Overlay скрыт");
            }
        });
    }

    public async Task UpdateStatusAsync(RecordingStatus status, string? message = null)
    {
        await DispatcherHelper.InvokeOnUIAsync(() =>
        {
            if (_overlayWindow != null)
            {
                UpdateOverlayStatus(status, message);
                _logger.Debug("Статус overlay обновлен: {Status}", status);
            }
        });
    }

    #endregion

    #region Windows-Specific Private Methods

    private void EnsureOverlayWindowExists()
    {
        if (_overlayWindow != null) 
            return;

        _overlayWindow = new OverlayWindow();
        
        var config = _currentConfig ?? OverlayPositionCalculator.CreateDefaultConfig();
        ApplyConfigToWindow(config);
        
        _logger.Debug("Overlay окно создано и настроено");
    }

    private void ApplyConfigToWindow(OverlayConfig config)
    {
        if (_overlayWindow == null)
            return;

        _overlayWindow.Opacity = config.Opacity;
        
        // Используем Core для расчета позиции
        var screenWidth = (int)SystemParameters.PrimaryScreenWidth;
        var screenHeight = (int)SystemParameters.PrimaryScreenHeight;
        var (x, y) = OverlayPositionCalculator.CalculatePosition(
            config.Position, screenWidth, screenHeight, config.OffsetX, config.OffsetY);
        
        _overlayWindow.Left = x;
        _overlayWindow.Top = y;
        
        _logger.Debug("Конфигурация применена: {Position} ({X}, {Y}), прозрачность: {Opacity}", 
            config.Position, x, y, config.Opacity);
    }

    private void UpdateOverlayStatus(RecordingStatus status, string? customMessage = null)
    {
        if (_overlayWindow == null) 
            return;
            
        var (text, color, icon) = GetStatusDisplay(status, customMessage);
        _overlayWindow.UpdateStatus(icon, text, color);
    }

    private static (string Text, Brush Color, string Icon) GetStatusDisplay(RecordingStatus status, string? customMessage)
    {
        if (!string.IsNullOrEmpty(customMessage))
        {
            return (customMessage, Brushes.White, "🎤");
        }
        
        return status switch
        {
            RecordingStatus.Idle => ("Готов", Brushes.LimeGreen, "🎤"),
            RecordingStatus.Recording => ("Запись...", Brushes.OrangeRed, "🔴"),
            RecordingStatus.Processing => ("Обработка...", Brushes.Yellow, "⚡"),
            RecordingStatus.Completed => ("Готово!", Brushes.LimeGreen, "✅"),
            RecordingStatus.Error => ("Ошибка", Brushes.Red, "❌"),
            RecordingStatus.Cancelled => ("Отменено", Brushes.Gray, "🚫"),
            _ => ("Неизвестно", Brushes.White, "❓")
        };
    }

    #endregion

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _eventCoordinator.Dispose();

            DispatcherHelper.InvokeOnUI(() =>
            {
                _overlayWindow?.Close();
                _overlayWindow = null;
            });

            _logger.Debug("WindowsOverlayService disposed");
            _isDisposed = true;
        }
    }
}