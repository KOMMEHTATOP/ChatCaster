using ChatCaster.Core.Models;

namespace ChatCaster.Core.Services.Audio;

/// <summary>
/// Абстрактный сервис распознавания речи (не привязан к конкретному движку)
/// </summary>
public interface ISpeechRecognitionService
{
    Task<bool> InitializeAsync(SpeechRecognitionConfig config);
    Task<VoiceProcessingResult> RecognizeAsync(byte[] audioData, CancellationToken cancellationToken = default);
    Task<bool> ReloadConfigAsync(SpeechRecognitionConfig config);

    bool IsInitialized { get; }
    string EngineName { get; }
    string EngineVersion { get; }

    // Получение информации о возможностях движка
    Task<SpeechEngineCapabilities> GetCapabilitiesAsync();
    Task<IEnumerable<string>> GetSupportedLanguagesAsync();
}
