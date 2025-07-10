using ChatCaster.Core.Events;
using ChatCaster.Core.Models;

namespace ChatCaster.Core.Services.Core;

/// <summary>
/// Главный сервис управления записью голоса
/// </summary>
public interface IVoiceRecordingService
{
    event EventHandler<RecordingStatusChangedEvent>? StatusChanged;
    event EventHandler<VoiceRecognitionCompletedEvent>? RecognitionCompleted;

    RecordingState CurrentState { get; }
    bool IsRecording { get; }

    Task<bool> StartRecordingAsync(CancellationToken cancellationToken = default);
    Task<VoiceProcessingResult> StopRecordingAsync(CancellationToken cancellationToken = default);
    Task CancelRecordingAsync();

    Task<bool> TestMicrophoneAsync();
    Task<VoiceProcessingResult> ProcessAudioDataAsync(byte[] audioData, CancellationToken cancellationToken = default);

}
