using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Core;
using Serilog;

namespace ChatCaster.Core.Services.Overlay;

/// <summary>
/// Кроссплатформенный координатор событий overlay
/// Обрабатывает события VoiceRecordingService и управляет отображением
/// </summary>
public class OverlayEventCoordinator : IDisposable
{
    private readonly static ILogger _logger = Log.ForContext<OverlayEventCoordinator>();
    
    private readonly IOverlayDisplay _overlayDisplay;
    private IVoiceRecordingService? _voiceService;
    private IConfigurationService? _configService;
    private bool _isDisposed;

    public OverlayEventCoordinator(IOverlayDisplay overlayDisplay)
    {
        _overlayDisplay = overlayDisplay ?? throw new ArgumentNullException(nameof(overlayDisplay));
    }

    /// <summary>
    /// Подписывается на события VoiceRecordingService
    /// </summary>
    /// <param name="voiceService">Сервис записи голоса</param>
    /// <param name="configService">Сервис конфигурации</param>
    public void SubscribeToVoiceService(IVoiceRecordingService voiceService, IConfigurationService configService)
    {
        // Отписываемся от предыдущего сервиса если есть
        if (_voiceService != null)
        {
            _voiceService.StatusChanged -= OnRecordingStatusChanged;
        }
        
        _voiceService = voiceService;
        _configService = configService;
    
        // Подписываемся на события
        voiceService.StatusChanged += OnRecordingStatusChanged;
        _logger.Debug("OverlayEventCoordinator подписался на события VoiceRecordingService");
    }

    /// <summary>
    /// Отписывается от событий VoiceRecordingService
    /// </summary>
    public void UnsubscribeFromVoiceService()
    {
        if (_voiceService != null)
        {
            _voiceService.StatusChanged -= OnRecordingStatusChanged;
            _voiceService = null;
            _configService = null;
            _logger.Debug("OverlayEventCoordinator отписался от событий VoiceRecordingService");
        }
    }

    /// <summary>
    /// Обработчик изменения статуса записи
    /// </summary>
    private async void OnRecordingStatusChanged(object? sender, RecordingStatusChangedEvent e)
    {
        try
        {
            _logger.Debug("Получено событие изменения статуса: {OldStatus} → {NewStatus}", e.OldStatus, e.NewStatus);
            
            // Проверяем включен ли overlay в настройках
            if (!IsOverlayEnabled())
            {
                _logger.Debug("Overlay отключен в настройках, пропускаем событие");
                return;
            }
            
            await ProcessStatusChange(e.NewStatus);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка обработки события изменения статуса записи");
        }
    }

    /// <summary>
    /// Обрабатывает изменение статуса и управляет отображением overlay
    /// </summary>
    private async Task ProcessStatusChange(RecordingStatus newStatus)
    {
        switch (newStatus)
        {
            case RecordingStatus.Recording:
                await _overlayDisplay.ShowAsync(RecordingStatus.Recording);
                break;
                
            case RecordingStatus.Processing:
                await _overlayDisplay.UpdateStatusAsync(RecordingStatus.Processing);
                break;
                
            case RecordingStatus.Completed:
                await _overlayDisplay.UpdateStatusAsync(RecordingStatus.Completed);
                await DelayAndHide(2000); // Показать 2 секунды
                break;
                
            case RecordingStatus.Error:
            case RecordingStatus.Cancelled:
                await _overlayDisplay.UpdateStatusAsync(newStatus);
                await DelayAndHide(1000); // Показать 1 секунду
                break;
                
            case RecordingStatus.Idle:
                await _overlayDisplay.HideAsync();
                break;
        }
    }

    /// <summary>
    /// Показывает статус с задержкой и затем скрывает overlay
    /// </summary>
    private async Task DelayAndHide(int delayMs)
    {
        try
        {
            await Task.Delay(delayMs);
            await _overlayDisplay.HideAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка в DelayAndHide с задержкой {DelayMs}ms", delayMs);
        }
    }

    /// <summary>
    /// Проверяет включен ли overlay в настройках
    /// </summary>
    private bool IsOverlayEnabled()
    {
        try
        {
            return _configService?.CurrentConfig?.Overlay?.IsEnabled == true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка проверки настроек overlay");
            return false; // По умолчанию отключен при ошибке
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            UnsubscribeFromVoiceService();
            _isDisposed = true;
            _logger.Debug("OverlayEventCoordinator disposed");
        }
    }
}