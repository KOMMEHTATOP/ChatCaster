using ChatCaster.Core.Models;

namespace ChatCaster.Core.Services.Overlay;

/// <summary>
/// Утилиты для расчета позиций overlay на экране (кроссплатформенные)
/// </summary>
public static class OverlayPositionCalculator
{
    private const int DefaultOverlayWidth = 200;
    private const int DefaultOverlayHeight = 80;

    /// <summary>
    /// Рассчитывает позицию overlay на экране
    /// </summary>
    /// <param name="position">Позиция на экране</param>
    /// <param name="screenWidth">Ширина экрана</param>
    /// <param name="screenHeight">Высота экрана</param>
    /// <param name="offsetX">Смещение по X</param>
    /// <param name="offsetY">Смещение по Y</param>
    /// <param name="overlayWidth">Ширина overlay (опционально)</param>
    /// <param name="overlayHeight">Высота overlay (опционально)</param>
    /// <returns>Координаты (X, Y) для размещения overlay</returns>
    public static (int X, int Y) CalculatePosition(
        OverlayPosition position, 
        int screenWidth, 
        int screenHeight,
        int offsetX, 
        int offsetY,
        int overlayWidth = DefaultOverlayWidth,
        int overlayHeight = DefaultOverlayHeight)
    {
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
            _ => (screenWidth - overlayWidth - 50, 50) // Безопасная позиция по умолчанию
        };
    }

    /// <summary>
    /// Создает конфигурацию overlay по умолчанию
    /// </summary>
    /// <returns>Конфигурация overlay по умолчанию</returns>
    public static OverlayConfig CreateDefaultConfig()
    {
        return new OverlayConfig
        {
            Position = OverlayPosition.TopRight,
            OffsetX = 50,
            OffsetY = 50,
            Opacity = 0.9f,
            IsEnabled = true
        };
    }

    /// <summary>
    /// Проверяет валидность конфигурации overlay
    /// </summary>
    /// <param name="config">Конфигурация для проверки</param>
    /// <param name="screenWidth">Ширина экрана</param>
    /// <param name="screenHeight">Высота экрана</param>
    /// <returns>True если конфигурация валидна</returns>
    public static bool ValidateConfig(OverlayConfig config, int screenWidth, int screenHeight)
    {
        // Проверяем прозрачность
        if (config.Opacity < 0.1f || config.Opacity > 1.0f)
            return false;

        // Проверяем смещения
        if (config.OffsetX < 0 || config.OffsetY < 0)
            return false;

        // Проверяем что смещения не превышают размеры экрана
        if (config.OffsetX > screenWidth || config.OffsetY > screenHeight)
            return false;

        return true;
    }

    /// <summary>
    /// Получает размеры overlay по умолчанию
    /// </summary>
    /// <returns>Кортеж (ширина, высота)</returns>
    public static (int Width, int Height) GetDefaultSize()
    {
        return (DefaultOverlayWidth, DefaultOverlayHeight);
    }
}