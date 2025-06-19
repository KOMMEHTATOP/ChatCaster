using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using ChatCaster.Core.Events;

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

    public bool IsVisible => _overlayWindow?.IsVisible == true;
    public (int X, int Y) CurrentPosition => _overlayWindow != null 
        ? ((int)_overlayWindow.Left, (int)_overlayWindow.Top) 
        : (0, 0);

    public async Task ShowAsync(RecordingStatus status)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
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
                        Console.WriteLine($"Overlay показан для статуса: {status}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка показа overlay: {ex.Message}");
            }
        });
    }

    public async Task HideAsync()
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (_overlayWindow?.IsVisible == true)
                {
                    _overlayWindow.Hide();
                    Console.WriteLine("Overlay скрыт");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка скрытия overlay: {ex.Message}");
            }
        });
    }

    public async Task UpdateStatusAsync(RecordingStatus status, string? message = null)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (_overlayWindow != null)
                {
                    UpdateOverlayStatus(status, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обновления статуса overlay: {ex.Message}");
            }
        });
    }

    public async Task UpdatePositionAsync(int x, int y)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
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
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обновления позиции overlay: {ex.Message}");
            }
        });
    }

    public async Task<bool> ApplyConfigAsync(OverlayConfig config)
    {
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                _currentConfig = config;

                if (_overlayWindow != null)
                {
                    // Применяем настройки к существующему окну
                    ApplyConfigToWindow(_overlayWindow, config);
                }

                Console.WriteLine("Конфигурация overlay применена");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка применения конфигурации overlay: {ex.Message}");
                return false;
            }
        });
    }

    private void CreateOverlayWindow()
    {
        try
        {
            _overlayWindow = new OverlayWindow();
            
            // Применяем конфигурацию если есть
            if (_currentConfig != null)
            {
                ApplyConfigToWindow(_overlayWindow, _currentConfig);
            }
            else
            {
                // Конфигурация по умолчанию
                var defaultConfig = new OverlayConfig
                {
                    Position = OverlayPosition.TopRight,
                    OffsetX = 50,
                    OffsetY = 50,
                    Opacity = 0.9f
                };
                ApplyConfigToWindow(_overlayWindow, defaultConfig);
            }

            Console.WriteLine("Overlay окно создано");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка создания overlay окна: {ex.Message}");
        }
    }

    private void ApplyConfigToWindow(OverlayWindow window, OverlayConfig config)
    {
        try
        {
            // Устанавливаем прозрачность
            window.Opacity = config.Opacity;

            // Позиционирование
            var (x, y) = CalculatePosition(config.Position, config.OffsetX, config.OffsetY);
            window.Left = x;
            window.Top = y;

            Console.WriteLine($"Overlay позиция: {config.Position} ({x}, {y}), прозрачность: {config.Opacity}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка применения конфигурации к окну: {ex.Message}");
        }
    }

    private (int X, int Y) CalculatePosition(OverlayPosition position, int offsetX, int offsetY)
    {
        // Получаем размеры экрана
        var screenWidth = (int)SystemParameters.PrimaryScreenWidth;
        var screenHeight = (int)SystemParameters.PrimaryScreenHeight;
        
        // Размеры overlay окна
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
            _ => (screenWidth - overlayWidth - 50, 50) // По умолчанию TopRight
        };
    }

    private void UpdateOverlayStatus(RecordingStatus status, string? customMessage = null)
    {
        if (_overlayWindow == null) return;

        try
        {
            // Определяем текст и цвет по статусу
            var (text, color, icon) = GetStatusDisplay(status, customMessage);

            // Обновляем UI элементы overlay
            _overlayWindow.UpdateStatus(icon, text, color);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обновления статуса overlay: {ex.Message}");
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    _overlayWindow?.Close();
                    _overlayWindow = null;
                }
                catch
                {
                    // Игнорируем ошибки при закрытии
                }
            });

            _isDisposed = true;
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
        // Настройки окна
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        
        // Размеры
        Width = 200;
        Height = 80;

        // Фон с скругленными углами
        Background = new SolidColorBrush(Color.FromArgb(220, 30, 30, 30)); // Полупрозрачный темный

        // Создаем UI
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
            Text = "Готов"
        };

        stackPanel.Children.Add(_iconText);
        stackPanel.Children.Add(_statusText);
        border.Child = stackPanel;
        Content = border;

        // Эффект тени
        Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 10,
            ShadowDepth = 3,
            Opacity = 0.5
        };
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
            Console.WriteLine($"Ошибка обновления UI overlay: {ex.Message}");
        }
    }
}