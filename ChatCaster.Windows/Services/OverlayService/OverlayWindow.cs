using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Serilog;

namespace ChatCaster.Windows.Services.OverlayService;

/// <summary>
/// WPF окно для overlay индикатора
/// Показывает статус записи поверх всех окон
/// </summary>
public class OverlayWindow : Window
{
    private readonly static ILogger _logger = Log.ForContext<OverlayWindow>();
    
    private readonly TextBlock _iconText;
    private readonly TextBlock _statusText;

    public OverlayWindow()
    {
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

        try
        {
            InitializeWindow();
            CreateContent();
            ApplyEffects();
            
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка создания OverlayWindow");
        }
    }

    /// <summary>
    /// Обновляет отображаемый статус overlay
    /// </summary>
    /// <param name="icon">Иконка статуса</param>
    /// <param name="text">Текст статуса</param>
    /// <param name="color">Цвет текста</param>
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
            _logger.Error(ex, "Ошибка обновления UI overlay");
        }
    }

    #region Private Methods

    /// <summary>
    /// Инициализирует базовые свойства окна
    /// </summary>
    private void InitializeWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = new SolidColorBrush(Color.FromArgb(220, 30, 30, 30));
        ShowActivated = false;      // Не активировать окно при показе
        Focusable = false;         // Запретить фокус
        IsHitTestVisible = false;  // Сделать окно некликабельным
    }

    /// <summary>
    /// Создает содержимое окна
    /// </summary>
    private void CreateContent()
    {
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

        stackPanel.Children.Add(_iconText);
        stackPanel.Children.Add(_statusText);
        border.Child = stackPanel;
        Content = border;
    }

    /// <summary>
    /// Применяет визуальные эффекты
    /// </summary>
    private void ApplyEffects()
    {
        Effect = new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 10,
            ShadowDepth = 3,
            Opacity = 0.5
        };
    }

    #endregion
}