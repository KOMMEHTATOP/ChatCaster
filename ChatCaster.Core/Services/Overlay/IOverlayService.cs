using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Core;

namespace ChatCaster.Core.Services.Overlay;

/// <summary>
/// Сервис overlay индикатора (Platform-specific)
/// </summary>
public interface IOverlayService
{
    event EventHandler<OverlayPositionChangedEvent>? PositionChanged;

    Task ShowAsync(RecordingStatus status);
    Task HideAsync();
    Task UpdateStatusAsync(RecordingStatus status, string? message = null);
    Task UpdatePositionAsync(int x, int y);

    Task<bool> ApplyConfigAsync(OverlayConfig config);

    /// <summary>
    /// Подписывается на события VoiceRecordingService для автоматического управления overlay
    /// </summary>
    /// <param name="voiceService">Сервис записи голоса</param>
    /// <param name="configService">Сервис конфигурации для проверки настроек overlay</param>
    void SubscribeToVoiceService(IVoiceRecordingService voiceService, IConfigurationService configService);

    bool IsVisible { get; }
    (int X, int Y) CurrentPosition { get; }
}
