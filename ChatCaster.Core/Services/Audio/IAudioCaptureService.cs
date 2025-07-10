using ChatCaster.Core.Models;

namespace ChatCaster.Core.Services.Audio;

/// <summary>
/// Сервис для работы с аудио устройствами (Platform-specific)
/// </summary>
public interface IAudioCaptureService
{
    event EventHandler<float>? VolumeChanged;
    event EventHandler<byte[]>? AudioDataReceived;
    Task<bool> TestMicrophoneAsync();

    Task<IEnumerable<AudioDevice>> GetAvailableDevicesAsync();
    Task<AudioDevice?> GetDefaultDeviceAsync();
    Task<bool> SetActiveDeviceAsync(string deviceId);

    Task<bool> StartCaptureAsync(AudioConfig config);
    Task StopCaptureAsync();

    bool IsCapturing { get; }
    float CurrentVolume { get; }
    AudioDevice? ActiveDevice { get; }
}
