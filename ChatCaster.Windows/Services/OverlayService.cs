using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using ChatCaster.Core.Events;
using Serilog;
using System.Diagnostics;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Реализация overlay сервиса для Windows WPF
/// Показывает индикатор записи поверх всех окон
/// </summary>
public class OverlayService : IOverlayService, IDisposable
{
    public event EventHandler<OverlayPositionChangedEvent>? PositionChanged;

    private OverlayWindow? _overlayWindow;
    private OverlayConfig? _currentConfig;
    private bool _isDisposed;
    
    private IVoiceRecordingService? _voiceService;
    private IConfigurationService? _configService;

    public bool IsVisible => _overlayWindow?.IsVisible == true;
    public (int X, int Y) CurrentPosition => _overlayWindow != null 
        ? ((int)_overlayWindow.Left, (int)_overlayWindow.Top) 
        : (0, 0);

    public void SubscribeToVoiceService(IVoiceRecordingService voiceService, IConfigurationService configService)
    {
        if (_voiceService != null)
        {
            _voiceService.StatusChanged -= OnRecordingStatusChanged;
        }
        
        _voiceService = voiceService;
        _configService = configService;
    
        // Подписываемся на изменения статуса
        voiceService.StatusChanged += OnRecordingStatusChanged;
        Debug.WriteLine("OverlayService подписался на события VoiceRecordingService");
    }

    private async void OnRecordingStatusChanged(object? sender, RecordingStatusChangedEvent e)
    {
        try
        {
            Debug.WriteLine($"OverlayService получил событие: {e.OldStatus} → {e.NewStatus}");
            
            // Проверяем настройки
            if (_configService?.CurrentConfig?.Overlay?.IsEnabled != true)
            {
                Debug.WriteLine("Overlay отключен в настройках, пропускаем");
                return;
            }
            
            switch (e.NewStatus)
            {
                case RecordingStatus.Recording:
                    await ShowAsync(RecordingStatus.Recording);
                    break;
                case RecordingStatus.Processing:
                    await UpdateStatusAsync(RecordingStatus.Processing);
                    break;
                case RecordingStatus.Completed:
                    await UpdateStatusAsync(RecordingStatus.Completed);
                    await Task.Delay(2000); // Показать 2 сек
                    await HideAsync();
                    break;
                case RecordingStatus.Error:
                case RecordingStatus.Cancelled:
                    await UpdateStatusAsync(e.NewStatus);
                    await Task.Delay(1000);
                    await HideAsync();
                    break;
                case RecordingStatus.Idle:
                    await HideAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка в OnRecordingStatusChanged: {ex.Message}");
        }
    }

    public async Task ShowAsync(RecordingStatus status)
    {
        try
        {
            if (Application.Current == null) 
            {
                Debug.WriteLine("Application.Current is null, cannot show overlay");
                return;
            }

            if (Application.Current.Dispatcher.CheckAccess())
            {
                CreateOrShowOverlay(status);
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(() => CreateOrShowOverlay(status));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка показа overlay: {ex.Message}");
        }
    }

    private void CreateOrShowOverlay(RecordingStatus status)
    {
        try
        {
            if (_overlayWindow == null)
            {
                CreateOverlayWindow();
            }
            
            if (_overlayWindow != null)
            {
                UpdateOverlayStatus(status);
                if (!_overlayWindow.IsVisible)
                {
                    _overlayWindow.Show();
                    Debug.WriteLine($"Overlay показан для статуса: {status}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка в CreateOrShowOverlay: {ex.Message}");
        }
    }

    public async Task HideAsync()
    {
        try
        {
            if (Application.Current == null) 
            {
                Debug.WriteLine("Application.Current is null, cannot hide overlay");
                return;
            }

            if (Application.Current.Dispatcher.CheckAccess())
            {
                HideOverlay();
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(HideOverlay);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка скрытия overlay: {ex.Message}");
        }
    }

    private void HideOverlay()
    {
        try
        {
            if (_overlayWindow?.IsVisible == true)
            {
                _overlayWindow.Hide();
                Debug.WriteLine("Overlay скрыт");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка в HideOverlay: {ex.Message}");
        }
    }

    public async Task UpdateStatusAsync(RecordingStatus status, string? message = null)
    {
        try
        {
            if (Application.Current == null) 
            {
                Debug.WriteLine("Application.Current is null, cannot update overlay status");
                return;
            }

            if (Application.Current.Dispatcher.CheckAccess())
            {
                UpdateOverlayStatusInternal(status, message);
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(() => UpdateOverlayStatusInternal(status, message));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка обновления статуса overlay: {ex.Message}");
        }
    }

    private void UpdateOverlayStatusInternal(RecordingStatus status, string? message)
    {
        try
        {
            if (_overlayWindow != null)
            {
                UpdateOverlayStatus(status, message);
                Debug.WriteLine($"Overlay статус обновлен: {status}, сообщение: {message}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка в UpdateOverlayStatusInternal: {ex.Message}");
        }
    }

    public async Task UpdatePositionAsync(int x, int y)
    {
        try
        {
            if (Application.Current == null) 
            {
                Debug.WriteLine("Application.Current is null, cannot update overlay position");
                return;
            }

            if (Application.Current.Dispatcher.CheckAccess())
            {
                UpdatePositionInternal(x, y);
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(() => UpdatePositionInternal(x, y));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка обновления позиции overlay: {ex.Message}");
        }
    }

    private void UpdatePositionInternal(int x, int y)
    {
        try
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
                Debug.WriteLine($"Overlay позиция обновлена: ({x}, {y})");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка в UpdatePositionInternal: {ex.Message}");
        }
    }

    public async Task<bool> ApplyConfigAsync(OverlayConfig config)
    {
        try
        {
            if (Application.Current == null) 
            {
                Debug.WriteLine("Application.Current is null, cannot apply overlay config");
                return false;
            }

            if (Application.Current.Dispatcher.CheckAccess())
            {
                return ApplyConfigInternal(config);
            }
            else
            {
                return await Application.Current.Dispatcher.InvokeAsync(() => ApplyConfigInternal(config));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка применения конфигурации overlay: {ex.Message}");
            return false;
        }
    }

    private bool ApplyConfigInternal(OverlayConfig config)
    {
        try
        {
            Log.Information($"🔍 ApplyConfigInternal вызван: Position={config.Position}");
            _currentConfig = config;
            if (_overlayWindow != null)
            {
                Log.Information($"🔍 Применяем к существующему окну: {config.Position}");
                ApplyConfigToWindow(_overlayWindow, config);
            }
            else
            {
                Log.Information("🔍 Окно еще не создано, сохраняем конфигурацию");
            }
            Debug.WriteLine("Конфигурация overlay применена");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка в ApplyConfigInternal: {ex.Message}");
            return false;
        }
    }
    
    private void CreateOverlayWindow()
    {
        try
        {
            Log.Information($"🔍 CreateOverlayWindow: _currentConfig = {(_currentConfig != null ? $"Position={_currentConfig.Position}" : "NULL")}");
        
            _overlayWindow = new OverlayWindow();
            if (_currentConfig != null)
            {
                Log.Information($"🔍 Применяем сохраненную конфигурацию: {_currentConfig.Position}");
                ApplyConfigToWindow(_overlayWindow, _currentConfig);
            }
            else
            {
                Log.Information("🔍 Применяем дефолтную конфигурацию: TopRight");
                var defaultConfig = new OverlayConfig
                {
                    Position = OverlayPosition.TopRight,
                    OffsetX = 50,
                    OffsetY = 50,
                    Opacity = 0.9f
                };
                ApplyConfigToWindow(_overlayWindow, defaultConfig);
            }
            Debug.WriteLine("Overlay окно создано");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка создания overlay окна: {ex.Message}");
        }
    }

    private void ApplyConfigToWindow(OverlayWindow window, OverlayConfig config)
    {
        try
        {
            window.Opacity = config.Opacity;
            var (x, y) = CalculatePosition(config.Position, config.OffsetX, config.OffsetY);
            window.Left = x;
            window.Top = y;
            Debug.WriteLine($"Overlay позиция: {config.Position} ({x}, {y}), прозрачность: {config.Opacity}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка применения конфигурации к окну: {ex.Message}");
        }
    }

    private (int X, int Y) CalculatePosition(OverlayPosition position, int offsetX, int offsetY)
    {
        try
        {
            var screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            var screenHeight = (int)SystemParameters.PrimaryScreenHeight;
            const int overlayWidth = 200;
            const int overlayHeight = 80;

            return position switch
            {
                OverlayPosition.TopLeft => (offsetX, offsetY),
                OverlayPosition.TopRight => (screenWidth - overlayWidth - offsetX, offsetY),
                OverlayPosition.BottomLeft => (offsetX, screenHeight - overlayHeight - offsetY),
                OverlayPosition.BottomRight => (screenWidth - overlayWidth - offsetX, screenHeight - overlayHeight - offsetY),
                OverlayPosition.TopCenter => (screenWidth / 2 - overlayWidth / 2, offsetY),
                OverlayPosition.BottomCenter => (screenWidth / 2 - overlayWidth / 2, screenHeight - overlayHeight - offsetY),
                OverlayPosition.MiddleLeft => (offsetX, screenHeight / 2 - overlayHeight / 2),
                OverlayPosition.MiddleRight => (screenWidth - overlayWidth - offsetX, screenHeight / 2 - overlayHeight / 2),
                OverlayPosition.MiddleCenter => (screenWidth / 2 - overlayWidth / 2, screenHeight / 2 - overlayHeight / 2),
                _ => (screenWidth - overlayWidth - 50, 50)
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка расчета позиции overlay: {ex.Message}");
            return (50, 50); // Безопасная позиция по умолчанию
        }
    }

    private void UpdateOverlayStatus(RecordingStatus status, string? customMessage = null)
    {
        if (_overlayWindow == null) return;
        try
        {
            var (text, color, icon) = GetStatusDisplay(status, customMessage);
            _overlayWindow.UpdateStatus(icon, text, color);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка обновления статуса overlay: {ex.Message}");
        }
    }

    private (string Text, Brush Color, string Icon) GetStatusDisplay(RecordingStatus status, string? customMessage)
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

    public void Dispose()
    {
        if (!_isDisposed)
        {
            try
            {
                // ✅ ДОБАВЛЯЕМ: Отписываемся от событий при Dispose
                if (_voiceService != null)
                {
                    _voiceService.StatusChanged -= OnRecordingStatusChanged;
                }

                if (Application.Current?.Dispatcher != null)
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        DisposeInternal();
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(DisposeInternal);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка закрытия оверлея: {ex.Message}");
            }
            _isDisposed = true;
        }
    }

    private void DisposeInternal()
    {
        try
        {
            _overlayWindow?.Close();
            _overlayWindow = null;
            Debug.WriteLine("Overlay disposed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка в DisposeInternal: {ex.Message}");
        }
    }
}

/// <summary>
/// WPF окно для overlay индикатора
/// </summary>
public class OverlayWindow : Window
{
    private readonly TextBlock _iconText;
    private readonly TextBlock _statusText;

    public OverlayWindow()
    {
        try
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            Background = new SolidColorBrush(Color.FromArgb(220, 30, 30, 30));

            var border = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Background,
                Padding = new Thickness(15d),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 120, 120, 120)),
                BorderThickness = new Thickness(1)
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _iconText = new TextBlock
            {
                FontSize = 20,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Text = "🎤"
            };

            _statusText = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Text = "Готов",
                MaxWidth = 200,
                TextWrapping = TextWrapping.Wrap
            };

            stackPanel.Children.Add(_iconText);
            stackPanel.Children.Add(_statusText);
            border.Child = stackPanel;
            Content = border;

            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 10,
                ShadowDepth = 3,
                Opacity = 0.5
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка создания OverlayWindow: {ex.Message}");
        }
    }

    public void UpdateStatus(string icon, string text, Brush color)
    {
        try
        {
            _iconText.Text = icon;
            _statusText.Text = text;
            _statusText.Foreground = color;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка обновления UI overlay: {ex.Message}");
        }
    }
}