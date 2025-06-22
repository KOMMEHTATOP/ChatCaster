using ChatCaster.Core.Services;
using ChatCaster.Core.Models;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Контейнер сервисов для Windows реализации
/// Содержит все runtime сервисы приложения
/// </summary>
public class ServiceContext
{
    public IGamepadService? GamepadService { get; set; }
    public IAudioCaptureService? AudioService { get; set; }
    public ISpeechRecognitionService? SpeechService { get; set; }
    public ISystemIntegrationService? SystemService { get; set; }
    public IOverlayService? OverlayService { get; set; }
    public IVoiceRecordingService? VoiceRecordingService { get; set; }
    public IConfigurationService? ConfigurationService { get; set; }

    public AppConfig Config { get; set; } = new();
    
    public ServiceContext(AppConfig config)
    {
        Config = config;
    }
}
